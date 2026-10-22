# MountainGen — OpenTK GPU rendering branch

This branch upgrades the original [MountainGen WinForms](https://github.com/Agmash29a/MountainGen-WinForms) port to **GPU rendering**, without replacing its Windows Forms controls.

## Setup
- Windows 10/11 and an OpenGL 3.3-capable graphics driver
- .NET 8 SDK or Visual Studio 2022 with the .NET desktop development workload
- NuGet restore downloads `OpenTK.GLControl 4.0.2` and its OpenTK dependencies

```powershell
git switch feature/opentk-gpu-rendering
dotnet restore
dotnet run --project MountainGen.WinForms.csproj
```

## Implementation
- `GLControl` embedded in the right pane of the existing Windows Forms UI
- OpenGL 3.3 vertex and fragment shaders; height/slope-based vegetation, rocks and snow, per-pixel Lambert lighting
- Single indexed vertex buffer, element index buffer, and vertex array object; hardware depth buffer replaces the CPU painter-sort
- GPU mesh rebuilt only when the terrain is regenerated; orbit, zoom and lighting changes update matrices/uniforms
- Drag mouse to orbit; scroll wheel to zoom; existing terrain controls, wireframe, PNG/BMP snapshot and OBJ export retained
- GPU rendering requires an OpenGL 3.3 capable driver. OpenGL context creation may fail on Remote Desktop or old GPUs.

## Known differences / limitations
- This feature branch is a modernized implementation of the original VB6 concept, **not** an exact numerical or visual port.
- The wireframe and basic terrain colouring are implemented; `Show axes` is retained in the UI but is not yet drawn in OpenGL.
- Water level currently affects the fragment colour threshold, rather than generating a separate flat water surface.
- Smooth camera changes require mouse events; no perpetual render loop runs while idle.
- Requires Windows for build/runtime. The code was authored without local Windows SDK verification; inspect GitHub Actions for compilation results.

## Build
```powershell
dotnet build MountainGen.WinForms.csproj --configuration Release
```
