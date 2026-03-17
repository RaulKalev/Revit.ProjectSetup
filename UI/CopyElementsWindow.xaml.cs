using ProjectSetup.Services.Revit;
using ProjectSetup.UI.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProjectSetup.UI
{
    public partial class CopyElementsWindow : Window
    {
        private readonly WindowResizer _windowResizer;

        public CopyElementsViewModel ViewModel { get; private set; }

        public CopyElementsWindow(RevitExternalEventService eventService, bool isDarkMode)
        {
            InitializeComponent();

            if (!isDarkMode)
                SwapTheme(false);

            ViewModel   = new CopyElementsViewModel(eventService, isDarkMode);
            ViewModel.OnImportComplete  = () => { Activate(); Topmost = true; Topmost = false; };
            ViewModel.GetOwnerWindow    = () => this;
            DataContext = ViewModel;

            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Opacity = 0;
            Loaded += (s, e) => Opacity = 1;

            _windowResizer = new WindowResizer(this);
            MouseLeftButtonUp += (s, e) => _windowResizer.StopResizing();
        }

        private void SwapTheme(bool dark)
        {
            Uri newUri = dark
                ? new Uri("pack://application:,,,/ProjectSetup;component/UI/Themes/DarkTheme.xaml")
                : new Uri("pack://application:,,,/ProjectSetup;component/UI/Themes/LightTheme.xaml");

            var dicts = Resources.MergedDictionaries;
            for (int i = 0; i < dicts.Count; i++)
            {
                var src = dicts[i]?.Source?.ToString();
                if (src != null && (src.Contains("DarkTheme") || src.Contains("LightTheme")))
                {
                    dicts[i] = new ResourceDictionary { Source = newUri };
                    return;
                }
            }
            dicts.Add(new ResourceDictionary { Source = newUri });
        }

        #region DataGrid single-click checkbox

        private void DataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // If the click landed on a CheckBox, toggle it directly and absorb the event
            // so the DataGrid doesn't swallow the first click for row-selection.
            var cb = GetVisualParent<CheckBox>(e.OriginalSource as DependencyObject);
            if (cb != null)
            {
                cb.IsChecked = !cb.IsChecked;
                e.Handled = true;
                return;
            }

            var cell = GetVisualParent<DataGridCell>(e.OriginalSource as DependencyObject);
            if (cell == null || cell.IsEditing || cell.IsReadOnly) return;
            if (!cell.IsFocused) cell.Focus();
            FamiliesGrid.BeginEdit();
        }

        private static T GetVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T t) return t;
                child = System.Windows.Media.VisualTreeHelper.GetParent(child);
            }
            return null;
        }

        #endregion

        #region Window chrome

        private void Window_MouseMove(object sender, MouseEventArgs e)
            => _windowResizer.ResizeWindow(e);

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e) { }

        private void TitleBar_Loaded(object sender, RoutedEventArgs e) { }

        #endregion
    }
}
