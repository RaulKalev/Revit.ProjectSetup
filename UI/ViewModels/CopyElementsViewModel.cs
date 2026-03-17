using ProjectSetup.Models;
using ProjectSetup.Services.Revit;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace ProjectSetup.UI.ViewModels
{
    public class CopyElementsViewModel : BaseViewModel
    {
        private readonly RevitExternalEventService _eventService;

        // ── Backing stores ────────────────────────────────────────────────────
        private readonly List<FamilyItemDto> _allItems = new List<FamilyItemDto>();
        private bool _isDarkMode = true;

        private string _selectedProject;
        private string _selectedCategory;
        private string _searchText;
        private bool   _isLoading;
        private string _statusMessage = "Select a source project to browse its families.";
        private int    _selectedCount;

        // ── Public collections ────────────────────────────────────────────────
        public ObservableCollection<string>      SourceProjects { get; } = new ObservableCollection<string>();
        public ObservableCollection<string>      AllCategories  { get; } = new ObservableCollection<string>();
        public ObservableCollection<FamilyItemDto> FilteredItems{ get; } = new ObservableCollection<FamilyItemDto>();

        // ── Properties ────────────────────────────────────────────────────────
        public string SelectedProject
        {
            get => _selectedProject;
            set
            {
                if (SetProperty(ref _selectedProject, value) && value != null)
                    LoadFamilies(value);
            }
        }

        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value))
                    ApplyFilter();
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    ApplyFilter();
                    OnPropertyChanged(nameof(HasSearchText));
                }
            }
        }

        public bool HasSearchText => !string.IsNullOrEmpty(_searchText);

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public int SelectedCount
        {
            get => _selectedCount;
            private set
            {
                if (SetProperty(ref _selectedCount, value))
                    OnPropertyChanged(nameof(ImportButtonLabel));
            }
        }

        public string DisplayCount  => $"Showing: {FilteredItems.Count} / {_allItems.Count}";
        public string ImportButtonLabel => _selectedCount > 0 ? $"Import ({_selectedCount})" : "Import";

        /// <summary>True = all filtered selected, False = none, Null = mixed (indeterminate).</summary>
        public bool? AllSelected
        {
            get
            {
                if (FilteredItems.Count == 0) return false;
                bool all  = FilteredItems.All(x => x.IsSelected);
                bool none = FilteredItems.All(x => !x.IsSelected);
                if (all)  return true;
                if (none) return false;
                return null;
            }
            set
            {
                if (value.HasValue) SetAllSelected(value.Value);
                OnPropertyChanged();
            }
        }

        // ── Commands ──────────────────────────────────────────────────────────
        public ICommand RefreshProjectsCommand { get; }
        public ICommand SelectAllCommand        { get; }
        public ICommand DeselectAllCommand      { get; }
        public ICommand ImportCommand           { get; }
        public ICommand ClearSearchCommand      { get; }
        /// <summary>Wired by the code-behind to bring the window back to the front after import.</summary>
        public Action OnImportComplete { get; set; }

        /// <summary>Wired by the code-behind to supply the owner Window for dialogs.</summary>
        public Func<System.Windows.Window> GetOwnerWindow { get; set; }

        public CopyElementsViewModel(RevitExternalEventService eventService, bool isDarkMode = true)
        {
            _eventService = eventService;
            _isDarkMode   = isDarkMode;

            RefreshProjectsCommand = new RelayCommand(_ => LoadProjects());
            SelectAllCommand       = new RelayCommand(_ => SetAllSelected(true),  _ => FilteredItems.Count > 0);
            DeselectAllCommand     = new RelayCommand(_ => SetAllSelected(false), _ => _selectedCount > 0);
            ClearSearchCommand     = new RelayCommand(_ => SearchText = null,     _ => !string.IsNullOrEmpty(_searchText));
            ImportCommand          = new RelayCommand(_ => ImportSelected(),        _ => _selectedCount > 0);

            LoadProjects();
        }

        // ── Source project loading ─────────────────────────────────────────────
        private void LoadProjects()
        {
            StatusMessage = "Fetching open documents…";
            _eventService.Raise(new GetOtherDocumentsRequest(docs =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var prev = _selectedProject;
                    SourceProjects.Clear();
                    foreach (var t in docs) SourceProjects.Add(t);

                    if (prev != null && SourceProjects.Contains(prev))
                        SelectedProject = prev;
                    else if (SourceProjects.Count > 0)
                        SelectedProject = SourceProjects[0];
                    else
                    {
                        _allItems.Clear();
                        AllCategories.Clear();
                        FilteredItems.Clear();
                        UpdateCounts();
                        StatusMessage = "No other open projects found. Open a source project in Revit first.";
                    }
                });
            }));
        }

        // ── Family loading ────────────────────────────────────────────────────
        private void LoadFamilies(string sourceTitle)
        {
            _allItems.Clear();
            AllCategories.Clear();
            FilteredItems.Clear();
            SelectedCount = 0;
            IsLoading     = true;
            StatusMessage = $"Loading families from '{sourceTitle}'…";

            _eventService.Raise(new GetFamiliesRequest(sourceTitle, families =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var f in families)
                    {
                        f.PropertyChanged += OnItemPropertyChanged;
                        _allItems.Add(f);
                    }

                    // Rebuild category list
                    AllCategories.Clear();
                    AllCategories.Add("All");
                    foreach (var cat in _allItems.Select(f => f.Category).Distinct().OrderBy(c => c))
                        AllCategories.Add(cat);

                    // Default to "All"
                    _selectedCategory = "All";
                    OnPropertyChanged(nameof(SelectedCategory));

                    IsLoading = false;
                    ApplyFilter();

                    StatusMessage = _allItems.Count > 0
                        ? $"{_allItems.Count} famil{(_allItems.Count == 1 ? "y" : "ies")} found in '{sourceTitle}'."
                        : $"No families found in '{sourceTitle}'.";
                });
            }));
        }

        // ── Filter ────────────────────────────────────────────────────────────
        private void ApplyFilter()
        {
            var category = string.IsNullOrEmpty(_selectedCategory) || _selectedCategory == "All"
                           ? null : _selectedCategory;
            var search   = _searchText?.Trim().ToLowerInvariant();

            FilteredItems.Clear();
            foreach (var item in _allItems)
            {
                if (category != null && item.Category != category) continue;
                if (!string.IsNullOrEmpty(search)
                    && !item.Name.ToLowerInvariant().Contains(search)
                    && !item.Category.ToLowerInvariant().Contains(search)) continue;
                FilteredItems.Add(item);
            }
            UpdateCounts();
        }

        // ── Selection helpers ─────────────────────────────────────────────────
        private void OnItemPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FamilyItemDto.IsSelected))
                UpdateCounts();
        }

        private void SetAllSelected(bool selected)
        {
            // Only affects visible (filtered) items
            foreach (var item in FilteredItems)
                item.IsSelected = selected;
            // UpdateCounts is called reactively via PropertyChanged hooks
        }

        private void UpdateCounts()
        {
            SelectedCount = _allItems.Count(x => x.IsSelected);
            OnPropertyChanged(nameof(DisplayCount));
            OnPropertyChanged(nameof(AllSelected));
        }

        // ── Import ────────────────────────────────────────────────────────────
        private void ImportSelected()
        {
            var selectedItems = _allItems.Where(x => x.IsSelected).ToList();
            if (selectedItems.Count == 0) return;

            var toImport = selectedItems.Select(x => x.UniqueId).ToList();

            IsLoading     = true;
            StatusMessage = "Checking for existing families…";

            // Step 1: check which names already exist in the target doc
            _eventService.Raise(new CheckFamiliesRequest(_selectedProject, toImport, duplicateNames =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsLoading = false;

                    List<string> finalIds = toImport;

                    if (duplicateNames.Count > 0)
                    {
                        var dupList = selectedItems
                            .Where(x => duplicateNames.Contains(x.Name))
                            .Select(x => $"•  {x.Name}")
                            .ToList();

                        var answer = DialogWindow.Show(
                            owner       : GetOwnerWindow?.Invoke(),
                            title       : "Families Already Exist",
                            message     : $"{duplicateNames.Count} of the selected famil{(duplicateNames.Count == 1 ? "y" : "ies")} {(duplicateNames.Count == 1 ? "already exists" : "already exist")} in the active document.",
                            buttons     : new List<DialogButton>
                            {
                                new DialogButton("Overwrite All", "overwrite",  isDefault: false),
                                new DialogButton("Skip Existing", "skip",       isDefault: true),
                                new DialogButton("Cancel",        "cancel",     isCancel:  true),
                            },
                            iconKind    : "AlertCircleOutline",
                            iconColor   : "#f0a040",
                            detailItems : dupList,
                            isDarkMode  : _isDarkMode);

                        if (answer == "cancel" || answer == null)
                        {
                            StatusMessage = "Import cancelled.";
                            return;
                        }

                        if (answer == "skip")
                        {
                            finalIds = selectedItems
                                .Where(x => !duplicateNames.Contains(x.Name))
                                .Select(x => x.UniqueId)
                                .ToList();

                            if (finalIds.Count == 0)
                            {
                                StatusMessage = $"All {duplicateNames.Count} selected famil{(duplicateNames.Count == 1 ? "y" : "ies")} already exist — nothing imported.";
                                return;
                            }
                        }
                    }

                    // Step 2: perform the import
                    IsLoading     = true;
                    StatusMessage = $"Importing {finalIds.Count} famil{(finalIds.Count == 1 ? "y" : "ies")}…";

                    _eventService.Raise(new CopyFamiliesRequest(_selectedProject, finalIds, importResult =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            IsLoading = false;

                            if (importResult.ErrorMessage != null)
                            {
                                StatusMessage = importResult.ErrorMessage;
                                OnImportComplete?.Invoke();
                                return;
                            }

                            // Update status bar
                            var parts = new List<string>();
                            if (importResult.Imported.Count > 0) parts.Add($"Loaded {importResult.Imported.Count}");
                            if (importResult.Skipped.Count  > 0) parts.Add($"{importResult.Skipped.Count} skipped");
                            if (importResult.Failed.Count   > 0) parts.Add($"{importResult.Failed.Count} failed");
                            StatusMessage = parts.Count > 0 ? string.Join(", ", parts) + "." : "Nothing imported.";

                            // Step 3: summary popup
                            var detailLines = new List<string>();
                            foreach (var n in importResult.Imported) detailLines.Add($"✓  {n}");
                            foreach (var n in importResult.Skipped)  detailLines.Add($"–  {n}  (skipped)");
                            foreach (var n in importResult.Failed)   detailLines.Add($"✗  {n}  (failed)");

                            string summaryMsg = (importResult.Imported.Count > 0 && importResult.Skipped.Count == 0 && importResult.Failed.Count == 0)
                                ? $"Successfully loaded {importResult.Imported.Count} famil{(importResult.Imported.Count == 1 ? "y" : "ies")} into the active document."
                                : "Import complete. See details below.";

                            DialogWindow.Show(
                                owner       : GetOwnerWindow?.Invoke(),
                                title       : "Import Complete",
                                message     : summaryMsg,
                                buttons     : new List<DialogButton> { new DialogButton("OK", "ok", isDefault: true) },
                                iconKind    : importResult.Failed.Count > 0 ? "AlertCircleOutline" : "CheckCircleOutline",
                                iconColor   : importResult.Failed.Count > 0 ? "#f0a040" : "#70babc",
                                detailItems : detailLines.Count > 0 ? detailLines : null,
                                isDarkMode  : _isDarkMode);

                            OnImportComplete?.Invoke();
                        });
                    }));
                });
            }));
        }
    }
}
