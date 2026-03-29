using ProjectSetup.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProjectSetup.UI
{
    public partial class DialogWindow : Window
    {
        private DialogViewModel _vm;

        private DialogWindow() => InitializeComponent();

        // ── Static factory ────────────────────────────────────────────────────

        /// <summary>
        /// Shows a styled dialog and returns the <see cref="DialogButton.Result"/> string
        /// of whichever button the user clicked (or null if closed without choosing).
        /// </summary>
        /// <param name="owner">Parent window for centering (pass null for screen-centered).</param>
        /// <param name="title">Dialog heading.</param>
        /// <param name="message">Body text (use \n for line breaks).</param>
        /// <param name="buttons">Button definitions; defaults to a single "OK" button.</param>
        /// <param name="iconKind">MaterialDesign PackIcon Kind name, e.g. "AlertCircleOutline".</param>
        /// <param name="iconColor">Hex color for the icon, e.g. "#f0a040". Defaults to #70babc.</param>
        /// <param name="detailItems">Optional list of strings shown in a scrollable detail box.</param>
        /// <param name="isDarkMode">Swap to light theme when false.</param>
        public static string Show(
            Window owner,
            string title,
            string message,
            IReadOnlyList<DialogButton> buttons  = null,
            string iconKind                       = null,
            string iconColor                      = null,
            IReadOnlyList<string> detailItems     = null,
            bool isDarkMode                       = true)
        {
            var dlg = new DialogWindow();

            if (!isDarkMode)
                dlg.SwapTheme(false);

            if (owner != null)
            {
                dlg.Owner = owner;
                dlg.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            var vm = new DialogViewModel(title, message, buttons, iconKind, iconColor, detailItems);
            dlg._vm        = vm;
            dlg.DataContext = vm;

            // Build buttons dynamically so IsDefault/IsCancel work correctly
            dlg.BuildButtons(vm);

            // Subscribe after building so the lambda captures dlg
            vm.CloseRequested += _ => dlg.Close();

            dlg.Topmost = true;
            dlg.ShowDialog();
            return vm.ChosenResult;
        }

        // ── Button builder ────────────────────────────────────────────────────

        private void BuildButtons(DialogViewModel vm)
        {
            ButtonsHost.Items.Clear();
            for (int i = 0; i < vm.Buttons.Count; i++)
            {
                var btn  = vm.Buttons[i];
                var cmd  = vm.ButtonCommands[i];

                var styleName = btn.IsDefault ? "DialogButtonPrimaryStyle" : "DialogButtonStyle";
                var style = (Style)Resources[styleName];

                var button = new Button
                {
                    Content   = btn.Label,
                    Command   = cmd,
                    Style     = style,
                    IsDefault = btn.IsDefault,
                    IsCancel  = btn.IsCancel,
                };
                ButtonsHost.Items.Add(button);
            }
        }

        // ── Keyboard handling ─────────────────────────────────────────────────

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                // Find the cancel button, if any
                if (_vm != null)
                {
                    for (int i = 0; i < _vm.Buttons.Count; i++)
                    {
                        if (_vm.Buttons[i].IsCancel)
                        {
                            _vm.ButtonCommands[i].Execute(null);
                            return;
                        }
                    }
                    // No explicit cancel button: just close with null result
                }
                Close();
            }
        }

        // ── Theme swap (mirrors pattern from other child windows) ─────────────

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
    }
}
