using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace ProjectSetup.UI.ViewModels
{
    /// <summary>
    /// Describes one button in a <see cref="DialogWindow"/>.
    /// </summary>
    public class DialogButton
    {
        public string  Label     { get; }
        public string  Result    { get; }   // returned from DialogWindow.Show()
        public bool    IsDefault { get; }   // highlighted / accent color
        public bool    IsCancel  { get; }   // fired by Escape key

        public DialogButton(string label, string result, bool isDefault = false, bool isCancel = false)
        {
            Label     = label;
            Result    = result;
            IsDefault = isDefault;
            IsCancel  = isCancel;
        }
    }

    /// <summary>
    /// ViewModel that drives <see cref="DialogWindow"/>.
    /// </summary>
    public class DialogViewModel : BaseViewModel
    {
        // ── Dialog chrome ─────────────────────────────────────────────────────
        public string Title       { get; }
        public string IconKind    { get; }   // MaterialDesign PackIcon Kind name, or null
        public string IconColor   { get; }   // hex or null → falls back to accent
        public string Message     { get; }   // may contain \n for multi-line

        // ── Optional detail list (e.g. skipped family names) ─────────────────
        public IReadOnlyList<string> DetailItems { get; }
        public bool HasDetails => DetailItems != null && DetailItems.Count > 0;

        // ── Buttons ───────────────────────────────────────────────────────────
        public IReadOnlyList<DialogButton> Buttons { get; }

        // ── Result set by code-behind when a button is clicked ────────────────
        public string ChosenResult { get; private set; }

        // ── Commands ──────────────────────────────────────────────────────────
        public IReadOnlyList<ICommand> ButtonCommands { get; }

        /// <summary>Raised when a button is clicked; code-behind subscribes to close the window.</summary>
        public event Action<string> CloseRequested;

        public DialogViewModel(
            string title,
            string message,
            IReadOnlyList<DialogButton> buttons,
            string iconKind  = null,
            string iconColor = null,
            IReadOnlyList<string> detailItems = null)
        {
            Title       = title;
            Message     = message;
            Buttons     = buttons ?? new List<DialogButton> { new DialogButton("OK", "OK", isDefault: true) };
            IconKind    = iconKind;
            IconColor   = iconColor ?? "#70babc";
            DetailItems = detailItems;

            var cmds = new List<ICommand>();
            foreach (var btn in Buttons)
            {
                var captured = btn;
                cmds.Add(new RelayCommand(_ =>
                {
                    ChosenResult = captured.Result;
                    CloseRequested?.Invoke(captured.Result);
                }));
            }
            ButtonCommands = cmds;
        }
    }
}
