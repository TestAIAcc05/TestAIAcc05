using System;
using System.Windows;
using Desktop.ImportTool.ViewModels;
using Telerik.Windows.Controls;

namespace Desktop.ImportTool.Services
{
    // Small helper so you don't have to rewrite App.xaml.cs.
    // Call DockingIntegration.AttachAndInitialize(mainWindow) from your existing OnStartup.
    public static class DockingIntegration
    {
        // Attaches a DockingLayoutService to the given window.
        // - Looks up a RadDocking named "RadDocking" in the window.
        // - Stores the service in Application.Current.Resources["DockingLayoutService"].
        // - If the window.DataContext is a MainViewModel (or compatible), calls vm.InitializeDocking(svc).
        // - Calls svc.Initialize() and registers a shutdown handler.
        // Returns the created DockingLayoutService instance.
        public static DockingLayoutService AttachAndInitialize(Window mainWindow)
        {
            if (mainWindow == null) throw new ArgumentNullException(nameof(mainWindow));

            // Find the RadDocking control by x:Name (your MainWindow should have RadDocking named "RadDocking")
            var docking = mainWindow.FindName("RadDocking") as RadDocking;
            if (docking == null)
            {
                // Try to provide a helpful error message rather than failing silently.
                throw new InvalidOperationException("RadDocking named 'RadDocking' not found in MainWindow. Ensure the RadDocking control has x:Name=\"RadDocking\" in your MainWindow.xaml.");
            }

            // Create service and expose to Application resources so DockingBehavior can find it.
            var svc = new DockingLayoutService(docking);
            Application.Current.Resources["DockingLayoutService"] = svc;

            // If DataContext is your MainViewModel and has InitializeDocking, call it.
            if (mainWindow.DataContext is MainViewModel vm)
            {
                vm.InitializeDocking(svc);
            }
            else
            {
                // If there is no DataContext or it's not MainViewModel, try to create a MainViewModel
                // with the svc if the constructor exists. Do this conservatively: do not overwrite an existing DataContext.
                if (mainWindow.DataContext == null)
                {
                    try
                    {
                        var createdVm = new MainViewModel(svc);
                        mainWindow.DataContext = createdVm;
                    }
                    catch
                    {
                        // If creating a new VM fails, do nothing. We do not overwrite existing DataContext.
                    }
                }
            }

            // Initialize the service to load layout/pane states (after panes have been created/registered).
            svc.Initialize();

            // Ensure the service shuts down and saves state on application exit.
            Application.Current.Exit += (s, e) =>
            {
                try { svc.Shutdown(); } catch { /* ignore shutdown errors */ }
            };

            return svc;
        }
    }
}