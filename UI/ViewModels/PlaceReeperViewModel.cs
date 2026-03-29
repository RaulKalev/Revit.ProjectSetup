using ProjectSetup.Models;
using ProjectSetup.Services.Revit;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace ProjectSetup.UI.ViewModels
{
    public class PlaceReeperViewModel : BaseViewModel
    {
        private readonly RevitExternalEventService _eventService;
        private readonly Dispatcher               _dispatcher;
        private readonly bool                     _isDarkMode;

        private bool         _isEl          = true;
        private bool         _isSearching;
        private bool         _isPlacing;
        private string       _statusMessage = "Vali EL/EN ja vajuta Otsi.";
        private ReeperItemDto _selectedItem;

        // ── Discipline ───────────────────────────────────────────────────────

        public bool IsEL
        {
            get => _isEl;
            set
            {
                if (SetProperty(ref _isEl, value))
                    OnPropertyChanged(nameof(IsEN));
            }
        }

        public bool IsEN
        {
            get => !_isEl;
            set => IsEL = !value;
        }

        // ── State ────────────────────────────────────────────────────────────

        public bool IsSearching
        {
            get => _isSearching;
            set
            {
                if (SetProperty(ref _isSearching, value))
                    OnPropertyChanged(nameof(IsBusy));
            }
        }

        public bool IsPlacing
        {
            get => _isPlacing;
            set
            {
                if (SetProperty(ref _isPlacing, value))
                    OnPropertyChanged(nameof(IsBusy));
            }
        }

        public bool IsBusy => _isSearching || _isPlacing;

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        // ── Data ─────────────────────────────────────────────────────────────

        public ObservableCollection<ReeperItemDto> FoundItems { get; }
            = new ObservableCollection<ReeperItemDto>();

        public ReeperItemDto SelectedItem
        {
            get => _selectedItem;
            set => SetProperty(ref _selectedItem, value);
        }

        // ── Commands ─────────────────────────────────────────────────────────

        public ICommand SearchCommand { get; }
        public ICommand PlaceCommand  { get; }

        // ── Delegates wired by the window ────────────────────────────────────

        public Action       OnPlaceComplete { get; set; }
        public Func<Window> GetOwnerWindow  { get; set; }

        // ── Constructor ──────────────────────────────────────────────────────

        public PlaceReeperViewModel(RevitExternalEventService eventService, bool isDarkMode)
        {
            _eventService = eventService;
            _isDarkMode   = isDarkMode;
            _dispatcher   = Dispatcher.CurrentDispatcher;

            SearchCommand = new RelayCommand(_ => Search(), _ => !IsBusy);
            PlaceCommand  = new RelayCommand(_ => Place(),  _ => _selectedItem != null && !IsBusy);
        }

        // ── Search ───────────────────────────────────────────────────────────

        private void Search()
        {
            IsSearching = true;
            FoundItems.Clear();
            SelectedItem  = null;
            StatusMessage = "Otsin reeperi elemente lingitud mudelitest…";

            _eventService.Raise(new FindReeperInLinksRequest(result =>
            {
                _dispatcher.Invoke(() =>
                {
                    IsSearching = false;

                    if (result.ErrorMessage != null)
                    {
                        StatusMessage = $"Viga: {result.ErrorMessage}";
                        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                        return;
                    }

                    foreach (var item in result.Items)
                        FoundItems.Add(item);

                    StatusMessage = FoundItems.Count > 0
                        ? $"{FoundItems.Count} reeperi element{(FoundItems.Count == 1 ? "" : "i")} leitud. " +
                          "Vali element ja vajuta Paigalda."
                        : "Ühtegi reeperi elementi ei leitud lingitud mudelitest.";

                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                });
            }));
        }

        // ── Place ─────────────────────────────────────────────────────────────

        private void Place()
        {
            if (_selectedItem == null) return;

            string discipline = _isEl ? "EL" : "EN";
            IsPlacing     = true;
            StatusMessage = $"Paigaldan {discipline}_REEPER_v01…";

            _eventService.Raise(new PlaceReeperRequest(_selectedItem, discipline, result =>
            {
                _dispatcher.Invoke(() =>
                {
                    IsPlacing = false;

                    if (result.ErrorMessage != null)
                    {
                        StatusMessage = $"Viga: {result.ErrorMessage}";
                        DialogWindow.Show(
                            owner     : GetOwnerWindow?.Invoke(),
                            title     : "Paigalda Reeper",
                            message   : result.ErrorMessage,
                            iconKind  : "AlertCircleOutline",
                            iconColor : "#f0a040",
                            isDarkMode: _isDarkMode);
                        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                        return;
                    }

                    StatusMessage = result.Message ?? "Reeper paigaldatud.";
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                    OnPlaceComplete?.Invoke();
                });
            }));
        }
    }
}
