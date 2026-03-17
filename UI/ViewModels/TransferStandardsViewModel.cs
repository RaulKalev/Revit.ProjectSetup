using ProjectSetup.Models;
using ProjectSetup.Services.Revit;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace ProjectSetup.UI.ViewModels
{
    public class TransferStandardsViewModel : BaseViewModel
    {
        private readonly RevitExternalEventService _eventService;
        private readonly System.Windows.Threading.Dispatcher _dispatcher;

        private string _selectedProject;
        private string _selectedCategory;
        private bool   _isLoading;
        private string _statusMessage = "Select a source project and category.";
        private int    _itemCount;

        public ObservableCollection<string>           SourceProjects { get; }
        public ObservableCollection<string>           Categories     { get; }
        public ObservableCollection<StandardsItemDto> Items          { get; }

        public string SelectedProject
        {
            get => _selectedProject;
            set
            {
                if (SetProperty(ref _selectedProject, value) && _selectedCategory != null)
                    LoadItems(_selectedCategory);
            }
        }

        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value) && value != null)
                    LoadItems(value);
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

        public int ItemCount
        {
            get => _itemCount;
            set => SetProperty(ref _itemCount, value);
        }

        public ICommand SelectAllCommand       { get; }
        public ICommand DeselectAllCommand     { get; }
        public ICommand RefreshProjectsCommand { get; }

        public TransferStandardsViewModel(RevitExternalEventService eventService)
        {
            _eventService = eventService;
            _dispatcher   = System.Windows.Threading.Dispatcher.CurrentDispatcher;

            SourceProjects = new ObservableCollection<string>();

            Categories = new ObservableCollection<string>
            {
                "Line Styles",
                "Fill Patterns",
                "Text Types",
                "Dimension Types",
                "Materials",
                "Object Styles",
                "Filters",
                "View Templates"
            };

            Items = new ObservableCollection<StandardsItemDto>();

            SelectAllCommand       = new RelayCommand(_ => SetAllSelected(true),  _ => Items.Count > 0);
            DeselectAllCommand     = new RelayCommand(_ => SetAllSelected(false), _ => Items.Count > 0);
            RefreshProjectsCommand = new RelayCommand(_ => LoadProjects());

            LoadProjects();
        }

        private void LoadProjects()
        {
            StatusMessage = "Fetching open documents…";
            _eventService.Raise(new GetOpenDocumentsRequest(docs =>
            {
                _dispatcher.Invoke(() =>
                {
                    var prev = _selectedProject;
                    SourceProjects.Clear();
                    foreach (var title in docs)
                        SourceProjects.Add(title);

                    // Restore previous selection if still available, else pick first
                    if (prev != null && SourceProjects.Contains(prev))
                        SelectedProject = prev;
                    else if (SourceProjects.Count > 0)
                        SelectedProject = SourceProjects[0];

                    StatusMessage = SourceProjects.Count > 0
                        ? "Select a category to browse."
                        : "No open documents found. Open a project in Revit first.";
                });
            }));
        }

        private void LoadItems(string category)
        {
            if (_selectedProject == null)
            {
                StatusMessage = "Select a source project first.";
                return;
            }

            Items.Clear();
            ItemCount = 0;
            IsLoading = true;
            StatusMessage = $"Loading {category.ToLower()} from '{_selectedProject}'…";

            _eventService.Raise(new GetStandardsItemsRequest(category, _selectedProject, items =>
            {
                _dispatcher.Invoke(() =>
                {
                    Items.Clear();
                    foreach (var item in items)
                        Items.Add(item);

                    ItemCount     = Items.Count;
                    IsLoading     = false;
                    StatusMessage = Items.Count > 0
                        ? $"{Items.Count} item{(Items.Count == 1 ? "" : "s")} found in '{_selectedProject}'."
                        : $"No {category.ToLower()} found in '{_selectedProject}'.";
                });
            }));
        }

        private void SetAllSelected(bool selected)
        {
            foreach (var item in Items)
                item.IsSelected = selected;
        }
    }
}
