using Autodesk.Revit.UI;
using ProjectSetup.Services;
using ProjectSetup.Services.Revit;
using ProjectSetup.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace ProjectSetup.UI
{
    public partial class ProjectSetupWindow : Window
    {
        #region PInvoke
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        #endregion

        #region Fields
        private readonly WindowResizer _windowResizer;
        private readonly SettingsService _settingsService;
        private readonly RevitExternalEventService _externalEventService;
        private bool _isDarkMode = true;
        #endregion

        public MainViewModel ViewModel { get; private set; }

        public ProjectSetupWindow(UIApplication app, RevitExternalEventService externalEventService, SettingsService settingsService)
        {
            _settingsService      = settingsService;
            _externalEventService = externalEventService;

            // Load and apply theme before InitializeComponent so DynamicResource brushes resolve correctly
            var initialSettings = _settingsService.Load();
            _isDarkMode = initialSettings.IsDarkMode;
            ApplyThemeToAppResources(_isDarkMode);

            InitializeComponent();

            ViewModel = new MainViewModel(externalEventService);
            DataContext = ViewModel;

            // Wire up window-navigation actions so the ViewModel stays UI-agnostic
            ViewModel.OpenTransferWindowRequest      = OpenTransferStandardsWindow;
            ViewModel.OpenCopyElementsWindowRequest  = OpenCopyElementsWindow;
            ViewModel.OpenCreateLevelsWindowRequest  = OpenCreateLevelsWindow;
            ViewModel.OpenCreateLevelsWindowAndThenRequest = OpenCreateLevelsWindowAndThen;
            ViewModel.RequestFolderPick              = PickFolder;
            ViewModel.OpenLinkIfcWindowRequest       = OpenLinkIfcWindow;
            ViewModel.OpenLinkDwgWindowRequest       = OpenLinkDwgWindow;
            ViewModel.OpenCreatePlanSetsWindowRequest = OpenCreatePlanSetsWindow;
            ViewModel.OpenPlaceReeperWindowRequest    = OpenPlaceReeperWindow;
            ViewModel.RequestSaveFilePick            = PickSaveFilePath;
            ViewModel.GetOwnerWindow                 = () => this;
            ViewModel.IsDarkMode                     = _isDarkMode;

            ThemeToggleButton.IsChecked = _isDarkMode;

            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Opacity = 0;

            _windowResizer = new WindowResizer(this);
            Closed += (s, e) => SaveWindowState();
            Loaded += OnWindowLoaded;
            MouseLeftButtonUp += (s, e) => _windowResizer.StopResizing();

            LoadWindowState(initialSettings);
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            // Fade in after layout is complete (avoids flash during WPF initialization)
            Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() => Opacity = 1));
        }

        private void OpenTransferStandardsWindow()
        {
            var win = new TransferStandardsWindow(_externalEventService, _isDarkMode);
            win.Show();
        }

        private void OpenCopyElementsWindow()
        {
            var win = new CopyElementsWindow(_externalEventService, _isDarkMode);
            win.Show();
        }

        private void OpenCreateLevelsWindow()
        {
            var win = new CreateLevelsWindow(_externalEventService, _isDarkMode);
            win.Show();
        }

        private void OpenCreateLevelsWindowAndThen(Action onClosed)
        {
            var win = new CreateLevelsWindow(_externalEventService, _isDarkMode);
            win.Closed += (s, e) => onClosed?.Invoke();
            win.Show();
        }

        private string PickFolder()
        {
#if NET8_0_OR_GREATER
            var dlg = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select folder containing IFC files"
            };
            return dlg.ShowDialog() == true ? dlg.FolderName : null;
#else
            using var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description     = "Select folder containing IFC files",
                ShowNewFolderButton = false
            };
            return dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK
                ? dlg.SelectedPath
                : null;
