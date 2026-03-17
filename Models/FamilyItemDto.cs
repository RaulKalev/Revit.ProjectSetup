using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ProjectSetup.Models
{
    public class FamilyItemDto : INotifyPropertyChanged
    {
        private bool _isSelected;

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

        /// <summary>Revit UniqueId (GUID string) of the Family element in the source document.</summary>
        public string UniqueId  { get; set; }
        public string Name      { get; set; }
        public string Category  { get; set; }
        public int    TypeCount { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
