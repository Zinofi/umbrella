[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path -Path $PSScriptRoot -ChildPath 'NuGetSafeUpgrade.Common.ps1')

function Assert-NuGetUpgradeTest {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

$testRoot = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath ('umbrella-nuget-safe-upgrade-' + [guid]::NewGuid().ToString('N'))
[System.IO.Directory]::CreateDirectory($testRoot) | Out-Null

try {
    if ($env:OS -eq 'Windows_NT') {
        Add-Type -TypeDefinition @'
using System.IO;
using System.Threading;
public static class NuGetUpgradeLockFixture {
    public static Thread HoldFile(string path) {
        var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var thread = new Thread(() => { Thread.Sleep(500); stream.Dispose(); });
        thread.Start();
        return thread;
    }
}
'@
        $lockedFilePath = Join-Path $testRoot 'locked.props'
        [System.IO.File]::WriteAllText($lockedFilePath, 'before')
        $lockThread = [NuGetUpgradeLockFixture]::HoldFile($lockedFilePath)
        Write-NuGetUpgradeFileContent -Path $lockedFilePath -Content 'after' -Encoding ([System.Text.UTF8Encoding]::new($true))
        $lockThread.Join()
        $writtenBytes = [System.IO.File]::ReadAllBytes($lockedFilePath)
        Assert-NuGetUpgradeTest -Condition ((Get-Content -LiteralPath $lockedFilePath -Raw) -eq 'after') -Message 'A transient sharing lock prevented the package-file write.'
        Assert-NuGetUpgradeTest -Condition ($writtenBytes[0] -eq 0xEF -and $writtenBytes[1] -eq 0xBB -and $writtenBytes[2] -eq 0xBF) -Message 'The retry writer did not preserve the requested encoding.'
    }
    $nonTransientFailure = $false
    try { Write-NuGetUpgradeFileContent -Path $testRoot -Content 'invalid target' -Encoding ([System.Text.UTF8Encoding]::new($false)) }
    catch { $nonTransientFailure = $true }
    Assert-NuGetUpgradeTest -Condition $nonTransientFailure -Message 'A non-sharing write failure was suppressed.'

    $policyTestRoot = Join-Path -Path $testRoot -ChildPath 'framework-policy'
    [System.IO.Directory]::CreateDirectory($policyTestRoot) | Out-Null
    $policyConfigPath = Join-Path $policyTestRoot 'nuget-upgrade-exclusions.json'
    $policyConfigContent = '{"packages":[],"patterns":[],"frameworkCoupledFamilies":["Microsoft.Extensions.","System.Text.Json"],"frameworkMajorOverrides":{"netstandard2.0":10,"net462":10},"options":{"allowPrerelease":false}}'
    [System.IO.File]::WriteAllText($policyConfigPath, $policyConfigContent, [System.Text.UTF8Encoding]::new($false))
    $policyConfig = Get-NuGetUpgradeConfig -RepoRoot $policyTestRoot
    $policyArguments = @{
        FrameworkCoupledFamilies = $policyConfig.FrameworkCoupledFamilies
        FrameworkMajorOverrides = $policyConfig.FrameworkMajorOverrides
    }
    Assert-NuGetUpgradeTest -Condition ($policyConfig.FrameworkMajorOverrides['netstandard2.0'] -eq 10) -Message 'The configured netstandard2.0 baseline was not loaded.'
    Assert-NuGetUpgradeTest -Condition ($policyConfig.FrameworkMajorOverrides['net462'] -eq 10) -Message 'The configured net462 baseline was not loaded.'
    foreach ($policyFramework in @('netstandard2.0', 'net462', 'net8.0', 'net9.0', 'net10.0')) {
        $policyCap = Get-NuGetUpgradeGuardrail -PackageId 'System.Text.Json' -TargetFramework $policyFramework -CurrentVersion '6.0.11' @policyArguments
        $expectedCap = if ($policyFramework -eq 'net8.0') { 8 } elseif ($policyFramework -eq 'net9.0') { 9 } else { 10 }
        Assert-NuGetUpgradeTest -Condition ($policyCap -eq $expectedCap) -Message "Incorrect direct-package cap for $policyFramework."
        $policyDefinition = [pscustomobject]@{
            PackageId = 'System.Text.Json'
            CurrentVersion = '6.0.11'
            ValidationReferences = @([pscustomobject]@{ RelativeProjectPath = 'Policy.csproj'; TargetFrameworks = @($policyFramework) })
        }
        $directViolations = @(Test-NuGetUpgradeCandidateAgainstGuardrails -Definition $policyDefinition -CandidateVersion '10.0.5' @policyArguments)
        $expectedViolationCount = if ($expectedCap -lt 10) { 1 } else { 0 }
        Assert-NuGetUpgradeTest -Condition ($directViolations.Count -eq $expectedViolationCount) -Message "Incorrect direct-package decision for $policyFramework."
        $resolvedPolicyPackages = @(
            [pscustomobject]@{ PackageId = 'Microsoft.Extensions.Logging.Abstractions'; Version = '10.0.5'; Kind = 'Transitive' }
            [pscustomobject]@{ PackageId = 'Microsoft.Extensions.DependencyInjection.Abstractions'; Version = '10.0.5'; Kind = 'Transitive' }
            [pscustomobject]@{ PackageId = 'System.Text.Json'; Version = '10.0.5'; Kind = 'Transitive' }
        )
        $transitiveViolations = @(Test-NuGetUpgradeResolvedPackages -ResolvedPackages $resolvedPolicyPackages -TargetFramework $policyFramework @policyArguments)
        Assert-NuGetUpgradeTest -Condition ($transitiveViolations.Count -eq (3 * $expectedViolationCount)) -Message "Incorrect transitive-package decision for $policyFramework."
    }
    $futurePackage = @([pscustomobject]@{ PackageId = 'System.Text.Json'; Version = '11.0.0'; Kind = 'Transitive' })
    foreach ($legacyFramework in @('netstandard2.0', 'net462')) {
        $futureViolations = @(Test-NuGetUpgradeResolvedPackages -ResolvedPackages $futurePackage -TargetFramework $legacyFramework @policyArguments)
        Assert-NuGetUpgradeTest -Condition ($futureViolations.Count -eq 1 -and $futureViolations[0].MaxAllowedMajor -eq 10) -Message "The $legacyFramework transitive cap allowed major 11."
        $policyDefinition.ValidationReferences[0].TargetFrameworks = @($legacyFramework)
        $futureDirectViolations = @(Test-NuGetUpgradeCandidateAgainstGuardrails -Definition $policyDefinition -CandidateVersion '11.0.0' @policyArguments)
        Assert-NuGetUpgradeTest -Condition ($futureDirectViolations.Count -eq 1) -Message "The $legacyFramework direct framework-coupled cap allowed major 11."
    }
    $legacyCap = Get-NuGetUpgradeGuardrail -PackageId 'System.Text.Json' -TargetFramework 'net48' -CurrentVersion '6.0.11' @policyArguments
    Assert-NuGetUpgradeTest -Condition ($legacyCap -eq 6) -Message 'An unconfigured .NET Framework target lost its existing direct-package cap.'
    $oldConfigContent = $policyConfigContent.Replace(',"frameworkMajorOverrides":{"netstandard2.0":10,"net462":10}', '')
    [System.IO.File]::WriteAllText($policyConfigPath, $oldConfigContent, [System.Text.UTF8Encoding]::new($false))
    $oldConfig = Get-NuGetUpgradeConfig -RepoRoot $policyTestRoot
    Assert-NuGetUpgradeTest -Condition ($oldConfig.FrameworkMajorOverrides.Count -eq 0) -Message 'An existing configuration acquired an implicit framework override.'
    $missingConfig = Get-NuGetUpgradeConfig -RepoRoot $testRoot
    Assert-NuGetUpgradeTest -Condition ($missingConfig.FrameworkMajorOverrides.Count -eq 0) -Message 'A missing configuration acquired an implicit framework override.'
    foreach ($invalidOverride in @('{"net8.0":10}', '{"netstandard2.0":4}', '{"netstandard2.0":10.5}', '{"netstandard2.0":"10"}', 'null')) {
        $invalidConfigContent = $policyConfigContent.Replace('{"netstandard2.0":10,"net462":10}', $invalidOverride)
        [System.IO.File]::WriteAllText($policyConfigPath, $invalidConfigContent, [System.Text.UTF8Encoding]::new($false))
        $invalidConfigRejected = $false
        try { $null = Get-NuGetUpgradeConfig -RepoRoot $policyTestRoot } catch { $invalidConfigRejected = $true }
        Assert-NuGetUpgradeTest -Condition $invalidConfigRejected -Message "Invalid framework override was accepted: $invalidOverride."
    }
    [System.IO.File]::WriteAllText($policyConfigPath, $policyConfigContent, [System.Text.UTF8Encoding]::new($false))

    # Exercise the full workflow without network access, including major-bump policy,
    # graph-cap rejection, failed restore rollback, and framework-specific splitting.
    $workflowTestRoot = Join-Path $testRoot 'workflow-policy'
    [System.IO.Directory]::CreateDirectory($workflowTestRoot) | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Invoke-NuGetSafeUpgrade.ps1') -Destination $workflowTestRoot
    $workflowStubs = @'
function Get-NuGetUpgradeAvailableVersions { param([string]$PackageId) return @('2.0.0') }
function Invoke-NuGetUpgradeRestore {
    param([string]$Path)
    if (Test-Path -LiteralPath (Join-Path (Split-Path $Path -Parent) 'fail-restore')) {
        return [pscustomobject]@{ ExitCode = 1; Output = 'NU1202: No assets compatible with the requested target framework.' }
    }
    return [pscustomobject]@{ ExitCode = 0; Output = 'Restore succeeded.' }
}
function Get-NuGetUpgradeResolvedPackages {
    param([string]$ProjectPath, [string]$TargetFramework)
    $versionPath = Join-Path (Split-Path $ProjectPath -Parent) 'resolved-version.txt'
    return @([pscustomobject]@{ PackageId = 'System.Text.Json'; Version = (Get-Content -LiteralPath $versionPath -Raw).Trim(); Kind = 'Transitive' })
}
'@
    $workflowCommon = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'NuGetSafeUpgrade.Common.ps1') -Raw) + [Environment]::NewLine + $workflowStubs
    [System.IO.File]::WriteAllText((Join-Path $workflowTestRoot 'NuGetSafeUpgrade.Common.ps1'), $workflowCommon, [System.Text.UTF8Encoding]::new($false))
    [System.IO.File]::WriteAllText((Join-Path $workflowTestRoot 'Policy.sln'), '', [System.Text.UTF8Encoding]::new($false))
    [System.IO.File]::WriteAllText((Join-Path $workflowTestRoot 'nuget-upgrade-exclusions.json'), $policyConfigContent, [System.Text.UTF8Encoding]::new($false))
    $workflowProjectPath = Join-Path $workflowTestRoot 'Policy.csproj'
    $workflowProjectContent = '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>netstandard2.0</TargetFramework></PropertyGroup><ItemGroup><PackageReference Include="Example.Package" Version="1.0.0" /></ItemGroup></Project>'
    [System.IO.File]::WriteAllText($workflowProjectPath, $workflowProjectContent, [System.Text.UTF8Encoding]::new($false))
    $workflowReportPath = Join-Path $workflowTestRoot 'report.json'
    $resolvedVersionPath = Join-Path $workflowTestRoot 'resolved-version.txt'
    $testPowerShellPath = (Get-Process -Id $PID).Path
    foreach ($workflowScenario in @('compatible', 'above-cap', 'incompatible-assets', 'unconfigured-legacy', 'net462-compatible', 'net462-above-cap', 'net462-incompatible-assets', 'net462-unconfigured-legacy', 'framework-split')) {
        $scenarioConfig = if ($workflowScenario -like '*unconfigured-legacy') { $oldConfigContent } else { $policyConfigContent }
        [System.IO.File]::WriteAllText((Join-Path $workflowTestRoot 'nuget-upgrade-exclusions.json'), $scenarioConfig, [System.Text.UTF8Encoding]::new($false))
        $scenarioProject = if ($workflowScenario -eq 'framework-split') { $workflowProjectContent.Replace('<TargetFramework>netstandard2.0</TargetFramework>', '<TargetFrameworks>netstandard2.0;net462;net8.0;net9.0;net10.0</TargetFrameworks>') } elseif ($workflowScenario -like 'net462-*') { $workflowProjectContent.Replace('netstandard2.0', 'net462') } else { $workflowProjectContent }
        [System.IO.File]::WriteAllText($workflowProjectPath, $scenarioProject, [System.Text.UTF8Encoding]::new($false))
        $resolvedVersion = if ($workflowScenario -like '*above-cap') { '11.0.0' } else { '10.0.5' }
        [System.IO.File]::WriteAllText($resolvedVersionPath, $resolvedVersion, [System.Text.UTF8Encoding]::new($false))
        $failRestorePath = Join-Path $workflowTestRoot 'fail-restore'
        if ($workflowScenario -like '*incompatible-assets') { [System.IO.File]::WriteAllText($failRestorePath, '') }
        elseif (Test-Path -LiteralPath $failRestorePath) { Remove-Item -LiteralPath $failRestorePath }
        $workflowOutput = & $testPowerShellPath -NoProfile -ExecutionPolicy Bypass -File (Join-Path $workflowTestRoot 'Invoke-NuGetSafeUpgrade.ps1') -Mode Analyze -RepoRoot $workflowTestRoot -PackageId 'Example.Package' -ReportPath $workflowReportPath 2>&1
        Assert-NuGetUpgradeTest -Condition ($LASTEXITCODE -eq 0) -Message ("Workflow scenario $workflowScenario failed: " + ($workflowOutput -join [Environment]::NewLine))
        $workflowReport = Read-NuGetUpgradeJsonFile -Path $workflowReportPath
        $scenarioAllowed = $workflowScenario -eq 'compatible' -or $workflowScenario -eq 'net462-compatible' -or $workflowScenario -eq 'framework-split'
        Assert-NuGetUpgradeTest -Condition (($workflowReport.successful.Count -eq 1) -eq $scenarioAllowed) -Message "Unexpected upgrade decision for $workflowScenario."
        Assert-NuGetUpgradeTest -Condition (($workflowReport.blocked.Count -eq 1) -eq (-not $scenarioAllowed)) -Message "Unexpected blocked decision for $workflowScenario."
        Assert-NuGetUpgradeTest -Condition ((Get-Content -LiteralPath $workflowProjectPath -Raw) -eq $scenarioProject) -Message "Analyze did not restore the original project for $workflowScenario."
        if ($workflowScenario -eq 'framework-split') {
            Assert-NuGetUpgradeTest -Condition (($workflowReport.successful[0].upgradeFrameworks -join ',') -eq 'netstandard2.0,net462,net10.0') -Message 'The framework split did not allow netstandard2.0, net462 and net10.0.'
            Assert-NuGetUpgradeTest -Condition (($workflowReport.successful[0].keepFrameworks -join ',') -eq 'net8.0,net9.0') -Message 'The framework split changed .NET 8 or 9 dependencies.'
        }
    }

    $commentTestRoot = Join-Path -Path $testRoot -ChildPath 'commented-item'
    [System.IO.Directory]::CreateDirectory($commentTestRoot) | Out-Null
    $commentTestPath = Join-Path -Path $commentTestRoot -ChildPath 'Directory.Packages.props'
    $commentTestContent = @'
