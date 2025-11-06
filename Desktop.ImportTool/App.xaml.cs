using System.Windows;
using Desktop.ImportTool.Services;
using Telerik.Windows.Controls;

namespace Desktop.ImportTool
{
    // Safe, minimally-invasive App startup that will attach DockingIntegration without
    // destroying existing startup logic or overwriting your existing MainViewModel if present.
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // If your App.xaml includes StartupUri="Views/MainWindow.xaml", the MainWindow instance
            // may already be created and assigned to Application.Current.MainWindow by base.OnStartup.
            // Otherwise, create it here.
            var main = Application.Current.MainWindow;
            if (main == null)
            {
                main = new Views.MainWindow();
                Application.Current.MainWindow = main;
                // Note: do not set DataContext here; preserve your existing DataContext wiring.
                main.Show();
            }

            // Try to attach docking integration. This will:
            // - create DockingLayoutService
            // - store it in Application.Current.Resources["DockingLayoutService"]
            // - call InitializeDocking on your MainViewModel if present
            // - load layout and hook shutdown saving
            try
            {
                DockingIntegration.AttachAndInitialize(main);
            }
            catch (System.Exception ex)
            {
                // Do not crash the app if docking integration fails — log for diagnostics.
                System.Diagnostics.Debug.WriteLine($"DockingIntegration failed to attach: {ex}");
                // Optionally rethrow if you prefer a fail-fast behaviour.
                // throw;
            }
        }
    }
}