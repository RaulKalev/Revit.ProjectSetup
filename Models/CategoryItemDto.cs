using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ProjectSetup.Models
{
    public class CategoryItemDto : INotifyPropertyChanged
    {
        private bool _isSelected;

        public string Name             { get; }
        public string ViewTemplateName { get; }

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

        public CategoryItemDto(string name, string viewTemplateName, bool isSelected = false)
        {
            Name             = name;
            ViewTemplateName = viewTemplateName;
            _isSelected      = isSelected;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
