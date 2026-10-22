# MountainGen — C# Windows Forms

A modern Windows Forms reinterpretation of Riley T. Perry's 2002 VB6 MountainGen:
https://github.com/Agmash29a/MountainGen

## Requirements
- Windows 10/11
- Visual Studio 2022 with **.NET desktop development**, or the .NET 8 SDK.

## Run
Open `MountainGen.WinForms.csproj` in Visual Studio and press F5.
Or, on Windows:

```powershell
dotnet run --project MountainGen.WinForms.csproj
```

## Features
- Recursive midpoint-displacement (diamond-square) terrain, repeatable with a seed
- Adjustable subdivision detail, roughness, and vertical exaggeration
- Software 3D triangular mesh renderer with flat Lambert lighting
- Vegetation, rock, snow and water altitude colouring
- Camera orbit by dragging, zoom by mouse wheel, wireframe mode, XYZ axes
- PNG/BMP screenshots and Wavefront OBJ mesh export

## Differences from the VB6 original
The original terrain routines recursively subdivided individual triangle edges. This version uses a shared-vertex height grid with diamond-square subdivision, avoiding cracks in adjacent triangles. The original custom viewing transformations/hidden-surface-removal routines are replaced with a compact orthographic camera and painter-sorted polygons. Therefore results are **not pixel-identical** or numerically identical to the VB6 application. It is a functional modernization rather than a literal port.

The application uses only standard .NET Windows Forms and System.Drawing, with no external NuGet dependencies.
