using ProjectSetup.Services.Revit;
using System;
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

        /// <summary>
        /// Wired up by ProjectSetupWindow to open the Transfer Standards browser window.
        /// </summary>
        public Action OpenTransferWindowRequest      { get; set; }

        /// <summary>
        /// Wired up by ProjectSetupWindow to open the Copy Elements browser window.
        /// </summary>
        public Action OpenCopyElementsWindowRequest { get; set; }

        /// <summary>
        /// Wired up by ProjectSetupWindow to open the Create Levels window.
        /// </summary>
        public Action OpenCreateLevelsWindowRequest { get; set; }

        public MainViewModel(RevitExternalEventService eventService)
        {
            _eventService = eventService;

            OpenProjectInfoCommand          = new RelayCommand(_ => RaiseRequest(new ProjectInformationRequest(SetStatus)));
            ApplyBrowserOrganizationCommand = new RelayCommand(_ => SetStatus("Browser Organization: coming in a future update."));
            CheckRequiredContentCommand     = new RelayCommand(_ => SetStatus("Required Content Check: coming in a future update."));

            OpenPurgeUnusedCommand  = new RelayCommand(_ => RaiseRequest(new PurgeUnusedRequest(SetStatus)));
            ReviewWarningsCommand   = new RelayCommand(_ => RaiseRequest(new ModelWarningsRequest(SetStatus)));
            RunModelAuditCommand    = new RelayCommand(_ => SetStatus("Model Audit: coming in a future update."), _ => false);

            TransferStandardsCommand = new RelayCommand(_ => OpenTransferWindowRequest?.Invoke());
            ApplyFromTemplateCommand  = new RelayCommand(_ => SetStatus("Apply from Template: coming in a future update."), _ => false);
            CopyElementsCommand       = new RelayCommand(_ => OpenCopyElementsWindowRequest?.Invoke());
            CreateLevelsCommand       = new RelayCommand(_ => OpenCreateLevelsWindowRequest?.Invoke());
        }

        private void RaiseRequest(IExternalEventRequest request)
        {
            SetStatus("Working...");
            IsBusy = true;
            try
            {
                _eventService.Raise(request);
                // IsBusy is cleared once the external event callback runs SetStatus
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