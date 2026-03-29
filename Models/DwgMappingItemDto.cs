using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace ProjectSetup.Models
{
    public class DwgMappingItemDto : INotifyPropertyChanged
    {
        private bool _isSelected = true;
        private FloorPlanViewInfo _selectedView;

        public string FullPath   { get; }
        public string FileName   => Path.GetFileName(FullPath);
        public string FileStem   => Path.GetFileNameWithoutExtension(FullPath);
        public long   FileSizeKb { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected == value) return; _isSelected = value; OnPropertyChanged(); }
        }

        public FloorPlanViewInfo SelectedView
        {
            get => _selectedView;
            set { if (ReferenceEquals(_selectedView, value)) return; _selectedView = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasView)); }
        }

        public bool HasView => _selectedView != null;

        public DwgMappingItemDto(string fullPath)
        {
            FullPath   = fullPath;
            var info   = new FileInfo(fullPath);
            FileSizeKb = info.Exists ? info.Length / 1024 : 0;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
