// url= (create/replace this file in your project at Desktop.ImportTool/Infrastructure/IDockingService.cs)
using System;

namespace Desktop.ImportTool.Infrastructure
{
    // Simple event args to notify when a pane opens/closes
    public class PaneStateChangedEventArgs : EventArgs
    {
        public string PaneKey { get; }
        public bool IsOpen { get; }

        public PaneStateChangedEventArgs(string paneKey, bool isOpen)
        {
            PaneKey = paneKey;
            IsOpen = isOpen;
        }
    }

    // Minimal docking service interface used by the VM.
    // We add IsPaneOpen and a PaneStateChanged event so the VM can disable commands.
    public interface IDockingService
    {
        void OpenPane(string paneKey);

        // Returns true if a pane for the given key currently exists (docked or undocked).
        bool IsPaneOpen(string paneKey);

        // Raised when a pane is opened or closed. PaneKey is the key (e.g. "Tasks" / "History"), IsOpen indicates open state.
        event EventHandler<PaneStateChangedEventArgs> PaneStateChanged;
    }
}