using ProjectSetup.Services.Revit;
using ProjectSetup.UI.ViewModels;
using System;
using System.Windows;
using System.Windows.Input;

namespace ProjectSetup.UI
{
    public partial class CreateLevelsWindow : Window
    {
        private readonly WindowResizer _windowResizer;

        public CreateLevelsViewModel ViewModel { get; private set; }

        public CreateLevelsWindow(RevitExternalEventService eventService, bool isDarkMode)
        {
            InitializeComponent();

            if (!isDarkMode)
                SwapTheme(false);

            ViewModel   = new CreateLevelsViewModel(eventService);
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

        #region Window chrome

        private void Window_MouseMove(object sender, MouseEventArgs e)
            => _windowResizer.ResizeWindow(e);

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e) { }

        private void TitleBar_Loaded(object sender, RoutedEventArgs e) { }

        private void Edge_MouseEnter(object sender, MouseEventArgs e) { }
        private void Edge_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!_windowResizer.IsResizing)
                Mouse.OverrideCursor = null;
        }

        private void LeftEdge_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
            => _windowResizer.StartResizing(e, ResizeDirection.Left);
        private void RightEdge_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
            => _windowResizer.StartResizing(e, ResizeDirection.Right);
        private void BottomEdge_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
            => _windowResizer.StartResizing(e, ResizeDirection.Bottom);
        private void BottomLeftCorner_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
            => _windowResizer.StartResizing(e, ResizeDirection.BottomLeft);
        private void BottomRightCorner_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
            => _windowResizer.StartResizing(e, ResizeDirection.BottomRight);

        #endregion
    }
}