#endif
        }

        private void OpenLinkIfcWindow(List<string> paths)
        {
            var win = new LinkIfcWindow(paths, _externalEventService, _isDarkMode);
            win.Topmost = true;
            win.Show();
        }

        private void OpenLinkDwgWindow(List<string> paths)
        {
            var win = new LinkDwgWindow(paths, _externalEventService, _isDarkMode);
            win.Topmost = true;
            win.Show();
        }

        private void OpenCreatePlanSetsWindow()
        {
            var win = new CreatePlanSetsWindow(_externalEventService, _isDarkMode);
            win.Topmost = true;
            win.Show();
        }

        private void OpenPlaceReeperWindow(Action onSuccess)
        {
            var win = new PlaceReeperWindow(_externalEventService, _isDarkMode);
            win.ViewModel.OnPlaceComplete = () => { win.Close(); onSuccess?.Invoke(); };
            win.Topmost = true;
            win.Show();
        }

        private string PickSaveFilePath()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title      = "Save Revit Project As",
                Filter     = "Revit Project (*.rvt)|*.rvt",
                DefaultExt = ".rvt"
            };
            return dlg.ShowDialog() == true ? dlg.FileName : null;
        }

        #region Theme

        private void ToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            _isDarkMode = !_isDarkMode;
            ViewModel.IsDarkMode = _isDarkMode;
            SwapTheme(_isDarkMode);
            var settings = _settingsService.Load();
            settings.IsDarkMode = _isDarkMode;
            _settingsService.Save(settings);
        }

        private void SwapTheme(bool dark)
        {
            Uri newUri = dark
                ? new Uri("pack://application:,,,/ProjectSetup;component/UI/Themes/DarkTheme.xaml")
                : new Uri("pack://application:,,,/ProjectSetup;component/UI/Themes/LightTheme.xaml");

            // Swap in this window's MergedDictionaries
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

        // Applied before InitializeComponent so first render uses correct colors
        private static void ApplyThemeToAppResources(bool dark)
        {
            if (Application.Current == null) return;

            Uri uri = dark
                ? new Uri("pack://application:,,,/ProjectSetup;component/UI/Themes/DarkTheme.xaml")
                : new Uri("pack://application:,,,/ProjectSetup;component/UI/Themes/LightTheme.xaml");

            var appDicts = Application.Current.Resources.MergedDictionaries;
            for (int i = appDicts.Count - 1; i >= 0; i--)
            {
                var src = appDicts[i]?.Source?.ToString();
                if (src != null && (src.Contains("DarkTheme") || src.Contains("LightTheme")))
                {
                    appDicts[i] = new ResourceDictionary { Source = uri };
                    return;
                }
            }
            appDicts.Add(new ResourceDictionary { Source = uri });
        }

        #endregion

        #region Window state

        private void LoadWindowState(Models.SettingsModel settings)
        {
            if (!double.IsNaN(settings.WindowLeft) && settings.WindowLeft >= 0) Left = settings.WindowLeft;
            if (!double.IsNaN(settings.WindowTop)  && settings.WindowTop  >= 0) Top  = settings.WindowTop;
            if (settings.WindowWidth  > MinWidth)  Width  = settings.WindowWidth;
            if (settings.WindowHeight > MinHeight) Height = settings.WindowHeight;
        }

        private void SaveWindowState()
        {
            var settings = _settingsService.Load();
            settings.WindowLeft   = Left;
            settings.WindowTop    = Top;
            settings.WindowWidth  = ActualWidth;
            settings.WindowHeight = ActualHeight;
            settings.IsDarkMode   = _isDarkMode;
            _settingsService.Save(settings);
        }

        #endregion

        #region Title bar

        private void TitleBar_Loaded(object sender, RoutedEventArgs e) { /* TitleBar handles drag internally */ }

        #endregion

        #region Window chrome / resize

        private void Window_MouseMove(object sender, MouseEventArgs e)
            => _windowResizer.ResizeWindow(e);

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e) { }
        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
            => _windowResizer.StopResizing();
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e) { }
        private void Window_PreviewKeyUp(object sender, KeyEventArgs e) { }

        private void LeftEdge_MouseEnter(object sender, MouseEventArgs e)    => Mouse.OverrideCursor = Cursors.SizeWE;
        private void RightEdge_MouseEnter(object sender, MouseEventArgs e)   => Mouse.OverrideCursor = Cursors.SizeWE;
        private void BottomEdge_MouseEnter(object sender, MouseEventArgs e)  => Mouse.OverrideCursor = Cursors.SizeNS;
        private void BottomLeftCorner_MouseEnter(object sender, MouseEventArgs e)  => Mouse.OverrideCursor = Cursors.SizeNESW;
        private void BottomRightCorner_MouseEnter(object sender, MouseEventArgs e) => Mouse.OverrideCursor = Cursors.SizeNWSE;
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