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

### Transfer Standards
Browse and inspect standards from any open Revit document:

- Select a **source project** from all currently open documents
- Filter by **category**: Line Styles, Fill Patterns, Text Types, Dimension Types, Materials, Object Styles, Filters, View Templates
- Items displayed in a searchable DataGrid with checkbox selection
- **Select All / Clear All** helpers

---

## UI
- Dark / light theme toggle (persisted between sessions)
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
├── App.cs                        # IExternalApplication — ribbon registration
├── Commands/
│   └── ProjectSetupCommand.cs    # IExternalCommand — opens main window
├── Models/                       # DTOs (SettingsModel, StandardsItemDto, …)
├── Services/
│   ├── Core/                     # SessionLogger, ILogger
│   ├── Revit/                    # External event requests (API calls)
│   └── SettingsService.cs        # JSON settings persistence
└── UI/
    ├── Themes/                   # DarkTheme, LightTheme, ElementStyles
    ├── ViewModels/               # MainViewModel, TransferStandardsViewModel
    ├── ProjectSetupWindow.xaml   # Main three-tab window
    ├── TransferStandardsWindow.xaml
    └── TitleBar.xaml             # Custom title bar user control
```

---

## License

Private / internal use. All rights reserved.
