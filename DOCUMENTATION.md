# Model Info Updater - Complete Technical Documentation

> A comprehensive guide to understanding how this Revit add-in was built, how Velopack auto-updates work, and how all components integrate together.

## Table of Contents

1. [Project Overview](#project-overview)
2. [Architecture Overview](#architecture-overview)
3. [Project Structure](#project-structure)
4. [File-by-File Breakdown](#file-by-file-breakdown)
5. [MVVM Pattern Implementation](#mvvm-pattern-implementation)
6. [Velopack Integration Deep Dive](#velopack-integration-deep-dive)
7. [GitHub Releases Integration](#github-releases-integration)
8. [Data Flow](#data-flow)
9. [Build and Release Process](#build-and-release-process)
10. [Multi-Framework Support](#multi-framework-support)
11. [Deployment Workflow](#deployment-workflow)
12. [Troubleshooting](#troubleshooting)

---

## Project Overview

**Model Info Updater** is a Revit add-in that allows users to view and edit Project Information parameters (Project Name, Project Number, Client Name, Project Status). It demonstrates:

- **MVVM Architecture** - Clean separation of concerns
- **Multi-Framework Targeting** - Supports both .NET 8.0 (Revit 2026) and .NET Framework 4.8 (Revit 2024)
- **Velopack Auto-Updates** - Automatic update checking and deployment via GitHub Releases
- **WPF UI** - Modern Windows Presentation Foundation interface

### Key Technologies

| Technology | Purpose |
|------------|---------|
| C# / .NET | Core programming language |
| WPF | User interface framework |
| Revit API | Interaction with Autodesk Revit |
| Velopack | Auto-update framework |
| GitHub Releases | Update distribution |

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                                 REVIT                                       │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                     ModelInfoUpdater Add-in                          │   │
│  │  ┌──────────┐    ┌──────────────┐    ┌───────────────────────────┐  │   │
│  │  │ App.cs   │───>│ Command.cs   │───>│ MainWindow (UI)           │  │   │
│  │  │(Startup) │    │(Button Click)│    │      │                    │  │   │
│  │  └────┬─────┘    └──────────────┘    │      ▼                    │  │   │
│  │       │                              │ MainViewModel             │  │   │
│  │       ▼                              │      │                    │  │   │
│  │  ┌─────────────────┐                 │      ▼                    │  │   │
│  │  │VelopackUpdate   │                 │ ProjectInfoService        │  │   │
│  │  │   Service       │                 │      │                    │  │   │
│  │  └────────┬────────┘                 │      ▼                    │  │   │
│  │           │                          │ Revit Document (API)      │  │   │
│  │           ▼                          └───────────────────────────┘  │   │
│  │  ┌─────────────────┐                                                │   │
│  │  │ GitHub API      │ (Check for updates)                            │   │
│  │  └─────────────────┘                                                │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘

                              │
                              │ (When update needed, launches external process)
                              ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                    ModelInfoUpdater.Updater.exe                             │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │  1. VelopackApp.Build().Run() - Handle pending updates              │   │
│  │  2. CheckForUpdatesAsync() - Query GitHub Releases                  │   │
│  │  3. DownloadUpdatesAsync() - Download new version                   │   │
│  │  4. ApplyUpdatesAndRestart() - Apply and restart                    │   │
│  │  5. DeployToRevit() - Copy files to Revit add-in folders            │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘

                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         GitHub Releases                                     │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │  - releases.win.json (Velopack metadata)                            │   │
│  │  - ModelInfoUpdater-X.X.X-full.nupkg (Full package)                 │   │
│  │  - ModelInfoUpdater-X.X.X-delta.nupkg (Delta updates)               │   │
│  │  - ModelInfoUpdater-win-Setup.exe (Installer)                       │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Project Structure

```
ModelInfoUpdater/
├── Model Info Updater.sln          # Visual Studio solution file
├── ModelInfoUpdater.addin          # Revit add-in manifest (for dev/manual install)
├── velopack.json                   # Velopack configuration
│
├── src/                            # Main add-in source code
│   ├── ModelInfoUpdater.csproj     # Project file (multi-targeting)
│   ├── App.cs                      # IExternalApplication - Revit startup
│   ├── Command.cs                  # IExternalCommand - Button handler
│   │
│   ├── Core/                       # MVVM infrastructure
│   │   ├── ViewModelBase.cs        # INotifyPropertyChanged base
│   │   └── RelayCommand.cs         # ICommand implementation
│   │
│   ├── Models/                     # Data models
│   │   └── ProjectInfoModel.cs     # DTO for project information
│   │
│   ├── ViewModels/                 # View logic
│   │   └── MainViewModel.cs        # Main window ViewModel
│   │
│   ├── Views/UI/                   # WPF views
│   │   ├── MainWindow.xaml         # UI layout
│   │   └── MainWindow.xaml.cs      # Code-behind
│   │
│   ├── Services/                   # Business logic services
│   │   ├── IProjectInfoService.cs  # Interface for Revit operations
│   │   ├── ProjectInfoService.cs   # Implementation
│   │   ├── IUpdateService.cs       # Interface for updates
│   │   └── VelopackUpdateService.cs # Velopack implementation
│   │
│   ├── Updater/                    # Standalone updater application
│   │   ├── ModelInfoUpdater.Updater.csproj
│   │   ├── Program.cs              # Updater entry point
│   │   ├── app.manifest            # Windows manifest
│   │   └── ModelInfoUpdater.addin.template
│   │
│   └── Properties/
│       └── AssemblyInfo.cs         # Version info
│
├── scripts/
│   └── Build-Release.ps1           # Build automation script
│
├── installer/
│   └── ModelInfoUpdater.iss        # Inno Setup installer script
│
├── build-output/                   # Compiled binaries
│   ├── net8.0-windows/             # Revit 2026 build
│   └── net48/                      # Revit 2024 build
│
└── releases/                       # Release packages
    ├── velopack/                   # Velopack packages
    └── *.zip                       # Manual distribution ZIPs
```

---

## MVVM Pattern Implementation

This project follows the Model-View-ViewModel (MVVM) pattern for clean separation of concerns:

```
┌─────────────────────────────────────────────────────────────────┐
│                          VIEW                                    │
│         MainWindow.xaml + MainWindow.xaml.cs                    │
│   - Displays data via data binding                               │
│   - Triggers commands via button clicks                          │
│   - Contains NO business logic                                   │
└─────────────────────────────────────────────────────────────────┘
                              │
                              │ Data Binding
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                       VIEWMODEL                                  │
│                    MainViewModel.cs                              │
│   - Exposes properties for binding                               │
│   - Contains presentation logic                                  │
│   - Exposes commands (ICommand)                                  │
│   - Calls services for business operations                       │
└─────────────────────────────────────────────────────────────────┘
                              │
                              │ Method Calls
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                        MODEL / SERVICES                          │
│   ProjectInfoModel.cs + IProjectInfoService / ProjectInfoService │
│   - Data transfer objects (DTOs)                                 │
│   - Business logic (Revit API calls)                             │
│   - No UI knowledge                                              │
└─────────────────────────────────────────────────────────────────┘
```

### Benefits Demonstrated:
1. **Testability** - Services can be mocked for unit testing
2. **Separation of Concerns** - UI, logic, and data are isolated
3. **Maintainability** - Changes in one layer don't affect others
4. **Dependency Injection** - Services are injected into ViewModels

---

## File-by-File Breakdown

### Core Application Files

#### `src/App.cs` - Application Entry Point

**Purpose:** Implements `IExternalApplication` interface. This is where Revit loads the add-in.

**Key Responsibilities:**
- Creates the ribbon tab ("TESTER") and panel ("Tools")
- Adds the "Model Info Updater" button to Revit's ribbon
- Initializes the `VelopackUpdateService` asynchronously (non-blocking)
- Checks for updates in the background on Revit startup
- Shows update notifications when the user clicks the button

```csharp
// Key code flow:
public Result OnStartup(UIControlledApplication application)
{
    // 1. Initialize Velopack (async, non-blocking)
    InitializeUpdateServiceAsync();

    // 2. Create ribbon UI
    application.CreateRibbonTab(TabName);
    RibbonPanel ribbonPanel = application.CreateRibbonPanel(TabName, PanelName);

    // 3. Add button
    PushButtonData buttonData = new PushButtonData(...);
    ribbonPanel.AddItem(buttonData);
}
```

#### `src/Command.cs` - Button Click Handler

**Purpose:** Implements `IExternalCommand`. Executes when user clicks the ribbon button.

**Key Responsibilities:**
- Gets the active Revit document
- Shows update notification if available (fire-and-forget)
- Creates and displays the `MainWindow`
- Sets Revit main window as owner for proper modal behavior

```csharp
public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
{
    // Get document
    Document doc = uiDoc.Document;

    // Check for updates (background, non-blocking)
    _ = App.ShowUpdateNotificationIfAvailableAsync();

    // Show UI
    MainWindow mainWindow = new MainWindow(doc);
    mainWindow.ShowDialog();
}
```

### MVVM Infrastructure

#### `src/Core/ViewModelBase.cs`

**Purpose:** Base class for all ViewModels implementing `INotifyPropertyChanged`.

**Key Features:**
- `OnPropertyChanged()` - Raises property change notification
- `SetProperty<T>()` - Sets backing field and raises notification if changed

```csharp
protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
{
    if (Equals(field, value)) return false;
    field = value;
    OnPropertyChanged(propertyName);
    return true;
}
```

#### `src/Core/RelayCommand.cs`

**Purpose:** Reusable `ICommand` implementation for MVVM command binding.

**Key Features:**
- Delegates execution to provided `Action<object>`
- Supports `CanExecute` predicate for enabling/disabling
- Integrates with WPF's `CommandManager` for automatic re-query

### Model Layer

#### `src/Models/ProjectInfoModel.cs`

**Purpose:** Data Transfer Object (DTO) representing project information.

**Properties:**
- `ProjectName` - The project's name
- `ProjectNumber` - Project identifier
- `ClientName` - Client/owner name
- `ProjectStatus` - Current status (e.g., "In Progress")

```csharp
public class ProjectInfoModel
{
    public string ProjectName { get; set; }
    public string ProjectNumber { get; set; }
    public string ClientName { get; set; }
    public string ProjectStatus { get; set; }
}
```

### ViewModel Layer

#### `src/ViewModels/MainViewModel.cs`

**Purpose:** Contains all presentation logic for the main window.

**Key Properties:**
- `CurrentProjectName/Number/ClientName/Status` - Read-only, displays current values
- `NewProjectName/Number/ClientName/Status` - Editable, user can modify
- `StatusMessage` / `IsStatusError` - User feedback

**Commands:**
- `LoadCurrentCommand` - Reloads values from Revit document
- `SaveCommand` - Saves changes to Revit document
- `CloseCommand` - Closes the window

```csharp
public MainViewModel(IProjectInfoService projectInfoService, Action closeAction)
{
    _projectInfoService = projectInfoService;
    _closeAction = closeAction;

    // Initialize commands
    LoadCurrentCommand = new RelayCommand(ExecuteLoadCurrent, CanExecuteLoadCurrent);
    SaveCommand = new RelayCommand(ExecuteSave, CanExecuteSave);
    CloseCommand = new RelayCommand(ExecuteClose);

    // Load initial values
    ExecuteLoadCurrent(null);
}
```

### Service Layer

#### `src/Services/IProjectInfoService.cs` & `ProjectInfoService.cs`

**Purpose:** Abstracts Revit API interactions for testability.

**Methods:**
- `LoadProjectInfo()` - Reads from `Document.ProjectInformation`
- `SaveProjectInfo(model)` - Writes using a Revit `Transaction`
- `IsDocumentAvailable()` - Checks if document is valid

```csharp
public bool SaveProjectInfo(ProjectInfoModel model)
{
    using (Transaction transaction = new Transaction(_document, "Update Project Information"))
    {
        transaction.Start();
        ProjectInfo projectInfo = _document.ProjectInformation;

        SetParameterValue(projectInfo, BuiltInParameter.PROJECT_NAME, model.ProjectName);
        // ... other parameters

        transaction.Commit();
        return true;
    }
}
```

### View Layer

#### `src/UI/MainWindow.xaml` & `MainWindow.xaml.cs`

**Purpose:** WPF window with data-bound UI.

**Key Features:**
- Thin code-behind (MVVM pattern)
- All controls bound to ViewModel properties
- Commands bound to buttons
- Styled with custom `ResourceDictionary`

```xml
<!-- Example data binding -->
<TextBox Text="{Binding NewProjectName, UpdateSourceTrigger=PropertyChanged}"/>
<Button Content="Save" Command="{Binding SaveCommand}"/>
```

---

## Velopack Integration Deep Dive

### What is Velopack?

[Velopack](https://velopack.io/) is a modern, cross-platform auto-update framework for desktop applications. It creates delta updates, manages application lifecycle, and integrates with various update sources including **GitHub Releases**.

### Why Velopack for Revit Add-ins?

**Challenge:** Revit add-ins are loaded as DLLs into the Revit process. You cannot update a DLL while it's loaded (file is locked).

**Solution:** Velopack manages a separate **Launcher/Updater** application that:
1. Checks for updates while Revit is running
2. Downloads updates in the background
3. Applies updates when Revit is closed
4. Deploys files to the Revit add-in folder

### Velopack Components in This Project

#### 1. `velopack.json` - Configuration File

```json
{
  "$schema": "https://raw.githubusercontent.com/velopack/velopack/master/src/Velopack.Build/velopack.schema.json",
  "appId": "ModelInfoUpdater",
  "title": "Model Info Updater",
  "version": "1.0.0",
  "authors": "Modena AEC",
  "mainExe": "ModelInfoUpdater.Updater.exe",
  "channel": "stable",
  "runtime": "net8.0-x64-desktop",
  "packDirectory": "./build-output/net8.0-windows",
  "outputDirectory": "./releases/velopack"
}
```

**Key Settings:**
- `appId` - Unique identifier for the application
- `mainExe` - The launcher executable (not the add-in DLL!)
- `runtime` - Target .NET runtime
- `packDirectory` - Where built files are located
- `outputDirectory` - Where Velopack packages are created

#### 2. `src/Services/VelopackUpdateService.cs` - Update Logic

This service handles all update-related operations within the Revit add-in.

**Initialization:**
```csharp
public VelopackUpdateService(string githubRepoUrl, string? accessToken = null)
{
    // Create GitHub source - Velopack will query releases
    var source = new GithubSource(_githubRepoUrl, accessToken, prerelease: false);
    _updateManager = new UpdateManager(source);
}
```

**Checking for Updates:**
```csharp
public async Task<bool> CheckForUpdatesAsync()
{
    // Primary: Use Velopack's built-in check
    _updateInfo = await _updateManager.CheckForUpdatesAsync();

    if (_updateInfo?.TargetFullRelease != null)
    {
        // Compare versions manually (add-in wasn't installed via Velopack)
        if (IsNewerVersion(latestVersion, CurrentVersion))
            return true;
    }

    // Fallback: HTTP-based check using GitHub API directly
    return await CheckForUpdatesViaHttpAsync();
}
```

**Why Two Methods?**
- Velopack's `CheckForUpdatesAsync()` works best when the app was installed via Velopack
- For manually-installed Revit add-ins, we also have an HTTP fallback that directly queries GitHub's API

**Launching the Updater:**
```csharp
public bool LaunchUpdater(bool silent = false)
{
    string? launcherPath = FindLauncherPath();
    // Looks in: %LocalAppData%\ModelInfoUpdater\current\
    // Fallback: same directory as add-in DLL

    var startInfo = new ProcessStartInfo
    {
        FileName = launcherPath,
        Arguments = "--update",
        UseShellExecute = true
    };
    Process.Start(startInfo);
}
```

#### 3. `src/Updater/Program.cs` - Standalone Updater

This is a separate console application that handles the actual update process.

**Key Flow:**
```csharp
static async Task<int> Main(string[] args)
{
    // 1. Initialize Velopack - handles pending updates
    VelopackApp.Build().Run();

    if (updateMode)
    {
        // 2. Check and apply updates
        var updated = await CheckAndApplyUpdatesAsync(silent);
    }

    // 3. Always deploy to Revit add-in folders
    DeployToRevit(silent);
}
```

**Multi-Version Deployment:**
```csharp
private static readonly Dictionary<string, string> RevitFrameworkMap = new()
{
    { "2024", "net48" },
    { "2025", "net48" },
    { "2026", "net8.0-windows" }
};

private static void DeployToRevit(bool silent)
{
    foreach (var kvp in RevitFrameworkMap)
    {
        string revitAddinRoot = Path.Combine(programData, "Autodesk", "Revit", "Addins", revitVersion);

        // Copy appropriate framework files
        string[] filesToCopy = framework == "net8.0-windows" ? net8Files : net48Files;
        foreach (var file in filesToCopy)
        {
            File.Copy(source, dest, overwrite: true);
        }

        // Generate .addin manifest from template
        string template = File.ReadAllText(templatePath);
        string addinContent = template.Replace("{APPDIR}", addinDir);
        File.WriteAllText(addinPath, addinContent);
    }
}
```

---

## GitHub Releases Integration

### How Velopack Uses GitHub Releases

Velopack is designed to work seamlessly with GitHub Releases as an update source.

### Release Structure

When you create a GitHub Release, attach these files:

```
Release v1.2.1
├── releases.win.json           # Velopack metadata (auto-generated by vpk)
├── ModelInfoUpdater-1.2.1-full.nupkg    # Full package
├── ModelInfoUpdater-1.2.1-delta.nupkg   # Delta update (smaller)
├── ModelInfoUpdater-win-Setup.exe       # Initial installer
└── ModelInfoUpdater-win-Portable.zip    # Manual install ZIP
```

### `releases.win.json` Structure

This file tells Velopack what updates are available:

```json
{
  "Releases": [
    {
      "Version": "1.2.1",
      "FileName": "ModelInfoUpdater-1.2.1-full.nupkg",
      "SHA1": "abc123...",
      "FileSize": 1234567
    }
  ]
}
```

### Querying GitHub Releases

The `VelopackUpdateService` uses `GithubSource`:

```csharp
var source = new GithubSource(
    "https://github.com/givenxmodena/ModelInfoUpdater",  // Repository URL
    accessToken,  // null for public repos, token for private
    prerelease: false  // Ignore pre-release versions
);
var updateManager = new UpdateManager(source);
```

**For Public Repositories:** No authentication needed. GitHub allows unauthenticated read access to releases.

**For Private Repositories:** Provide a GitHub Personal Access Token with `repo` scope:
```csharp
var source = new GithubSource(repoUrl, "ghp_xxxxxxxxxxxxx", false);
```

### Creating a Release

1. **Build the project:**
   ```powershell
   .\scripts\Build-Release.ps1 -Version "1.2.1"
   ```

2. **Create Velopack packages:**
   ```powershell
   vpk pack `
       --packId ModelInfoUpdater `
       --packVersion 1.2.1 `
       --packDir ./build-output/net8.0-windows `
       --mainExe ModelInfoUpdater.Updater.exe `
       --outputDir ./releases/velopack
   ```

3. **Upload to GitHub:**
   - Go to repository → Releases → Create new release
   - Tag: `v1.2.1`
   - Upload all files from `releases/velopack/`
   - Publish

---

## Data Flow

### Application Startup Flow

```
┌──────────────────────────────────────────────────────────────────┐
│ 1. Revit starts                                                  │
│    └──> Loads add-ins from %APPDATA%\Autodesk\Revit\Addins\XXXX │
└──────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌──────────────────────────────────────────────────────────────────┐
│ 2. App.OnStartup() called                                        │
│    ├──> Creates ribbon tab "TESTER"                              │
│    ├──> Creates panel "Tools"                                    │
│    ├──> Adds "Model Info Updater" button                         │
│    └──> InitializeUpdateServiceAsync() [async, non-blocking]     │
│         └──> Creates VelopackUpdateService                       │
│         └──> CheckForUpdatesAsync() [background]                 │
└──────────────────────────────────────────────────────────────────┘
```

### Button Click Flow (User Interaction)

```
┌──────────────────────────────────────────────────────────────────┐
│ 1. User clicks "Model Info Updater" button                       │
└──────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌──────────────────────────────────────────────────────────────────┐
│ 2. Command.Execute() runs                                        │
│    ├──> Gets active document                                     │
│    ├──> Calls App.ShowUpdateNotificationIfAvailableAsync()       │
│    │    (Shows dialog if update available)                       │
│    └──> Creates MainWindow(document)                             │
└──────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌──────────────────────────────────────────────────────────────────┐
│ 3. MainWindow constructor                                        │
│    ├──> Creates ProjectInfoService(document)                     │
│    ├──> Creates MainViewModel(service, closeAction)              │
│    └──> Sets DataContext = viewModel                             │
└──────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌──────────────────────────────────────────────────────────────────┐
│ 4. MainViewModel constructor                                     │
│    └──> Calls ExecuteLoadCurrent()                               │
│         └──> projectInfoService.LoadProjectInfo()                │
│              └──> Reads from Document.ProjectInformation         │
│              └──> Returns ProjectInfoModel                       │
│         └──> Populates Current and New properties                │
└──────────────────────────────────────────────────────────────────┘
```

### Save Flow (Data Persistence)

```
┌──────────────────────────────────────────────────────────────────┐
│ 1. User edits fields and clicks "Save"                           │
└──────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌──────────────────────────────────────────────────────────────────┐
│ 2. SaveCommand executes ExecuteSave()                            │
│    └──> Creates ProjectInfoModel from New* properties            │
│    └──> Calls projectInfoService.SaveProjectInfo(model)          │
└──────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌──────────────────────────────────────────────────────────────────┐
│ 3. ProjectInfoService.SaveProjectInfo()                          │
│    └──> Creates Transaction("Update Project Information")        │
│    └──> transaction.Start()                                      │
│    └──> Sets parameters on Document.ProjectInformation           │
│    └──> transaction.Commit()                                     │
└──────────────────────────────────────────────────────────────────┘
```

### Update Flow (Velopack)

```
┌─────────────────────────────────────────────────────────────────────┐
│ 1. App startup (background)                                         │
│    └──> VelopackUpdateService.CheckForUpdatesAsync()                │
│         ├──> UpdateManager.CheckForUpdatesAsync()                   │
│         │    └──> Queries: github.com/owner/repo/releases           │
│         │    └──> Parses releases.win.json                          │
│         └──> [Fallback] HTTP GET api.github.com/.../releases/latest │
└─────────────────────────────────────────────────────────────────────┘
                              │
                  (If update available)
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 2. User clicks button → Update notification shown                   │
│    └──> "Update available: v1.2.1. Would you like to update?"       │
│    └──> User clicks "Yes"                                           │
└─────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 3. VelopackUpdateService.LaunchUpdater()                            │
│    └──> Finds ModelInfoUpdater.Updater.exe                          │
│    └──> Process.Start(updater, "--update")                          │
└─────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 4. Updater.exe runs (separate process)                              │
│    ├──> VelopackApp.Build().Run()                                   │
│    ├──> CheckAndApplyUpdatesAsync()                                 │
│    │    ├──> UpdateManager.DownloadUpdatesAsync()                   │
│    │    └──> UpdateManager.ApplyUpdatesAndRestart()                 │
│    └──> DeployToRevit() - copies files to add-in folders            │
└─────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 5. User closes Revit → Next startup loads new version               │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Build and Release Process

### Prerequisites

1. **.NET SDK 8.0** - For building .NET 8 target
2. **.NET Framework 4.8 SDK** - For building .NET 4.8 target
3. **Velopack CLI** - Install with: `dotnet tool install -g vpk`
4. **GitHub CLI** (optional) - For automated release publishing

### Build Script: `scripts/Build-Release.ps1`

```powershell
# Usage:
.\Build-Release.ps1 -Version "1.2.1"
.\Build-Release.ps1 -Version "1.2.1" -PublishToGitHub
```

**Steps performed:**

1. **Clean and Build:**
   ```powershell
   dotnet clean ModelInfoUpdater.csproj --configuration Release
   dotnet build ModelInfoUpdater.csproj --configuration Release
   dotnet build Updater/ModelInfoUpdater.Updater.csproj --configuration Release
   ```

2. **Create Velopack Package:**
   ```powershell
   vpk pack `
       --packId ModelInfoUpdater `
       --packVersion $Version `
       --packDir ./build-output/net8.0-windows `
       --mainExe "ModelInfoUpdater.Updater.exe" `
       --outputDir ./releases/velopack `
       --framework "net8.0-x64-desktop"
   ```

3. **Create ZIP Packages:**
   ```powershell
   # For Revit 2026 (.NET 8)
   Compress-Archive -Path @(
       "ModelInfoUpdater.dll",
       "ModelInfoUpdater.Updater.exe",
       "Velopack.dll"
   ) -DestinationPath "ModelInfoUpdater-1.2.1-Revit2026.zip"

   # For Revit 2024 (.NET 4.8)
   Compress-Archive -Path @(
       "ModelInfoUpdater.dll",
       "Velopack.dll"
   ) -DestinationPath "ModelInfoUpdater-1.2.1-Revit2024.zip"
   ```

4. **Publish to GitHub (optional):**
   ```powershell
   gh release create "v$Version" $Zip2026 $Zip2024 --repo owner/repo
   ```

---

## Multi-Framework Support

### Why Multi-Targeting?

| Revit Version | .NET Framework |
|---------------|----------------|
| Revit 2024 | .NET Framework 4.8 |
| Revit 2025 | .NET Framework 4.8 |
| Revit 2026 | .NET 8.0 |

### Project Configuration: `ModelInfoUpdater.csproj`

```xml
<PropertyGroup>
  <TargetFrameworks>net8.0-windows;net48</TargetFrameworks>
  <UseWPF>true</UseWPF>
</PropertyGroup>

<!-- Revit 2026 (.NET 8) -->
<PropertyGroup Condition="'$(TargetFramework)' == 'net8.0-windows'">
  <DefineConstants>$(DefineConstants);REVIT2026</DefineConstants>
</PropertyGroup>

<!-- Revit 2024 (.NET 4.8) -->
<PropertyGroup Condition="'$(TargetFramework)' == 'net48'">
  <DefineConstants>$(DefineConstants);REVIT2024</DefineConstants>
</PropertyGroup>

<!-- Conditional Revit API References -->
<ItemGroup Condition="'$(TargetFramework)' == 'net8.0-windows'">
  <Reference Include="RevitAPI">
    <HintPath>C:\Program Files\Autodesk\Revit 2026\RevitAPI.dll</HintPath>
  </Reference>
</ItemGroup>

<ItemGroup Condition="'$(TargetFramework)' == 'net48'">
  <Reference Include="RevitAPI">
    <HintPath>C:\Program Files\Autodesk\Revit 2024\RevitAPI.dll</HintPath>
  </Reference>
</ItemGroup>
```

### Build Output Structure

```
build-output/
├── net8.0-windows/                 # Revit 2026
│   ├── ModelInfoUpdater.dll
│   ├── ModelInfoUpdater.deps.json
│   ├── ModelInfoUpdater.Updater.exe
│   ├── Velopack.dll
│   └── net48/                      # Copied by Updater.csproj post-build
│       └── ModelInfoUpdater.dll
│
└── net48/                          # Revit 2024/2025
    ├── ModelInfoUpdater.dll
    └── Velopack.dll
```

### Conditional Compilation

Use preprocessor directives for version-specific code:

```csharp
#if REVIT2026
    // .NET 8 specific code
    using var httpClient = new HttpClient();
    response = await httpClient.GetStringAsync(apiUrl);
#else
    // .NET Framework 4.8 code
    using (var webClient = new WebClient())
    {
        response = await Task.Run(() => webClient.DownloadString(apiUrl));
    }
#endif
```

---

## Deployment Workflow

### Initial Installation (First Time)

**Option A: Inno Setup Installer**

```
installer/ModelInfoUpdater.iss → ModelInfoUpdater-Setup-1.0.0.exe
```

The installer:
1. Detects installed Revit versions
2. Copies appropriate DLLs to `%APPDATA%\Autodesk\Revit\Addins\{version}\`
3. Creates the `.addin` manifest file

**Option B: Manual Installation**

1. Copy files to Revit add-in folder:
   ```
   %APPDATA%\Autodesk\Revit\Addins\2026\
   ├── ModelInfoUpdater.dll
   ├── ModelInfoUpdater.Updater.exe
   ├── Velopack.dll
   └── ModelInfoUpdater.addin
   ```

2. Edit `.addin` to point to correct DLL path

### Subsequent Updates (Velopack)

After initial installation, updates are automatic:

1. Add-in checks GitHub Releases on Revit startup
2. User is notified when update is available
3. Updater downloads and applies the update
4. Files are deployed to all Revit add-in folders

### `.addin` Manifest Files

**Development (`ModelInfoUpdater.addin`):**
```xml
<RevitAddIns>
  <AddIn Type="Application">
    <AddInId>A1B2C3D4-E5F6-7890-ABCD-EF1234567890</AddInId>
    <Assembly>ModelInfoUpdater.dll</Assembly>
    <FullClassName>ModelInfoUpdater.App</FullClassName>
    <Name>Model Info Updater</Name>
    <VendorId>DEVELOPER</VendorId>
  </AddIn>
</RevitAddIns>
```

**Deployed (`ModelInfoUpdater.addin.template` → generated):**
```xml
<RevitAddIns>
  <AddIn Type="Application">
    <Assembly>{APPDIR}\ModelInfoUpdater.dll</Assembly>
    <!-- {APPDIR} is replaced with actual path during deployment -->
  </AddIn>
</RevitAddIns>
```

---

## Troubleshooting

### Common Issues

#### 1. "Update check failed: 404 Not Found"

**Cause:** GitHub repository is private or doesn't have releases.

**Solution:**
- Make repository public, OR
- Provide a GitHub Personal Access Token:
  ```csharp
  new VelopackUpdateService(repoUrl, "ghp_your_token_here");
  ```

#### 2. "Launcher not found"

**Cause:** `ModelInfoUpdater.Updater.exe` is not in the expected location.

**Solution:**
- Check `%LocalAppData%\ModelInfoUpdater\current\`
- Or ensure `.exe` is alongside the `.dll` in add-in folder

#### 3. Add-in doesn't load in Revit

**Causes & Solutions:**
- Wrong .NET version DLL → Use correct folder (`net8.0-windows` for 2026, `net48` for 2024)
- `.addin` path incorrect → Verify `<Assembly>` path points to actual DLL
- Missing dependencies → Ensure `Velopack.dll` is copied

#### 4. Update doesn't apply

**Cause:** DLL is locked while Revit is running.

**Solution:**
- Close Revit completely
- Run `ModelInfoUpdater.Updater.exe --update` manually
- Restart Revit

### Debug Logging

The application outputs debug messages visible in Visual Studio's Output window:

```csharp
System.Diagnostics.Debug.WriteLine($"[ModelInfoUpdater] Message");
System.Diagnostics.Debug.WriteLine($"[VelopackUpdateService] Message");
```

**View logs:**
1. Debug → Windows → Output (in Visual Studio)
2. Or use [DebugView](https://learn.microsoft.com/en-us/sysinternals/downloads/debugview) for runtime logs

### Version Information

- **Check current version:** Look at `src/Properties/AssemblyInfo.cs`
  ```csharp
  [assembly: AssemblyVersion("1.2.1.0")]
  ```

- **Update version:** Change in:
  1. `AssemblyInfo.cs` - `AssemblyVersion` and `AssemblyFileVersion`
  2. `MainWindow.xaml` - Window title
  3. `velopack.json` - `version` field

---

## Summary

This project demonstrates a complete Revit add-in with:

| Feature | Implementation |
|---------|----------------|
| **UI Framework** | WPF with MVVM pattern |
| **Multi-targeting** | .NET 8.0 (Revit 2026) + .NET 4.8 (Revit 2024) |
| **Auto-updates** | Velopack with GitHub Releases |
| **Update delivery** | Separate Updater.exe process |
| **Installation** | Inno Setup installer or manual |
| **Architecture** | Clean separation: Models, Views, ViewModels, Services |

### Key Takeaways for Developers

1. **Velopack can't update locked DLLs** - Use a separate Updater process
2. **GitHub Releases work great** for free, public update hosting
3. **Multi-targeting requires conditional compilation** for framework-specific code
4. **MVVM keeps code testable** and maintainable
5. **Always test both .NET versions** before releasing

---

*Documentation generated for Model Info Updater v1.2.1*