<Project>
  <ItemGroup>
    <!-- <PackageVersion Include="Example.Package" Version="1.0.0" /> -->
    <PackageVersion Include="Example.Package" Version="1.0.0" />
  </ItemGroup>
</Project>
'@
    [System.IO.File]::WriteAllText($commentTestPath, $commentTestContent, [System.Text.UTF8Encoding]::new($false))

    $commentDefinition = Get-NuGetUpgradeVersionDefinitions -RepoRoot $commentTestRoot -ProjectReferences @() | Select-Object -First 1
    Set-NuGetUpgradeVersionInFile -FilePath $commentDefinition.FilePath -ItemName $commentDefinition.ItemName -PackageId $commentDefinition.PackageId -CurrentVersion $commentDefinition.CurrentVersion -NewVersion '2.0.0' -ItemIndex $commentDefinition.ItemIndex

    $commentTestResult = Get-Content -Path $commentTestPath -Raw
    Assert-NuGetUpgradeTest -Condition ($commentTestResult.Contains('<!-- <PackageVersion Include="Example.Package" Version="1.0.0" /> -->')) -Message 'The commented PackageVersion was modified.'
    Assert-NuGetUpgradeTest -Condition ($commentTestResult.Contains('<PackageVersion Include="Example.Package" Version="2.0.0" />')) -Message 'The live PackageVersion was not modified.'

    $precedenceTestRoot = Join-Path -Path $testRoot -ChildPath 'condition-precedence'
    [System.IO.Directory]::CreateDirectory($precedenceTestRoot) | Out-Null
    $precedenceTestPath = Join-Path -Path $precedenceTestRoot -ChildPath 'Directory.Packages.props'
    $precedenceTestContent = @'
