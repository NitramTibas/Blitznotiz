# Windows build and test guide

## Current preparation status

The initial preparation adds build automation and an implementation specification.
A green preparation build contains the original app behavior. Middle-click support is only present after an implementation commit adds it.
Check BUILD-INFO.txt in each download for its exact commit.

## Download and run

1. Open this fork's Actions tab and select a successful Windows build for codex/middle-mouse-toggle.
2. Download the Blitznotiz-win-x64 artifact and extract its ZIP.
3. Close the original app first. The executable still uses the internal name Blitztext.exe.
4. Back up %APPDATA%/Blitztext before testing: original and fork share configuration, DPAPI secrets and the single-instance identity.
5. Run Blitztext.exe only where local/company policy permits. No .NET SDK or separate .NET runtime is required for this self-contained build.
6. Keep LICENSE.txt with redistributed copies. The SHA256SUMS.txt file identifies the built executable.

No public GitHub Release is created by the workflow. Artifacts are retained for 14 days.

## Reproduce on a Windows build machine

Requires .NET 9 SDK; do not install it on the restricted work PC.

```powershell
dotnet restore Blitztext.csproj --runtime win-x64 -p:SelfContained=true -p:PublishSingleFile=true
dotnet build Blitztext.csproj --configuration Release --runtime win-x64 --self-contained true --no-restore
dotnet publish Blitztext.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false --output dist
```

CI runs all test projects under tests/ when present. Until implementation adds those tests, CI explicitly reports that no automated tests exist.
The upstream project uses floating Avalonia 11.* package versions; record/pin resolved versions if needed to make comparisons reproducible. Do not upgrade the app framework as part of the shortcut change.

## Codex Cloud setup

Create a dedicated environment for NitramTibas/Blitznotiz. Do not reuse or modify an unrelated environment.
Ask the setup agent to use codex/middle-mouse-toggle, install .NET 9 in the cloud environment, and read AGENTS.md plus docs/CODEX_CLOUD_TASK.md.
Allow the package sources needed for the SDK/NuGet during environment setup. Keep access scoped to this project.
Linux restore/build may need -p:EnableWindowsTargeting=true. A Linux build does not validate Windows hooks, NAudio hardware recording or DPAPI.
Use GitHub's Windows workflow as the Windows build authority. Do not provide a production OpenAI key to CI or the setup agent.

After preparing the environment, use docs/CODEX_CLOUD_TASK.md as the implementation brief.
Do not merge main, push upstream or publish a Release as part of preparation.
