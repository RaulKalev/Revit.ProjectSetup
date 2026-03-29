using ProjectSetup.Models;
using ProjectSetup.Services.Revit;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace ProjectSetup.UI.ViewModels
{
    public class CreateLevelsViewModel : BaseViewModel
    {
        private readonly RevitExternalEventService _eventService;
        private readonly System.Windows.Threading.Dispatcher _dispatcher;

        private string _selectedLink;
        private bool   _isLoading;
        private string _statusMessage = "Vali lingitud mudel tasandite eelvaatamiseks.";

        public ObservableCollection<string>          LinkedModels  { get; } = new ObservableCollection<string>();
        public ObservableCollection<LevelPreviewDto> PreviewItems  { get; } = new ObservableCollection<LevelPreviewDto>();

        public string SelectedLink
        {
            get => _selectedLink;
            set
            {
                if (SetProperty(ref _selectedLink, value) && value != null)
                    LoadPreview(value);
            }
        }

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

        public bool CanApply => !_isLoading && _selectedLink != null && PreviewItems.Count > 0;

        public ICommand RefreshLinksCommand { get; }
        public ICommand ApplyCommand        { get; }
        public Action   CloseWindowRequest  { get; set; }

        public CreateLevelsViewModel(RevitExternalEventService eventService)
        {
            _eventService = eventService;
            _dispatcher   = System.Windows.Threading.Dispatcher.CurrentDispatcher;

            RefreshLinksCommand = new RelayCommand(_ => LoadLinks());
            ApplyCommand        = new RelayCommand(_ => ApplyLevels(), _ => CanApply);

            LoadLinks();
        }

        private void LoadLinks()
        {
            IsLoading     = true;
            StatusMessage = "Laadin lingitud mudeleid…";

            _eventService.Raise(new GetLinkedModelsRequest(links =>
            {
                _dispatcher.Invoke(() =>
                {
                    var prev = _selectedLink;
                    LinkedModels.Clear();
                    foreach (var t in links) LinkedModels.Add(t);

                    if (prev != null && LinkedModels.Contains(prev))
                        SelectedLink = prev;
                    else if (LinkedModels.Count > 0)
                        SelectedLink = LinkedModels[0];
                    else
                    {
                        PreviewItems.Clear();
                        IsLoading     = false;
                        StatusMessage = "Aktiivsest dokumendist ei leitud lingitud mudeleid.";
                        OnPropertyChanged(nameof(CanApply));
                    }
                });
            }));
        }

        private void LoadPreview(string linkTitle)
        {
            IsLoading     = true;
            StatusMessage = $"Loen tasandeid mudelist '{linkTitle}'…";
            PreviewItems.Clear();
            OnPropertyChanged(nameof(CanApply));

            _eventService.Raise(new GetLevelsFromLinkRequest(linkTitle, items =>
            {
                _dispatcher.Invoke(() =>
                {
                    PreviewItems.Clear();
                    foreach (var item in items)
                        PreviewItems.Add(item);

                    IsLoading = false;
                    StatusMessage = PreviewItems.Count > 0
                        ? $"{PreviewItems.Count} tasandit leitud — kontrolli ja vajuta Rakenda."
                        : "Lingitud mudelist ei leitud tasandeid.";

                    OnPropertyChanged(nameof(CanApply));
                });
            }));
        }

        private void ApplyLevels()
        {
            if (_selectedLink == null) return;

            IsLoading     = true;
            StatusMessage = "Rakendan tasandeid…";
            OnPropertyChanged(nameof(CanApply));

            _eventService.Raise(new CreateLevelsRequest(_selectedLink, res =>
            {
                _dispatcher.Invoke(() =>
                {
                    IsLoading = false;

                    if (res.ErrorMessage != null)
                    {
                        StatusMessage = $"Viga: {res.ErrorMessage}";
                        OnPropertyChanged(nameof(CanApply));
                        return;
                    }

                    var parts = new System.Collections.Generic.List<string>();
                    if (res.Renamed > 0) parts.Add($"{res.Renamed} nimetati ümber");
                    if (res.Created > 0) parts.Add($"{res.Created} loodud");
                    if (res.Deleted > 0) parts.Add($"{res.Deleted} kustutatud");
                    if (res.Skipped > 0) parts.Add($"{res.Skipped} ei saanud kustutada");

                    StatusMessage = parts.Count > 0
                        ? "Valmis — " + string.Join(", ", parts) + "."
                        : "Valmis.";

                    CloseWindowRequest?.Invoke();
                });
            }));
        }
    }
}
