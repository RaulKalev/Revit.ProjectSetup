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
    public class CreatePlanSetsViewModel : BaseViewModel
    {
        private readonly RevitExternalEventService _eventService;
        private readonly bool   _isDarkMode;
        private readonly System.Windows.Threading.Dispatcher _dispatcher;

        private bool   _isCreating;
        private string _statusMessage = "Vali kategooriad ja klõpsa 'Loo'.";
        private int    _selectedCount;
        private bool   _isELExpanded  = true;
        private bool   _isENExpanded  = true;

        // ── EL category items ────────────────────────────────────────────────
        public ObservableCollection<CategoryItemDto> ELItems { get; } = new ObservableCollection<CategoryItemDto>
        {
            new CategoryItemDto("Jõud",           "EL Jõud"),
            new CategoryItemDto("Valgustus",       "EL Valgus"),
            new CategoryItemDto("Kaabliredelid",   "EL Redelid"),
            new CategoryItemDto("Turvavalgustus",  "EL Turvavalgus"),
        };

        // ── EN category items ────────────────────────────────────────────────
        public ObservableCollection<CategoryItemDto> ENItems { get; } = new ObservableCollection<CategoryItemDto>
        {
            new CategoryItemDto("ATS",      "EN ATS"),
            new CategoryItemDto("Side",     "EN Side"),
            new CategoryItemDto("LPS",      "EN LPS"),
            new CategoryItemDto("SHS",      "EN SHS"),
            new CategoryItemDto("LPS/SHS",  "EN LPS/SHS"),
            new CategoryItemDto("VVS",      "EN VVS"),
            new CategoryItemDto("Valve",    "EN Valve"),
            new CategoryItemDto("Inva",     "EN Inva"),
            new CategoryItemDto("Helindus", "EN Heli"),
        };

        public bool IsELExpanded
        {
            get => _isELExpanded;
            set => SetProperty(ref _isELExpanded, value);
        }

        public bool IsENExpanded
        {
            get => _isENExpanded;
            set => SetProperty(ref _isENExpanded, value);
        }

        public bool IsCreating
        {
            get => _isCreating;
            set => SetProperty(ref _isCreating, value);
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
                    OnPropertyChanged(nameof(CreateButtonLabel));
            }
        }

        public string CreateButtonLabel => _selectedCount > 0 ? $"Loo ({_selectedCount})" : "Loo";

        // ── Delegates ────────────────────────────────────────────────────────
        public Action          OnCreateComplete { get; set; }
        public Func<Window>    GetOwnerWindow   { get; set; }

        // ── Commands ─────────────────────────────────────────────────────────
        public ICommand ToggleELCommand    { get; }
        public ICommand ToggleENCommand    { get; }
        public ICommand SelectAllELCommand { get; }
        public ICommand SelectAllENCommand { get; }
        public ICommand CreateCommand      { get; }

        public CreatePlanSetsViewModel(RevitExternalEventService eventService, bool isDarkMode = true)
        {
            _eventService = eventService;
            _isDarkMode   = isDarkMode;
            _dispatcher   = System.Windows.Threading.Dispatcher.CurrentDispatcher;

            // Subscribe to selection changes
            foreach (var item in ELItems) item.PropertyChanged += (_, _) => UpdateSelectedCount();
            foreach (var item in ENItems) item.PropertyChanged += (_, _) => UpdateSelectedCount();

            ToggleELCommand    = new RelayCommand(_ => IsELExpanded = !IsELExpanded);
            ToggleENCommand    = new RelayCommand(_ => IsENExpanded = !IsENExpanded);
            SelectAllELCommand = new RelayCommand(_ => SetAll(ELItems, true));
            SelectAllENCommand = new RelayCommand(_ => SetAll(ENItems, true));
            CreateCommand      = new RelayCommand(_ => CreateSelected(), _ => !IsCreating && SelectedCount > 0);

            UpdateSelectedCount();
        }

        private void SetAll(ObservableCollection<CategoryItemDto> items, bool selected)
        {
            foreach (var item in items) item.IsSelected = selected;
        }

        private void UpdateSelectedCount()
        {
            SelectedCount = ELItems.Count(x => x.IsSelected) + ENItems.Count(x => x.IsSelected);
        }

        private void CreateSelected()
        {
            var categories = ELItems.Concat(ENItems)
                .Where(x => x.IsSelected)
                .Select(x => new PlanSetCategory(x.Name, x.ViewTemplateName))
                .ToList();

            if (categories.Count == 0) return;

            IsCreating    = true;
            StatusMessage = "Plaanide loomine…";

            _eventService.Raise(new CreatePlanSetsRequest(categories, result =>
            {
                _dispatcher.Invoke(() =>
                {
                    IsCreating = false;

                    if (result.ErrorMessage != null)
                    {
                        StatusMessage = result.ErrorMessage;
                        DialogWindow.Show(
                            owner     : GetOwnerWindow?.Invoke(),
                            title     : "Plaanide loomine",
                            message   : result.ErrorMessage,
                            iconKind  : "AlertCircleOutline",
                            iconColor : "#f0a040",
                            isDarkMode: _isDarkMode);
                        return;
                    }

                    var parts = new System.Collections.Generic.List<string>();
                    if (result.Created > 0) parts.Add($"Loodud: {result.Created}");
                    if (result.Skipped > 0) parts.Add($"Vahele jäetud: {result.Skipped}");
                    if (result.Failed.Count > 0) parts.Add($"Vigu: {result.Failed.Count}");
                    var summary = parts.Count > 0 ? string.Join(", ", parts) + "." : "Muudatusi ei tehtud.";

                    StatusMessage = summary;

                    var details = new System.Collections.Generic.List<string>(result.Messages);
                    foreach (var (name, err) in result.Failed)
                        details.Add($"✗ {name}: {err}");

                    DialogWindow.Show(
                        owner       : GetOwnerWindow?.Invoke(),
                        title       : "Plaanide loomine",
                        message     : summary,
                        detailItems : details.Count > 0 ? details : null,
                        iconKind    : result.Failed.Count > 0 ? "AlertCircleOutline" : "CheckCircleOutline",
                        iconColor   : result.Failed.Count > 0 ? "#f0a040" : "#70babc",
                        isDarkMode  : _isDarkMode);

                    OnCreateComplete?.Invoke();
                });
            }));
        }
    }
}
