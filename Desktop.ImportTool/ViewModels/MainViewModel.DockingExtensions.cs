using System.Windows.Input;
using Desktop.ImportTool.Infrastructure;
using Desktop.ImportTool.Services;
//using Desktop.ImportTool.Utilities;

namespace Desktop.ImportTool.ViewModels
{
    // Partial extension: adds only docking-related bits.
    // This file intentionally does not replace your full MainViewModel implementation.
    // If your MainViewModel already exposes TogglePaneCommand or InitializeDocking, do NOT add this file;
    // instead merge the implementation into your existing members.
    public partial class MainViewModel
    {
        private DockingLayoutService dockingService;

        // Command for ToggleButtons in the UI. Will be set in InitializeDocking.
        public ICommand TogglePaneCommand { get; private set; }

        // Call this after DockingLayoutService is created (for example, from App.xaml.cs or DockingIntegration).
        // It wires the TogglePaneCommand and keeps the service reference for toggling panes.
        public void InitializeDocking(DockingLayoutService svc)
        {
            this.dockingService = svc;
            // If you already have a command implementation, prefer using that.
            this.TogglePaneCommand = new RelayCommand(OnTogglePane);
        }

        private void OnTogglePane(object parameter)
        {
            var id = parameter as string;
            if (string.IsNullOrWhiteSpace(id)) return;
            dockingService?.TogglePane(id);
        }
    }
}