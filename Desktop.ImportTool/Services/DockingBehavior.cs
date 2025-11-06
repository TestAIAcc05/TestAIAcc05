// Paste this file into your project under the namespace Desktop.ImportTool.Services
// If your project uses the older Blend SDK interactivity, change the using to System.Windows.Interactivity.
// Make sure the file's Build Action is "Compile" and the namespace matches the xmlns in your XAML.
using System;
using System.Linq;
using System.Windows;
using System.Windows.Interactivity;
using Telerik.Windows.Controls;
using Telerik.Windows.Controls.Docking;

namespace Desktop.ImportTool.Services
{
    // Minimal DockingBehavior that exposes an attached PaneId property (for XAML usage
    // like svc:DockingBehavior.PaneId="Tasks") and a small helper to show/hide panes by that PaneId.
    public class DockingBehavior : Behavior<RadDocking>
    {
        // Attached property used on RadPane (or any element) to give it a logical id.
        // Usage: svc:DockingBehavior.PaneId="Tasks"
        public static readonly DependencyProperty PaneIdProperty =
            DependencyProperty.RegisterAttached(
                "PaneId",
                typeof(string),
                typeof(DockingBehavior),
                new PropertyMetadata(null));

        public static void SetPaneId(DependencyObject element, string value)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));
            element.SetValue(PaneIdProperty, value);
        }

        public static string GetPaneId(DependencyObject element)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));
            return (string)element.GetValue(PaneIdProperty);
        }

        // Example small lifecycle handling (load/exit saving omitted here to keep class minimal).
        protected override void OnAttached()
        {
            base.OnAttached();
            // You can hook events here if you need to auto-save/restore etc.
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
        }

        // Public helper to show/hide a pane by PaneId (used by your viewmodel/service).
        // Returns true if an operation was performed.
        public bool SetPaneVisibilityById(string paneId, bool visible)
        {
            if (AssociatedObject == null || string.IsNullOrWhiteSpace(paneId))
                return false;

            var pane = FindPaneById(paneId);
            if (pane == null) return false;

            try
            {
                if (visible)
                {
                    var show = pane.GetType().GetMethod("Show");
                    if (show != null) { show.Invoke(pane, null); return true; }

                    // fallback: try IsHidden property
                    var isHiddenProp = pane.GetType().GetProperty("IsHidden");
                    if (isHiddenProp != null && isHiddenProp.CanWrite) { isHiddenProp.SetValue(pane, false); return true; }
                }
                else
                {
                    var hide = pane.GetType().GetMethod("Hide");
                    if (hide != null) { hide.Invoke(pane, null); return true; }

                    var isHiddenProp = pane.GetType().GetProperty("IsHidden");
                    if (isHiddenProp != null && isHiddenProp.CanWrite) { isHiddenProp.SetValue(pane, true); return true; }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DockingBehavior.SetPaneVisibilityById error: {ex}");
            }

            return false;
        }

        // Find a RadPane that has svc:DockingBehavior.PaneId == paneId
        private RadPane FindPaneById(string paneId)
        {
            // First check straight Panes collection (fast)
            foreach (var p in AssociatedObject.Panes())
            {
                try
                {
                    var rp = p as RadPane;
                    if (rp == null) continue;
                    var id = GetPaneId(rp);
                    if (string.Equals(id, paneId, StringComparison.OrdinalIgnoreCase))
                        return rp;
                }
                catch { /* ignore malformed items */ }
            }

            // Fall back to deeper search: descend visual/logical tree of docking to find RadPane elements
            var all = AssociatedObject.Descendents().OfType<RadPane>();
            foreach (var rp in all)
            {
                try
                {
                    var id = GetPaneId(rp);
                    if (string.Equals(id, paneId, StringComparison.OrdinalIgnoreCase))
                        return rp;
                }
                catch { }
            }

            return null;
        }
    }

    // Simple extension helpers used above (keep minimal)
    internal static class RadDockingHelpers
    {
        public static System.Collections.Generic.IEnumerable<object> Descendents(this RadDocking docking)
        {
            // minimal safe enumeration of docking.Items
            foreach (var item in docking.Items) yield return item;
        }

        public static System.Collections.Generic.IEnumerable<RadPane> Panes(this RadDocking docking)
        {
            foreach (var item in docking.Items)
            {
                if (item is RadPane rp) yield return rp;
            }
        }
    }
}