<Project>
  <ItemGroup Condition="'$(TargetFramework)' == 'net8.0' Or '$(TargetFramework)' == 'net9.0'">
    <PackageVersion Include="Example.Package" Version="1.0.0" Condition="'$(MSBuildProjectName)' == 'Included.Project'" />
  </ItemGroup>
</Project>
'@
    [System.IO.File]::WriteAllText($precedenceTestPath, $precedenceTestContent, [System.Text.UTF8Encoding]::new($false))

    $projectReferences = @(
        [pscustomobject]@{
            ProjectPath = Join-Path -Path $precedenceTestRoot -ChildPath 'Included.Project.csproj'
            ProjectName = 'Included.Project'
            RelativeProjectPath = 'Included.Project.csproj'
            PackageId = 'Example.Package'
            HasVersion = $false
            ApplicableTargetFrameworks = @('net8.0', 'net9.0', 'net10.0')
        }
        [pscustomobject]@{
            ProjectPath = Join-Path -Path $precedenceTestRoot -ChildPath 'Excluded.Project.csproj'
            ProjectName = 'Excluded.Project'
            RelativeProjectPath = 'Excluded.Project.csproj'
            PackageId = 'Example.Package'
            HasVersion = $false
            ApplicableTargetFrameworks = @('net8.0', 'net9.0', 'net10.0')
        }
    )

    $precedenceDefinition = Get-NuGetUpgradeVersionDefinitions -RepoRoot $precedenceTestRoot -ProjectReferences $projectReferences | Select-Object -First 1
    $expectedCondition = "('`$(TargetFramework)' == 'net8.0' Or '`$(TargetFramework)' == 'net9.0') AND ('`$(MSBuildProjectName)' == 'Included.Project')"
    Assert-NuGetUpgradeTest -Condition ($precedenceDefinition.Condition -eq $expectedCondition) -Message "Combined condition did not preserve grouping: $($precedenceDefinition.Condition)"
    Assert-NuGetUpgradeTest -Condition ($precedenceDefinition.ValidationReferences.Count -eq 1) -Message 'The grouped condition included an excluded project.'
    Assert-NuGetUpgradeTest -Condition ($precedenceDefinition.ValidationReferences[0].ProjectName -eq 'Included.Project') -Message 'The grouped condition selected the wrong project.'
    Assert-NuGetUpgradeTest -Condition (($precedenceDefinition.ValidationReferences[0].TargetFrameworks -join ',') -eq 'net8.0,net9.0') -Message 'The grouped condition selected the wrong target frameworks.'

    $centralTestRoot = Join-Path -Path $testRoot -ChildPath 'central-references'
    [System.IO.Directory]::CreateDirectory($centralTestRoot) | Out-Null
    $centralTestContent = @'
