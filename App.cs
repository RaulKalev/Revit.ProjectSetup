using Autodesk.Revit.UI;
using ricaun.Revit.UI;
using System;
using System.IO;
using ProjectSetup.Commands;

namespace ProjectSetup
{
    [AppLoader]
    public class App : IExternalApplication
    {
        public static Services.Revit.RevitExternalEventService ExternalEventService { get; private set; }
        public static Services.SettingsService SettingsService { get; private set; }
        public static Services.Core.SessionLogger Logger { get; private set; }
        private RibbonPanel ribbonPanel;

        private static readonly string CrashLogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RK Tools", "ProjectSetup", "crash.log");

        public Result OnStartup(UIControlledApplication application)
        {
            Logger = new Services.Core.SessionLogger();
            ExternalEventService = new Services.Revit.RevitExternalEventService(Logger);
            SettingsService = new Services.SettingsService(Logger);

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                LogCrash("AppDomain.UnhandledException", e.ExceptionObject as Exception);

            string tabName = "RK Tools";
            try { application.CreateRibbonTab(tabName); } catch { }

            ribbonPanel = application.CreateOrSelectPanel(tabName, "Project");
            ribbonPanel.CreatePushButton<ProjectSetupCommand>()
                .SetLargeImage("pack://application:,,,/ProjectSetup;component/Assets/ProjectSetup.tiff")
                .SetText("Project Setup")
                .SetToolTip("Set up and maintain Revit projects.")
                .SetLongDescription("Project Setup provides tools for project configuration, maintenance, and transferring project standards.");

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            ribbonPanel?.Remove();
            return Result.Succeeded;
        }

        public static void LogCrash(string source, Exception ex)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CrashLogPath));
                string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] SOURCE: {source}\n" +
                               $"TYPE: {ex?.GetType().FullName}\n" +
                               $"MESSAGE: {ex?.Message}\n" +
                               $"STACK:\n{ex?.StackTrace}\n" +
                               new string('=', 80) + "\n";
                File.AppendAllText(CrashLogPath, entry);
            }
            catch { }
        }
    }
}