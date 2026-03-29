using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace ProjectSetup.Models
{
    public class IfcFileItemDto : INotifyPropertyChanged
    {
        private bool _isSelected = true;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        /// <summary>File name with extension, shown in the preview grid.</summary>
        public string FileName { get; }

        /// <summary>Full absolute path to the IFC file.</summary>
        public string FullPath { get; }

        /// <summary>File size in kilobytes, shown in the preview grid.</summary>
        public long FileSizeKb { get; }

        public IfcFileItemDto(string fullPath)
        {
            FullPath   = fullPath;
            FileName   = Path.GetFileName(fullPath);
            var info   = new FileInfo(fullPath);
            FileSizeKb = info.Exists ? info.Length / 1024 : 0;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