<Project>
  <ItemGroup>
    <PackageVersion Include="Shared.Package" Version="1.0.0" />
    <PackageVersion Include="Coverage.Package" Version="8.0.0" Condition="'$(TargetFramework)' == 'net8.0' Or '$(TargetFramework)' == 'net9.0'" />
    <PackageVersion Include="Coverage.Package" Version="10.0.0" Condition="'$(TargetFramework)' == 'net10.0'" />
    <PackageReference Include="Shared.Package" />
  </ItemGroup>
  <ItemGroup Condition="'$(IsTestProject)' == 'true'">
    <PackageReference Include="Coverage.Package" Condition="'$(MSBuildProjectName)' == 'Included.Tests'" />
  </ItemGroup>
</Project>
'@
    [System.IO.File]::WriteAllText((Join-Path $centralTestRoot 'Directory.Packages.props'), $centralTestContent, [System.Text.UTF8Encoding]::new($false))
    foreach ($centralProjectName in @('Included.Tests', 'Other.Tests', 'Application')) {
        $centralTestFlag = if ($centralProjectName.EndsWith('.Tests')) { '<IsTestProject>true</IsTestProject>' } else { '<IsTestProject>false</IsTestProject>' }
        $centralProjectContent = '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>' + $centralTestFlag + '</PropertyGroup></Project>'
        [System.IO.File]::WriteAllText((Join-Path $centralTestRoot ($centralProjectName + '.csproj')), $centralProjectContent, [System.Text.UTF8Encoding]::new($false))
    }
    $centralInventory = Get-NuGetUpgradeInventory -RepoRoot $centralTestRoot
    $centralCoverageReferences = @($centralInventory.ProjectReferences | Where-Object PackageId -eq 'Coverage.Package')
    Assert-NuGetUpgradeTest -Condition ($centralCoverageReferences.Count -eq 1) -Message 'The centrally declared test reference included an excluded project or was not discovered.'
    Assert-NuGetUpgradeTest -Condition ($centralCoverageReferences[0].ProjectName -eq 'Included.Tests') -Message 'The central reference lost its test/project-name condition.'
    $centralCoverageDefinitions = @($centralInventory.VersionDefinitions | Where-Object PackageId -eq 'Coverage.Package')
    $centralOlderDefinition = $centralCoverageDefinitions | Where-Object CurrentVersion -eq '8.0.0' | Select-Object -First 1
    $centralNewerDefinition = $centralCoverageDefinitions | Where-Object CurrentVersion -eq '10.0.0' | Select-Object -First 1
    Assert-NuGetUpgradeTest -Condition ($centralOlderDefinition.ValidationReferences.Count -eq 1) -Message 'The older central version has no consumer validation.'
    Assert-NuGetUpgradeTest -Condition (($centralOlderDefinition.ValidationReferences[0].TargetFrameworks -join ',') -eq 'net8.0,net9.0') -Message 'The older central version selected the wrong frameworks.'
    Assert-NuGetUpgradeTest -Condition (($centralNewerDefinition.ValidationReferences[0].TargetFrameworks -join ',') -eq 'net10.0') -Message 'The newer central version selected the wrong frameworks.'
    $centralSharedDefinition = $centralInventory.VersionDefinitions | Where-Object PackageId -eq 'Shared.Package' | Select-Object -First 1
    Assert-NuGetUpgradeTest -Condition ($centralSharedDefinition.ValidationReferences.Count -eq 3) -Message 'The unconditional central reference did not include all projects.'

    # A closer central file replaces the parent import unless it explicitly imports it.
    $nestedTestRoot = Join-Path -Path $centralTestRoot -ChildPath 'nested'
    [System.IO.Directory]::CreateDirectory($nestedTestRoot) | Out-Null
    [System.IO.File]::WriteAllText((Join-Path $nestedTestRoot 'Directory.Packages.props'), '<Project><ItemGroup><PackageVersion Include="Nested.Package" Version="1.0.0" /><PackageReference Include="Nested.Package" /></ItemGroup></Project>', [System.Text.UTF8Encoding]::new($false))
    [System.IO.File]::WriteAllText((Join-Path $nestedTestRoot 'Nested.csproj'), '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>', [System.Text.UTF8Encoding]::new($false))
    $nestedReferences = @(Get-NuGetUpgradeProjectReferences -RepoRoot $centralTestRoot | Where-Object ProjectName -eq 'Nested')
    Assert-NuGetUpgradeTest -Condition ($nestedReferences.Count -eq 1 -and $nestedReferences[0].PackageId -eq 'Nested.Package') -Message 'The nested central file incorrectly inherited parent references.'

    $inheritedTestRoot = Join-Path -Path $testRoot -ChildPath 'inherited-test-property'
    [System.IO.Directory]::CreateDirectory($inheritedTestRoot) | Out-Null
    [System.IO.File]::WriteAllText((Join-Path $inheritedTestRoot 'Directory.Build.props'), '<Project><PropertyGroup><IsTestProject>true</IsTestProject></PropertyGroup></Project>', [System.Text.UTF8Encoding]::new($false))
    [System.IO.File]::WriteAllText((Join-Path $inheritedTestRoot 'Directory.Packages.props'), '<Project><PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally></PropertyGroup><ItemGroup><PackageVersion Include="Example.Package" Version="1.0.0" /></ItemGroup><ItemGroup Condition="''$(IsTestProject)'' == ''true''"><PackageReference Include="Example.Package" /></ItemGroup></Project>', [System.Text.UTF8Encoding]::new($false))
    $inheritedProjectPath = Join-Path $inheritedTestRoot 'Inherited.Tests.csproj'
    [System.IO.File]::WriteAllText($inheritedProjectPath, '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>', [System.Text.UTF8Encoding]::new($false))
    $inheritedInventory = Get-NuGetUpgradeInventory -RepoRoot $inheritedTestRoot
    $inheritedReferences = @($inheritedInventory.ProjectReferences | Where-Object PackageId -eq 'Example.Package')
    Assert-NuGetUpgradeTest -Condition ($inheritedReferences.Count -eq 1) -Message 'An imported IsTestProject value caused the central reference to be omitted.'
    $inheritedDefinition = $inheritedInventory.VersionDefinitions | Where-Object PackageId -eq 'Example.Package' | Select-Object -First 1
    Assert-NuGetUpgradeTest -Condition ($inheritedDefinition.ValidationReferences.Count -eq 1) -Message 'The inherited test consumer was not scheduled for graph validation.'
    Assert-NuGetUpgradeTest -Condition (($inheritedDefinition.ValidationReferences[0].TargetFrameworks -join ',') -eq 'net8.0') -Message 'The inherited test consumer selected the wrong framework.'

    # Prove that MSBuild really imports the property/reference in this fixture; it
    # deliberately has no IsTestProject or Microsoft.NET.Test.Sdk in the .csproj.
    $inheritedMsBuildOutput = & dotnet msbuild $inheritedProjectPath -getProperty:IsTestProject -getItem:PackageReference -nologo 2>&1
    Assert-NuGetUpgradeTest -Condition ($LASTEXITCODE -eq 0) -Message ('Inherited-property MSBuild evaluation failed: ' + ($inheritedMsBuildOutput -join [Environment]::NewLine))
    $inheritedEvaluation = ($inheritedMsBuildOutput -join [Environment]::NewLine) | ConvertFrom-Json
    Assert-NuGetUpgradeTest -Condition ($inheritedEvaluation.Properties.IsTestProject -eq 'true') -Message 'MSBuild did not import IsTestProject=true.'
    Assert-NuGetUpgradeTest -Condition (@($inheritedEvaluation.Items.PackageReference | Where-Object Identity -eq 'Example.Package').Count -eq 1) -Message 'MSBuild did not include the central package reference.'

    $nonTestRoot = Join-Path -Path $testRoot -ChildPath 'explicit-non-test-property'
    [System.IO.Directory]::CreateDirectory($nonTestRoot) | Out-Null
    Copy-Item -LiteralPath (Join-Path $inheritedTestRoot 'Directory.Build.props') -Destination $nonTestRoot
    Copy-Item -LiteralPath (Join-Path $inheritedTestRoot 'Directory.Packages.props') -Destination $nonTestRoot
    [System.IO.File]::WriteAllText((Join-Path $nonTestRoot 'Application.csproj'), '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net8.0</TargetFramework><IsTestProject>false</IsTestProject></PropertyGroup></Project>', [System.Text.UTF8Encoding]::new($false))
    $nonTestInventory = Get-NuGetUpgradeInventory -RepoRoot $nonTestRoot
    Assert-NuGetUpgradeTest -Condition ($nonTestInventory.ProjectReferences.Count -eq 0) -Message 'An explicit IsTestProject=false did not exclude the conditional reference.'
    Assert-NuGetUpgradeTest -Condition ($nonTestInventory.VersionDefinitions[0].ValidationReferences.Count -eq 0) -Message 'An unused central definition retained a null or excluded validation consumer.'

    Write-Host 'NuGet safe-upgrade regression tests passed.'
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        $resolvedTestRoot = [System.IO.Path]::GetFullPath($testRoot)
        $tempRootPrefix = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
        if (-not $resolvedTestRoot.StartsWith($tempRootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw 'Refusing to remove a regression-test directory outside the temporary directory.'
        }
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
