# Revit.ProjectSetup

A Revit plugin that provides a centralised hub for setting up, maintaining, and transferring standards across Revit projects. Built with WPF + MaterialDesign, targeting Revit 2024 (net48) and Revit 2026 (net8.0-windows).

---

## Features

### Project Setup
| Action | Description |
|--------|-------------|
| **Project Information** | Opens the Revit Project Information dialog |
| **Browser Organization** | Applies the standard browser organisation scheme *(coming soon)* |
| **Required Content Check** | Verifies required families are loaded *(coming soon)* |

### Maintenance
| Action | Description |
|--------|-------------|
| **Purge Unused** | Opens Revit's Purge Unused dialog |
| **Model Warnings** | Counts and summarises all current model warnings |
| **Model Audit** | Pre-defined model quality checklist *(coming soon)* |

### Link IFC Files
Batch-link external IFC models into the active Revit project:

- Pick a folder containing `.ifc` files
- Preview all discovered files in a DataGrid with file names and sizes
- **Select All / Clear All** helpers with checkbox selection
- Files are linked one-by-one via `RevitLinkType.CreateFromIFC`
- Each linked model is automatically **pinned** after placement
- Summary dialog reports success / failure for every file

### Link DWG Files
Batch-link DWG drawings into the active Revit project:

- Pick a folder containing `.dwg` files
- Preview all discovered files with checkbox selection
- Links placed on the current view; Revit link V/G overrides hidden after linking

### Create Levels & Base Views
Two-step workflow in a single dialog:

- Define levels with name, elevation, and floor-plan creation toggle
- Base views (floor plans, reflected ceiling plans, sections) are created automatically after levels are confirmed
- Window closes automatically on successful apply

### Create Plan Sets
Batch-create sheet plan sets per discipline category:

- Select EL (Elektripaigaldis) and/or EN (Nõrkvool) categories
- Collapsible group cards per discipline
- Creates floor-plan sheets with pre-configured view templates

### Paigalda Reeper (Place Benchmark)
Locate and place a benchmark (`xx_REEPER_v01`) aligned to the reeper in a linked model:

- Searches **all loaded linked models** for generic model elements (including DirectShapes) whose name or family name contains "reeper"
- Results shown in a DataGrid: linked model name, element name/type, and `X,Y,Z` size
- Select **EL** or **EN** discipline — places `EL_REEPER_v01` or `EN_REEPER_v01` type
- Placed at the exact world corner location of the source element
- `X,Y,Z` type parameter set to match the source element's size
- Shows an error dialog if the `xx_REEPER_v01` family is not loaded

### Transfer Standards
Browse and inspect standards from any open Revit document:

- Select a **source project** from all currently open documents
- Filter by **category**: Line Styles, Fill Patterns, Text Types, Dimension Types, Materials, Object Styles, Filters, View Templates
- Items displayed in a searchable DataGrid with checkbox selection
- **Select All / Clear All** helpers

---

## Step Progress Tracking

Each workflow step has a **tick button** to mark it done. Progress is persisted via Revit Extensible Storage and restored on next open. Steps also turn green automatically when the corresponding action completes successfully.

---

## UI
- Dark / light theme toggle (persisted between sessions)
- Theme-aware ribbon icon (dark and light TIFF variants)
- Custom frameless window with glass border effect and resize support
- MaterialDesignThemes 5.x styling throughout

---

## Requirements

| Target | Revit | .NET |
|--------|-------|------|
| `net48` | 2024 | .NET Framework 4.8 |
| `net8.0-windows` | 2026 | .NET 8 |

---

## Building

```bash
# Both targets
dotnet build

# Release
dotnet build --configuration Release

# Single target
dotnet build -f net48
dotnet build -f net8.0-windows
```

The Release build automatically code-signs the output assembly if `signtool.exe` and the certificate are present.

---

## Installation

1. Build the project in **Release** configuration.
2. Copy the output `.dll` and the `.addin` manifest to your Revit addins folder:
   - Per-user: `%APPDATA%\Autodesk\Revit\Addins\<year>\`
   - All users: `%PROGRAMDATA%\Autodesk\Revit\Addins\<year>\`
3. Restart Revit. The **Project Setup** button appears on the **RK Tools → Project** ribbon panel.

---

## Project Structure

```
ProjectSetup/
├── App.cs                            # IExternalApplication — ribbon registration
├── Commands/
│   └── ProjectSetupCommand.cs        # IExternalCommand — opens main window
├── Models/                           # DTOs (SettingsModel, ReeperItemDto, …)
├── Services/
│   ├── Core/                         # SessionLogger, ILogger
│   ├── Revit/                        # External event requests
│   │   ├── FindReeperInLinksRequest.cs
│   │   ├── PlaceReeperRequest.cs
│   │   ├── LinkDwgFilesRequest.cs
│   │   ├── CreatePlanSetsRequest.cs
│   │   └── …
│   └── SettingsService.cs            # JSON settings persistence
└── UI/
    ├── Themes/                       # DarkTheme, LightTheme, ElementStyles
    ├── ViewModels/                   # MainViewModel, PlaceReeperViewModel, …
    ├── ProjectSetupWindow.xaml       # Main window
    ├── PlaceReeperWindow.xaml        # Reeper search & placement window
    ├── CreateLevelsWindow.xaml       # Level + base view creation window
    ├── CreatePlanSetsWindow.xaml     # Plan set creation window
    ├── LinkDwgWindow.xaml            # DWG linking window
    ├── LinkIfcWindow.xaml            # IFC linking window
    ├── TransferStandardsWindow.xaml
    └── TitleBar.xaml                 # Custom title bar user control
```

---

## License

Private / internal use. All rights reserved.
