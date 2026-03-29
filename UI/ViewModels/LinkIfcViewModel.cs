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
    public class LinkIfcViewModel : BaseViewModel
    {
        private readonly RevitExternalEventService _eventService;
        private readonly bool _isDarkMode;

        private bool   _isLinking;
        private string _statusMessage = "Review the IFC files below. Deselect any you don't want to link, then click 'Link Selected'.";
        private int    _selectedCount;

        public ObservableCollection<IfcFileItemDto> Items { get; } = new ObservableCollection<IfcFileItemDto>();

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

        public int SelectedCount
        {
            get => _selectedCount;
            private set
            {
                if (SetProperty(ref _selectedCount, value))
                    OnPropertyChanged(nameof(LinkButtonLabel));
            }
        }

        public string LinkButtonLabel => _selectedCount > 0 ? $"Link ({_selectedCount})" : "Link Selected";

        /// <summary>True = all selected, False = none, Null = mixed (indeterminate).</summary>
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

        /// <summary>Wired by the code-behind to bring the window to the front after linking.</summary>
        public Action OnLinkComplete { get; set; }

        /// <summary>Wired by the code-behind to supply the owner Window for dialogs.</summary>
        public Func<Window> GetOwnerWindow { get; set; }

        public LinkIfcViewModel(IReadOnlyList<string> paths, RevitExternalEventService eventService, bool isDarkMode = true)
        {
            _eventService = eventService;
            _isDarkMode   = isDarkMode;

            foreach (var path in paths)
            {
                var item = new IfcFileItemDto(path);
                item.PropertyChanged += OnItemPropertyChanged;
                Items.Add(item);
            }

            UpdateCount();

            SelectAllCommand    = new RelayCommand(_ => SetAllSelected(true),  _ => Items.Count > 0 && _selectedCount < Items.Count);
            DeselectAllCommand  = new RelayCommand(_ => SetAllSelected(false), _ => _selectedCount > 0);
            LinkSelectedCommand = new RelayCommand(_ => LinkSelected(),        _ => _selectedCount > 0 && !_isLinking);
        }

        private void SetAllSelected(bool selected)
        {
            foreach (var item in Items)
                item.IsSelected = selected;
        }

        private void OnItemPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IfcFileItemDto.IsSelected))
                UpdateCount();
        }

        private void UpdateCount()
        {
            SelectedCount = Items.Count(x => x.IsSelected);
            OnPropertyChanged(nameof(AllSelected));
        }

        private void LinkSelected()
        {
            var paths = Items.Where(x => x.IsSelected).Select(x => x.FullPath).ToList();
            if (paths.Count == 0) return;

            IsLinking     = true;
            StatusMessage = $"Linking {paths.Count} IFC file{(paths.Count == 1 ? "" : "s")}…";

            _eventService.Raise(new LinkIfcFilesRequest(paths, result =>
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
                    if (result.Linked.Count > 0) parts.Add($"Linked {result.Linked.Count}");
                    if (result.Failed.Count > 0) parts.Add($"{result.Failed.Count} failed");
                    StatusMessage = parts.Count > 0 ? string.Join(", ", parts) + "." : "Nothing linked.";

                    var detailLines = new List<string>();
                    foreach (var name in result.Linked)
                        detailLines.Add($"✓  {name}");
                    foreach (var (name, error) in result.Failed)
                        detailLines.Add($"✗  {name}  ({error})");

                    string summaryMsg = result.Failed.Count == 0
                        ? $"Successfully linked {result.Linked.Count} IFC file{(result.Linked.Count == 1 ? "" : "s")} and pinned {(result.Linked.Count == 1 ? "it" : "them")}."
                        : "Linking complete. See details below.";

                    DialogWindow.Show(
                        owner       : GetOwnerWindow?.Invoke(),
                        title       : "Link IFC Complete",
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
