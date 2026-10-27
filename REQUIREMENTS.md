# Velo System & Development Requirements

## 1. System Requirements

### Target & Deployment Environment
- **Operating System:** Windows 10 version 22H2 64-bit (x64 / AMD64).
- **Architecture:** `win-x64` exclusively (Windows 10 x86, ARM64, Windows 11, Linux, macOS are non-targets as defined in PRD v2.0).
- **RAM:** Minimum 2 GB (Recommended: 4 GB+).
- **Disk Space:** 150 MB for installation, plus required storage for active downloads.

---

## 2. Developer Prerequisites & Tooling

To build, test, and contribute to Velo, the following free tools are required:

### 1. .NET 10.0 SDK (x64)
- **Install via Windows Package Manager (winget):**
  ```powershell
  winget install Microsoft.DotNet.SDK.10
  ```
- **Verify installation:**
  ```powershell
  dotnet --version
  ```

### 2. Supported IDEs / Editors
- **Visual Studio 2026 / 2022 Community (Free)**
  - Workload: *.NET desktop development*
  - Extension: *Avalonia for Visual Studio*
- **Visual Studio Code (Free)**
  - Extensions: *C# Dev Kit* (`ms-dotnettools.csdevkit`), *Avalonia for VSCode* (`AvaloniaTeam.vscode-avalonia`)
- **JetBrains Rider** (with Avalonia plugin)

### 3. Optional Tooling for Packaging
- **NSIS (Nullsoft Scriptable Install System)**: For generating `Velo-win10-x64-v{version}-setup.exe`.
  ```powershell
  winget install NSIS.NSIS
  ```

---

## 3. Package & Dependency Matrix

All packages are pinned and managed centrally via `Directory.Packages.props`:

| Package | Version | Purpose |
| :--- | :--- | :--- |
| **`Avalonia`** | `11.2.3` | Cross-platform UI toolkit (running on Windows 10 x64) |
| **`Avalonia.Desktop`** | `11.2.3` | Desktop platform integration |
| **`Avalonia.Themes.Fluent`** | `11.2.3` | Fluent UI Design System (Dark & Light) |
| **`Avalonia.Fonts.Inter`** | `11.2.3` | Modern Inter typography |
| **`CommunityToolkit.Mvvm`** | `8.4.0` | High-performance MVVM source generators |
| **`Microsoft.Data.Sqlite`** | `10.0.0` | Embedded SQLite transactional persistence |
| **`Serilog`** | `4.2.0` | Structured diagnostic logging |
| **`Serilog.Sinks.File`** | `6.0.0` | Rotating file logs with secret redaction |
| **`Serilog.Sinks.Console`** | `6.0.0` | Console output for debug runs |
| **`Microsoft.NET.Test.Sdk`** | `17.13.0` | Unit test execution |
| **`xunit`** | `2.9.3` | Test framework |
| **`xunit.runner.visualstudio`** | `3.0.2` | Visual Studio test runner integration |
| **`FluentAssertions`** | `7.2.0` | Fluent assertions for unit & integration tests |
| **`Moq`** | `4.20.72` | Mocking library for interfaces |

---

## 4. Quick Start Commands

### Clone & Restore
```powershell
git clone https://github.com/ahmed-el4enawy/Velo.git
cd Velo
dotnet restore
```

### Build the Solution
```powershell
dotnet build Velo.sln -c Debug
```

### Run Unit & Integration Tests
```powershell
dotnet test Velo.sln --logger "console;verbosity=normal"
```

### Launch Velo Desktop UI
```powershell
dotnet run --project src/Velo.Desktop/Velo.Desktop.csproj
```

### Publish Self-Contained x64 Release
```powershell
dotnet publish src/Velo.Desktop/Velo.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```
