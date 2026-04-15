# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

FEALiTE2D is a fast and reliable 2D finite element analysis (FEA) library for frame, beam, and truss elements, written in C# (.NET 8) and published as a NuGet package.

## Build & Test Commands

PowerShell skripty v `build/` jsou primárním způsobem práce se solution:

```powershell
# Sestavení (Release)
.\build\build.ps1

# Sestavení v Debug konfiguraci
.\build\build.ps1 -Configuration Debug

# Spuštění všech testů
.\build\test.ps1

# Spuštění konkrétního testu
.\build\test.ps1 -Filter "FullyQualifiedName~StructureTest"

# Vyčištění výstupů (/out + dotnet clean)
.\build\clean.ps1
```

Alternativně přímo přes `dotnet`:

```bash
dotnet build FEALiTE2D.sln -c Release
dotnet test FEALiTE2D.sln
dotnet test FEALiTE2D.Tests/FEALiTE2D.Tests.csproj --filter "FullyQualifiedName~StructureTest"
```

**Test framework**: NUnit 3 (`FEALiTE2D.Tests/` cílí na `net8.0`)

**Target framework**: Všechny projekty cílí na `net8.0`. NuGet balíčky jsou generovány automaticky při buildu (`GeneratePackageOnBuild=true`).

## Build infrastruktura

### Directory.Build.props

Soubor `Directory.Build.props` v kořeni repozitáře obsahuje vlastnosti sdílené napříč všemi projekty:

- Metadata NuGet balíčků: `Authors`, `Copyright`, `RepositoryUrl`, `PackageLicenseExpression`, `PackageProjectUrl`
- `GenerateDocumentationFile=true` a `EnableNETAnalyzers=false`
- Přesměrování výstupů (viz níže)

Každý `.csproj` proto obsahuje pouze vlastnosti specifické pro daný projekt (`Description`, `PackageTags`, `Version`, `PackageReleaseNotes`).

### Výstupní adresář `/out`

Veškeré sestavené binárky jdou do `/out/` (ignorováno v `.gitignore`):

```
out/
  FEALiTE2D/Release/net8.0/        ← binárky hlavní knihovny
  FEALiTE2D.Plotting/Release/net8.0/
  FEALiTE2D.Tests/Debug/net8.0/
  packages/                         ← .nupkg soubory obou knihoven
```

Řízeno přes `BaseOutputPath` a `PackageOutputPath` v `Directory.Build.props`.

## Architecture

The solution has three projects:

- **FEALiTE2D** — core library
- **FEALiTE2D.Tests** — NUnit test suite
- **FEALiTE2D.Plotting** — optional DXF diagram output (depends on netDxf)

### Core Library Namespaces

**`FEALiTE2D.Structure`** — Central analysis engine.
- `Structure` — holds nodes, elements, load cases, and drives the FEA solve sequence
- `Assembler` — builds global stiffness matrices and load vectors from element contributions
- `PostProcessor` — extracts displacements, reactions, and internal forces after solve

**`FEALiTE2D.Elements`** — Structural components.
- `Node2D` — node with 3 DOF (UX, UY, RZ); can have `NodalSupport` or `NodalSpringSupport`
- `FrameElement2D` — 2D Euler–Bernoulli beam/column element; generates local and global stiffness matrices
- `SpringElement2D` — elastic support element
- `IElement` — common interface for all element types
- `NodalDegreeOfFreedom` — enum for the three nodal DOF

**`FEALiTE2D.Loads`** — Load definitions.
- `ILoad` — interface implemented by all load types
- `FramePointLoad`, `FrameUniformLoad`, `FrameTrapezoidalLoad` — element-level loads
- `NodalLoad` — concentrated nodal load
- `SupportDisplacementLoad` — prescribed support displacements
- `LoadCase` / `LoadCombination` — grouping and factoring of loads; `LoadCombination` applies magnification factors to one or more `LoadCase` instances

**`FEALiTE2D.CrossSections`** — Cross-section geometry.
- `Frame2DSection` (abstract) — base class; exposes A, Ay, Az, Iy, Iz, J
- Concrete types: `Generic2DSection`, `RectangularSection`, `CircularSection`, `IPESection`, `HollowTube`

**`FEALiTE2D.Materials`** — Material properties.
- `IMaterial` / `GenericIsotropicMaterial` — E (modulus), U (Poisson ratio), Gama (unit weight), Alpha (thermal expansion coefficient)

**`FEALiTE2D.Meshing`** — Element meshing.
- `LinearMesher` / `ILinearMesher` — subdivides frame elements into equal `LinearMeshSegment` pieces for post-processing

**`FEALiTE2D.Helper`** — Utilities: `LinearFunction`, `ExtensionMethods`

### Coordinate System & DOF Conventions

- Global: XY plane; Z perpendicular (right-hand rule)
- Each `Node2D` has 3 DOF: UX, UY, RZ (translation X, translation Y, rotation about Z)
- `LoadDirection` enum specifies whether a load is in the global or element-local coordinate system
- `Node2D.RotationAngle` defines a local coordinate system rotation relative to the global frame

### Key Design Patterns

- **Interface-based extensibility**: `IElement`, `ILoad`, `IMaterial`, `ILinearMesher` — add new element or load types by implementing these interfaces
- **Separation of analysis stages**: model setup → `Assembler` → sparse matrix solve (CSparse) → `PostProcessor`
- **Serialization**: `Structure`, `Node2D`, `FrameElement2D` are marked `[Serializable]`

### Key Dependencies

| Package | Purpose |
|---|---|
| `CSparse` | Sparse matrix factorization/solve |
| `MathNet.Numerics` | Numerical utilities |
| `netDxf.netstandard` | DXF output (Plotting project only) |

## Documentation

DocFX is used to generate API docs from XML comments (`docfx.json` at repo root). Docs are deployed to GitHub Pages via `.github/workflows/doc_deploy.yml`.
