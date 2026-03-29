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
    public class LinkDwgViewModel : BaseViewModel
    {
        private readonly RevitExternalEventService _eventService;
        private readonly bool _isDarkMode;
        private readonly System.Windows.Threading.Dispatcher _dispatcher;

        private bool   _isLinking;
        private string _statusMessage = "Kontrolli DWG failide ja vaadete vastavust, seejärel klõpsa 'Link Selected'.";
        private int    _linkableCount;

        public ObservableCollection<DwgMappingItemDto>  Items          { get; } = new ObservableCollection<DwgMappingItemDto>();
        public ObservableCollection<FloorPlanViewInfo>  AvailableViews { get; } = new ObservableCollection<FloorPlanViewInfo>();

        public bool IsLinking
        {
            get => _isLinking;
            set => SetProperty(ref _isLinking, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public int LinkableCount
        {
            get => _linkableCount;
            private set
            {
                if (SetProperty(ref _linkableCount, value))
                    OnPropertyChanged(nameof(LinkButtonLabel));
            }
        }

        public string LinkButtonLabel => _linkableCount > 0 ? $"Link ({_linkableCount})" : "Link Selected";

        public bool? AllSelected
        {
            get
            {
                if (Items.Count == 0) return false;
                bool all  = Items.All(x => x.IsSelected);
                bool none = Items.All(x => !x.IsSelected);
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

        public ICommand SelectAllCommand    { get; }
        public ICommand DeselectAllCommand  { get; }
        public ICommand LinkSelectedCommand { get; }

        public Action          OnLinkComplete  { get; set; }
        public Func<Window>    GetOwnerWindow  { get; set; }

        public LinkDwgViewModel(IReadOnlyList<string> paths, RevitExternalEventService eventService, bool isDarkMode = true)
        {
            _eventService = eventService;
            _isDarkMode   = isDarkMode;
            _dispatcher   = System.Windows.Threading.Dispatcher.CurrentDispatcher;

            foreach (var path in paths)
            {
                var item = new DwgMappingItemDto(path);
                item.PropertyChanged += OnItemPropertyChanged;
                Items.Add(item);
            }

            SelectAllCommand    = new RelayCommand(_ => SetAllSelected(true),  _ => Items.Count > 0 && Items.Any(x => !x.IsSelected));
            DeselectAllCommand  = new RelayCommand(_ => SetAllSelected(false), _ => Items.Any(x => x.IsSelected));
            LinkSelectedCommand = new RelayCommand(_ => LinkSelected(),        _ => _linkableCount > 0 && !_isLinking);

            // Load floor plan views from Revit, then auto-match
            StatusMessage = "Laadimine…";
            _eventService.Raise(new GetFloorPlanViewsRequest(views =>
            {
                _dispatcher.Invoke(() =>
                {
                    AvailableViews.Clear();
                    foreach (var v in views) AvailableViews.Add(v);

                    AutoMatch();

                    StatusMessage = AvailableViews.Count > 0
                        ? $"Leitud {AvailableViews.Count} Floor Plan vaade(t). Kontrolli vastavusi ja klõpsa 'Link Selected'."
                        : "Aktiivsest dokumendist ei leitud Floor Plan vaateid. Loo esmalt vaated (samm 6).";

                    UpdateLinkableCount();
                });
            }));
        }

        private void AutoMatch()
        {
            foreach (var item in Items)
            {
                var stem = item.FileStem;

                // 1. Exact case-insensitive match
                var match = AvailableViews.FirstOrDefault(v =>
                    string.Equals(v.Name, stem, StringComparison.OrdinalIgnoreCase));

                // 2. View name contains the DWG stem (or vice versa)
                if (match == null)
                    match = AvailableViews.FirstOrDefault(v =>
                        v.Name.IndexOf(stem, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        stem.IndexOf(v.Name, StringComparison.OrdinalIgnoreCase) >= 0);

                item.SelectedView = match; // null if no match found — user must pick manually
            }
        }

        private void SetAllSelected(bool selected)
        {
            foreach (var item in Items)
                item.IsSelected = selected;
        }

        private void OnItemPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DwgMappingItemDto.IsSelected) ||
                e.PropertyName == nameof(DwgMappingItemDto.SelectedView))
            {
                UpdateLinkableCount();
                OnPropertyChanged(nameof(AllSelected));
            }
        }

        private void UpdateLinkableCount()
            => LinkableCount = Items.Count(x => x.IsSelected && x.SelectedView != null);

        private void LinkSelected()
        {
            var mappings = Items
                .Where(x => x.IsSelected && x.SelectedView != null)
                .Select(x => new DwgLinkMapping(x.FullPath, x.SelectedView.ElementId))
                .ToList();

            if (mappings.Count == 0) return;

            IsLinking     = true;
            StatusMessage = $"Linkimine {mappings.Count} DWG faili…";

            _eventService.Raise(new LinkDwgFilesRequest(mappings, result =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsLinking = false;

                    if (result.ErrorMessage != null)
                    {
                        StatusMessage = result.ErrorMessage;
                        OnLinkComplete?.Invoke();
                        return;
                    }

                    var parts = new List<string>();
                    if (result.Linked.Count > 0) parts.Add($"Lingitud: {result.Linked.Count}");
                    if (result.Failed.Count > 0) parts.Add($"Ebaõnnestus: {result.Failed.Count}");
                    StatusMessage = parts.Count > 0 ? string.Join(", ", parts) + "." : "Muudatusi ei tehtud.";

                    var detailLines = new List<string>();
                    foreach (var name in result.Linked)
                        detailLines.Add($"✓  {name}");
                    foreach (var (name, error) in result.Failed)
                        detailLines.Add($"✗  {name}  ({error})");

                    string summaryMsg = result.Failed.Count == 0
                        ? $"Edukalt lingitud {result.Linked.Count} DWG fail(i) ja kinnitatud."
                        : "Linkimine lõpetatud. Vaata üksikasju allpool.";

                    DialogWindow.Show(
                        owner       : GetOwnerWindow?.Invoke(),
                        title       : "DWG linkimine lõpetatud",
                        message     : summaryMsg,
                        buttons     : new List<DialogButton> { new DialogButton("OK", "ok", isDefault: true) },
                        iconKind    : result.Failed.Count > 0 ? "AlertCircleOutline" : "CheckCircleOutline",
                        iconColor   : result.Failed.Count > 0 ? "#f0a040" : "#70babc",
                        detailItems : detailLines.Count > 0 ? detailLines : null,
                        isDarkMode  : _isDarkMode);

                    OnLinkComplete?.Invoke();
                });
            }));
        }
    }
}
