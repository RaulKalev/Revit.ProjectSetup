using ProjectSetup.Services.Revit;
using ProjectSetup.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace ProjectSetup.UI.ViewModels
{
    /// <summary>
    /// Main window view model — owns all commands for the 3 plugin sections.
    /// </summary>
    public class MainViewModel : BaseViewModel
    {
        private readonly RevitExternalEventService _eventService;
        private bool _isBusy;
        private string _statusMessage = "Ready";

        // ── Step completion state stored in Extensible Storage ────────────────
        private readonly HashSet<int> _completedSteps = new HashSet<int>();

        private bool _isStep1Done;  public bool IsStep1Done  { get => _isStep1Done;  set => SetProperty(ref _isStep1Done,  value); }
        private bool _isStep2Done;  public bool IsStep2Done  { get => _isStep2Done;  set => SetProperty(ref _isStep2Done,  value); }
        private bool _isStep3Done;  public bool IsStep3Done  { get => _isStep3Done;  set => SetProperty(ref _isStep3Done,  value); }
        private bool _isStep4Done;  public bool IsStep4Done  { get => _isStep4Done;  set => SetProperty(ref _isStep4Done,  value); }
        private bool _isStep5Done;  public bool IsStep5Done  { get => _isStep5Done;  set => SetProperty(ref _isStep5Done,  value); }
        private bool _isStep6Done;  public bool IsStep6Done  { get => _isStep6Done;  set => SetProperty(ref _isStep6Done,  value); }
        private bool _isStep7Done;  public bool IsStep7Done  { get => _isStep7Done;  set => SetProperty(ref _isStep7Done,  value); }
        private bool _isStep8Done;  public bool IsStep8Done  { get => _isStep8Done;  set => SetProperty(ref _isStep8Done,  value); }
        private bool _isStep9Done;  public bool IsStep9Done  { get => _isStep9Done;  set => SetProperty(ref _isStep9Done,  value); }
        private bool _isStep10Done; public bool IsStep10Done { get => _isStep10Done; set => SetProperty(ref _isStep10Done, value); }

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        // ── Project Setup commands ────────────────────────────────────────────
        public ICommand OpenProjectInfoCommand          { get; }
        public ICommand ApplyBrowserOrganizationCommand { get; }
        public ICommand CheckRequiredContentCommand     { get; }
        public ICommand SaveAsCommand                   { get; }

        // ── Maintenance commands ──────────────────────────────────────────────
        public ICommand OpenPurgeUnusedCommand  { get; }
        public ICommand ReviewWarningsCommand   { get; }
        public ICommand RunModelAuditCommand    { get; }

        // ── Transfer Standards commands ───────────────────────────────────────
        public ICommand TransferStandardsCommand { get; }
        public ICommand ApplyFromTemplateCommand  { get; }
        public ICommand CopyElementsCommand       { get; }

        // ── Levels commands ───────────────────────────────────────────────────
        public ICommand CreateLevelsCommand { get; }

        // ── Base Views commands ───────────────────────────────────────────────
        public ICommand CreateBaseViewsCommand { get; }

        // ── IFC Linking commands ──────────────────────────────────────────────
        public ICommand LinkIfcFilesCommand { get; }

        // ── DWG Linking commands ─────────────────────────────────────────────
        public ICommand LinkDwgFilesCommand { get; }

        // ── Step progress toggle ──────────────────────────────────────────────
        public ICommand ToggleStepDoneCommand { get; }

        // ── Delegates wired by ProjectSetupWindow ─────────────────────────────
        public Action OpenTransferWindowRequest          { get; set; }
        public Action OpenCopyElementsWindowRequest      { get; set; }
        public Action OpenCreateLevelsWindowRequest      { get; set; }
        public Func<string> RequestFolderPick            { get; set; }
        public Action<List<string>> OpenLinkIfcWindowRequest { get; set; }
        public Action<List<string>> OpenLinkDwgWindowRequest { get; set; }
        /// <summary>Returns a save-file path chosen by the user, or null if cancelled.</summary>
        public Func<string> RequestSaveFilePick          { get; set; }

        /// <summary>Returns the owning Window for result dialogs.</summary>
        public Func<Window> GetOwnerWindow               { get; set; }

        /// <summary>Tracks the active theme so result dialogs match the main window.</summary>
        public bool IsDarkMode                           { get; set; } = true;

        public MainViewModel(RevitExternalEventService eventService)
        {
            _eventService = eventService;

            OpenProjectInfoCommand          = new RelayCommand(_ => RaiseRequest(new ProjectInformationRequest(SetStatus)));
            ApplyBrowserOrganizationCommand = new RelayCommand(_ => SetStatus("Browser Organization: coming in a future update."));
            CheckRequiredContentCommand     = new RelayCommand(_ => SetStatus("Required Content Check: coming in a future update."));
            SaveAsCommand                   = new RelayCommand(_ => ExecuteSaveAs());

            OpenPurgeUnusedCommand  = new RelayCommand(_ => RaiseRequest(new PurgeUnusedRequest(SetStatus)));
            ReviewWarningsCommand   = new RelayCommand(_ => RaiseRequest(new ModelWarningsRequest(SetStatus)));
            RunModelAuditCommand    = new RelayCommand(_ => SetStatus("Model Audit: coming in a future update."), _ => false);

            TransferStandardsCommand = new RelayCommand(_ => OpenTransferWindowRequest?.Invoke());
            ApplyFromTemplateCommand  = new RelayCommand(_ => SetStatus("Apply from Template: coming in a future update."), _ => false);
            CopyElementsCommand       = new RelayCommand(_ => OpenCopyElementsWindowRequest?.Invoke());

            CreateLevelsCommand = new RelayCommand(_ =>
            {
                OpenCreateLevelsWindowRequest?.Invoke();
                MarkStepDone(5);
            });

            CreateBaseViewsCommand = new RelayCommand(_ => ExecuteCreateBaseViews());

            LinkIfcFilesCommand = new RelayCommand(_ => PickFolderAndLinkIfc());
            LinkDwgFilesCommand  = new RelayCommand(_ => PickFolderAndLinkDwg());

            ToggleStepDoneCommand = new RelayCommand(p =>
            {
                if (p is int step) ToggleStep(step);
            });

            // Read persisted progress from Extensible Storage on construction
            _eventService.Raise(new ReadSetupProgressRequest(OnProgressLoaded));
        }

        // ── Step progress helpers ─────────────────────────────────────────────

        private void OnProgressLoaded(HashSet<int> completed)
        {
            _completedSteps.Clear();
            foreach (var n in completed) _completedSteps.Add(n);
            ApplyStepBoolsFromSet();
        }

        private void ApplyStepBoolsFromSet()
        {
            IsStep1Done  = _completedSteps.Contains(1);
            IsStep2Done  = _completedSteps.Contains(2);
            IsStep3Done  = _completedSteps.Contains(3);
            IsStep4Done  = _completedSteps.Contains(4);
            IsStep5Done  = _completedSteps.Contains(5);
            IsStep6Done  = _completedSteps.Contains(6);
            IsStep7Done  = _completedSteps.Contains(7);
            IsStep8Done  = _completedSteps.Contains(8);
            IsStep9Done  = _completedSteps.Contains(9);
            IsStep10Done = _completedSteps.Contains(10);
        }

        private void MarkStepDone(int step)
        {
            if (_completedSteps.Contains(step)) return;
            _completedSteps.Add(step);
            ApplyStepBoolsFromSet();
            _eventService.Raise(new WriteSetupProgressRequest(_completedSteps, SetStatus));
        }

        private void ToggleStep(int step)
        {
            if (_completedSteps.Contains(step))
                _completedSteps.Remove(step);
            else
                _completedSteps.Add(step);

            ApplyStepBoolsFromSet();
            _eventService.Raise(new WriteSetupProgressRequest(_completedSteps, SetStatus));
        }

        // ── Action helpers ────────────────────────────────────────────────────

        private void ExecuteSaveAs()
        {
            var path = RequestSaveFilePick?.Invoke();
            if (string.IsNullOrEmpty(path)) return;

            RaiseRequest(new SaveAsRequest(path, msg =>
            {
                SetStatus(msg);
                if (!msg.StartsWith("Save failed", StringComparison.OrdinalIgnoreCase))
                    MarkStepDone(3);
            }));
        }

        private void ExecuteCreateBaseViews()
        {
            SetStatus("Creating base views\u2026");
            IsBusy = true;
            _eventService.Raise(new CreateBaseViewsRequest(result =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (result.ErrorMessage != null)
                    {
                        SetStatus(result.ErrorMessage);
                        DialogWindow.Show(
                            owner     : GetOwnerWindow?.Invoke(),
                            title     : "Baas vaadete loomine",
                            message   : result.ErrorMessage,
                            iconKind  : "AlertCircleOutline",
                            iconColor : "#f0a040",
                            isDarkMode: IsDarkMode);
                        return;
                    }

                    var parts = new List<string>();
                    if (result.Created > 0) parts.Add($"Loodud: {result.Created}");
                    if (result.Renamed > 0) parts.Add($"Nimetatud \u00fcmber: {result.Renamed}");
                    var summary = parts.Count > 0 ? string.Join(", ", parts) + "." : "Muudatusi ei tehtud.";
                    SetStatus(summary);

                    DialogWindow.Show(
                        owner       : GetOwnerWindow?.Invoke(),
                        title       : "Baas vaadete loomine",
                        message     : summary,
                        detailItems : result.Messages.Count > 0 ? result.Messages : null,
                        iconKind    : "CheckCircleOutline",
                        iconColor   : "#70babc",
                        isDarkMode  : IsDarkMode);

                    MarkStepDone(6);
                });
            }));
        }

        private void PickFolderAndLinkIfc()
        {
            var folder = RequestFolderPick?.Invoke();
            if (string.IsNullOrEmpty(folder)) return;

            var files = new List<string>(Directory.GetFiles(folder, "*.ifc", SearchOption.TopDirectoryOnly));
            if (files.Count == 0)
            {
                SetStatus("No IFC files found in the selected folder.");
                return;
            }

            OpenLinkIfcWindowRequest?.Invoke(files);
            MarkStepDone(4);
        }

        private void PickFolderAndLinkDwg()
        {
            var folder = RequestFolderPick?.Invoke();
            if (string.IsNullOrEmpty(folder)) return;

            var files = new List<string>(Directory.GetFiles(folder, "*.dwg", SearchOption.TopDirectoryOnly));
            if (files.Count == 0)
            {
                SetStatus("DWG faile valitud kaustas ei leitud.");
                return;
            }

            OpenLinkDwgWindowRequest?.Invoke(files);
            MarkStepDone(7);
        }

        private void RaiseRequest(IExternalEventRequest request)
        {
            SetStatus("Working...");
            IsBusy = true;
            try
            {
                _eventService.Raise(request);
            }
            catch (Exception ex)
            {
                SetStatus($"Error: {ex.Message}");
                IsBusy = false;
            }
        }

        private void SetStatus(string message)
        {
            IsBusy = false;
            StatusMessage = message;
        }
    }
}