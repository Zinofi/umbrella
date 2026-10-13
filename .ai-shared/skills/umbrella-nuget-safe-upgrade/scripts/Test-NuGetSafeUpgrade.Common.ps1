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
