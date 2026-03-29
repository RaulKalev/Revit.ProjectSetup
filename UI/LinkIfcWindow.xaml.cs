using ProjectSetup.Services.Revit;
using ProjectSetup.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProjectSetup.UI
{
    public partial class LinkIfcWindow : Window
    {
        private readonly WindowResizer _windowResizer;

        public LinkIfcViewModel ViewModel { get; private set; }

        public LinkIfcWindow(IReadOnlyList<string> paths, RevitExternalEventService eventService, bool isDarkMode)
        {
            InitializeComponent();

            if (!isDarkMode)
                SwapTheme(false);

            ViewModel                   = new LinkIfcViewModel(paths, eventService, isDarkMode);
            ViewModel.OnLinkComplete    = () => { Activate(); Topmost = true; Topmost = false; };
            ViewModel.GetOwnerWindow    = () => this;
            DataContext                 = ViewModel;

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

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

        #region DataGrid single-click checkbox

        private void DataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
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
            IfcFilesGrid.BeginEdit();
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

        #region Window chrome / resize

        private void TitleBar_Loaded(object sender, RoutedEventArgs e) { }

        private void Window_MouseMove(object sender, MouseEventArgs e)
            => _windowResizer.ResizeWindow(e);

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e) { }

        private void LeftEdge_MouseEnter(object sender, MouseEventArgs e)            => Mouse.OverrideCursor = Cursors.SizeWE;
        private void RightEdge_MouseEnter(object sender, MouseEventArgs e)           => Mouse.OverrideCursor = Cursors.SizeWE;
        private void BottomEdge_MouseEnter(object sender, MouseEventArgs e)          => Mouse.OverrideCursor = Cursors.SizeNS;
        private void BottomLeftCorner_MouseEnter(object sender, MouseEventArgs e)    => Mouse.OverrideCursor = Cursors.SizeNESW;
        private void BottomRightCorner_MouseEnter(object sender, MouseEventArgs e)   => Mouse.OverrideCursor = Cursors.SizeNWSE;
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
