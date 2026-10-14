---
name: umbrella-nuget-safe-upgrade
description: 'Safely analyze and apply NuGet package upgrades in multi-targeted .NET repositories using exclusions, framework-aware version selection, restore checks, and transitive dependency graph validation.'
---

# NuGet Safe Upgrade

## Purpose

This skill safely analyzes and applies NuGet package upgrades while respecting exclusions, framework-coupling rules, and transitive dependency graph constraints.

## Assets

- `scripts\Invoke-NuGetSafeUpgrade.ps1`
- `scripts\NuGetSafeUpgrade.Common.ps1`
- `scripts\Test-NuGetSafeUpgrade.Common.ps1`
- `nuget-upgrade-exclusions.schema.json`

## Inputs

- optional package IDs to limit scope
- optional project paths to limit scope
- repository root or current working directory
- root-level `nuget-upgrade-exclusions.json`

## Workflow

1. Read `nuget-upgrade-exclusions.json`.
2. Run the wrapper script in `.github\skills\umbrella-nuget-safe-upgrade\scripts\`.
3. Start in Analyze mode unless the user explicitly requested direct application.
4. Filter scope by package IDs or project paths if provided.
5. Let the shared implementation select the highest safe candidate version for each package.
6. Keep only changes that pass restore and transitive graph checks.
7. Return successful, skipped, and blocked packages with override guidance.
8. After each Apply pass, check the blocked list for lockstep family packages and run a second Apply pass if any are present.
9. After the final Apply pass, run `dotnet build <solutionPath>` and verify the solution builds cleanly. Report any build failures before declaring the upgrade complete.

## Framework package baselines

Packages listed in `frameworkCoupledFamilies` (in `nuget-upgrade-exclusions.json`) are capped at the target framework's major version for net5+ TFMs. Both direct references and resolved transitive dependencies must respect that cap: .NET 8 stays on major 8 or lower, .NET 9 on major 9 or lower, and .NET 10 on major 10 or lower.

Legacy TFMs can use an explicit package baseline through `frameworkMajorOverrides`. The Umbrella repository and starter configuration set `"frameworkMajorOverrides": { "netstandard2.0": 10, "net462": 10 }`, allowing compatible framework-coupled packages and dependencies through major 10 on both targets, matching the .NET 10 cap. This permits upgrades such as `System.Text.Json` 6.x to 10.x and Redis 2.x to 3.x, provided restore and resolved-graph checks pass. Package major 10 does not itself imply a .NET 10 runtime requirement; the candidate must still supply assets compatible with the actual target framework.

Overrides apply only to legacy TFMs (`netstandard*`, `net4*`); they cannot raise the .NET 8 or 9 caps. Unconfigured legacy TFMs retain the current-major cap for direct framework-coupled packages and the package-major-upgrade guard. They do not have a numeric transitive cap; configure a baseline if one is required. Existing configurations without this optional property keep their previous behavior.

`System.Text.Json` and `System.Net.Http.Json` remain in `frameworkCoupledFamilies`, so their direct and transitive versions follow these baselines. Do not pin `netstandard2.0` or `net462` to an old package major solely because it is a legacy TFM.

`Microsoft.Extensions.Caching.Hybrid` is covered by the existing `Microsoft.Extensions.` family prefix. Keep its version framework-coupled: .NET 9 references stay on the 9.x line while .NET 10 references track the 10.x line.

`Microsoft.Maui.` packages follow the same contract and must also appear in `frameworkCoupledFamilies`. More generally, any SDK family whose major version must match the target framework's major (MAUI, ASP.NET Core, EF Core, etc.) belongs in this list. Check conditional references against the target framework's cap or configured legacy baseline. If a framework-coupled family is missing from `frameworkCoupledFamilies`, add it to `nuget-upgrade-exclusions.json` before re-running.

A cap that applies to only *some* of a project's TFMs does not block the upgrade outright: the capped TFMs keep the current version and the remaining TFMs are upgraded, via the framework split described below. An unconditional reference in a multi-targeted project therefore becomes per-TFM conditional blocks on first upgrade — each tracking its own major thereafter.

## Framework-split upgrades

When a package upgrade is blocked on some TFMs but would be safe on others, the script automatically splits the single unconditional `<PackageReference>` into per-framework conditional `<ItemGroup>` blocks:

- **Blocked TFMs** keep the current version (e.g., `net8.0`/`net9.0` when the new version pulls v10 framework-coupled dependencies).
- **Allowed TFMs** receive the upgraded version (e.g., `netstandard2.0` and `net462` with configured major-10 baselines, and `net10.0`).

Split candidates appear in the `successful` list with action `Analyzed (split candidate)` or `Applied (split by framework)` and include `upgradeFrameworks`/`keepFrameworks` fields.

Split only applies to unconditional `PackageReference` items directly in `.csproj` files. `PackageVersion` entries in `Directory.Packages.props` and already-conditional references are not split.

For Central Package Management, each `PackageVersion` condition is evaluated against the consuming project's `TargetFramework` and `MSBuildProjectName`. Only matching project/framework pairs are validated, and the exact centralized item is updated even when multiple conditional entries share a package ID and version.

Unversioned `PackageReference` items declared in the nearest `Directory.Packages.props` are included in the consumer inventory, including shared test tooling. Their item and group conditions are respected, with an unconditional project `IsTestProject` value used to distinguish test consumers. An absent or conditional value remains unknown because imports can set it; affected references are validated conservatively. These central references receive the same restore and resolved-graph checks as references declared in `.csproj` files.

**TFM ordering:** Condition strings and `<ItemGroup>` blocks are written in ascending semantic order: `netstandard*` (lowest) → `net4*` → `net5+` (ascending by major version). Lower TFMs always appear first in the file, matching conventional `.csproj` reading order.

**Legacy-TFM major-version guard:** For unconfigured `netstandard*` and `net4*` TFMs, a candidate that bumps the package's major version is automatically added to the blocked set. An explicit `frameworkMajorOverrides` entry replaces this blanket guard with the configured direct and transitive package cap plus restore validation. `-OverrideBlockedPackageId` remains available after manual review, but is not needed for compatible upgrades within a configured baseline.

## Lockstep package families

Some packages must be upgraded together — for example `System.Composition.AttributedModel`, `System.Composition.Runtime`, and `System.Composition.TypedParts` all share the same version number and each member pulls the others transitively.

The script processes packages independently, so when it tests a family member in isolation the siblings have already been reverted (Analyze) or not yet written (Apply). This produces a NU1605 blocked result:

```
Candidate X.Y.Z failed restore: Detected package downgrade: <SiblingPackage> from X.Y.Z to <OldVersion>
```

**How to identify:** a blocked package's NU1605 error references sibling packages that appear in the *successful* list of the same report, upgraded to the same candidate version.

**How to resolve:** in Apply mode no action is needed — siblings are written to disk before each subsequent package is tested, so the family resolves in a single Apply pass. In Analyze mode the blocked entry is expected; proceed directly to Apply.

## Command examples

Analyze:

```powershell
powershell -ExecutionPolicy Bypass -File .github\skills\umbrella-nuget-safe-upgrade\scripts\Invoke-NuGetSafeUpgrade.ps1 -Mode Analyze
```

Apply:

```powershell
powershell -ExecutionPolicy Bypass -File .github\skills\umbrella-nuget-safe-upgrade\scripts\Invoke-NuGetSafeUpgrade.ps1 -Mode Apply
```

Override a blocked package after review:

```powershell
powershell -ExecutionPolicy Bypass -File .github\skills\umbrella-nuget-safe-upgrade\scripts\Invoke-NuGetSafeUpgrade.ps1 -Mode Apply -PackageId Microsoft.Extensions.Logging.Abstractions -OverrideBlockedPackageId Microsoft.Extensions.Logging.Abstractions
```

## Output expectations

- `successful`: packages safely upgraded or safe upgrade candidates in Analyze mode
- `skipped`: packages excluded or with no newer safe version
- `blocked`: packages that failed compatibility or graph checks
- `options`: the requested package/project filters, prerelease setting, configured framework-major baselines, and explicit blocked-package overrides; each blocked item also contains safe follow-up commands

## Safety rules

- Never ignore `nuget-upgrade-exclusions.json`.
- Never flatten framework-specific versions into a single package version.
- Never keep a candidate that fails restore.
- Review newly introduced NuGet audit warnings, including dependencies of fallback candidates. Keep a vulnerable fallback blocked unless a compatible patched dependency set has been validated.
- Never treat a NuGet source/query failure as evidence that no newer versions exist; stop and surface the failure.
- Never keep a candidate that resolves framework-coupled package families beyond the target framework major or its configured legacy baseline unless explicitly overridden.
- Always verify that already-conditional package references are upgraded to a version compatible with their condition's TFM. If a package family is TFM-version-coupled but absent from `frameworkCoupledFamilies`, add it before applying upgrades.
- Never declare an Apply pass complete without running `dotnet build` to confirm the solution compiles cleanly.
- Keep reusable logic in `.ai-shared\` and keep this folder as a thin wrapper.
