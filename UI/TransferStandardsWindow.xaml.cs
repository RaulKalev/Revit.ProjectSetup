using ProjectSetup.Services.Revit;
using ProjectSetup.UI.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProjectSetup.UI
{
    public partial class TransferStandardsWindow : Window
    {
        private readonly WindowResizer _windowResizer;

        public TransferStandardsViewModel ViewModel { get; private set; }

        public TransferStandardsWindow(RevitExternalEventService eventService, bool isDarkMode)
        {
            InitializeComponent();

            // Sync to the current theme of the main window (XAML defaults to dark)
            if (!isDarkMode)
                SwapTheme(false);

            ViewModel   = new TransferStandardsViewModel(eventService);
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

        /// <summary>
        /// Puts the clicked cell into edit mode immediately so the first click
        /// toggles the checkbox rather than just selecting the row.
        /// </summary>
        private void DataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var cell = GetVisualParent<DataGridCell>(e.OriginalSource as DependencyObject);
            if (cell == null || cell.IsEditing || cell.IsReadOnly) return;

            if (!cell.IsFocused) cell.Focus();
            StandardsGrid.BeginEdit();
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
