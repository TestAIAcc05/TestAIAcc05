// Minimal DockingBehavior for RadDocking.
// Put this file into namespace Desktop.ImportTool.Services (same assembly as MainWindow XAML).
// If your project uses System.Windows.Interactivity instead of Microsoft.Xaml.Behaviors, change the using.
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Interactivity;
using Telerik.Windows.Controls;
using Telerik.Windows.Controls.Docking;

namespace Desktop.ImportTool.Services
{
    public class DockingBehavior : Behavior<RadDocking>
    {
        // Singleton instance so ViewModels/services can call into the attached behavior without changing DI.
        // This is intentionally simple and small to avoid invasive refactors.
        public static DockingBehavior Instance { get; private set; }

        // Attached PaneId so XAML can mark panes: svc:DockingBehavior.PaneId="Tasks"
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

        readonly string _layoutFilePath;

        public DockingBehavior()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                   AppDomain.CurrentDomain.FriendlyName ?? "ImportTool");
            Directory.CreateDirectory(dir);
            _layoutFilePath = Path.Combine(dir, "RadDockingLayout.xml");
        }

        protected override void OnAttached()
        {
            base.OnAttached();

            // If there's already an instance registered, replace it (single-instance behavior for simplicity).
            Instance = this;

            AssociatedObject.Loaded += OnLoaded;
            Application.Current.Exit += OnAppExit;
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();

            if (AssociatedObject != null)
                AssociatedObject.Loaded -= OnLoaded;

            Application.Current.Exit -= OnAppExit;

            // clear singleton if this instance is being removed
            if (Instance == this) Instance = null;
        }

        void OnLoaded(object s, RoutedEventArgs e)
        {
            // Attempt to restore saved layout
            if (!File.Exists(_layoutFilePath)) return;

            try
            {
                var bytes = File.ReadAllBytes(_layoutFilePath);
                using (var ms = new MemoryStream(bytes))
                {
                    // First try API that accepts Stream
                    var mi = AssociatedObject.GetType().GetMethod("LoadLayout", new[] { typeof(Stream) })
                             ?? AssociatedObject.GetType().GetMethod("RestoreLayout", new[] { typeof(Stream) });

                    if (mi != null)
                    {
                        ms.Position = 0;
                        mi.Invoke(AssociatedObject, new object[] { ms });
                        return;
                    }

                    // Fallback to string xml variant
                    ms.Position = 0;
                    var xml = new StreamReader(ms).ReadToEnd();
                    mi = AssociatedObject.GetType().GetMethod("LoadLayout", new[] { typeof(string) })
                         ?? AssociatedObject.GetType().GetMethod("RestoreLayout", new[] { typeof(string) });
                    mi?.Invoke(AssociatedObject, new object[] { xml });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("DockingBehavior: restore failed: " + ex);
            }
        }

        void OnAppExit(object s, ExitEventArgs e)
        {
            // Save layout on exit
            try
            {
                using (var ms = new MemoryStream())
                {
                    var mi = AssociatedObject.GetType().GetMethod("SaveLayout", new[] { typeof(Stream) })
                             ?? AssociatedObject.GetType().GetMethod("Save", new[] { typeof(Stream) });

                    if (mi != null)
                    {
                        mi.Invoke(AssociatedObject, new object[] { ms });
                        File.WriteAllBytes(_layoutFilePath, ms.ToArray());
                        return;
                    }

                    // fallback to SaveLayout() => string
                    mi = AssociatedObject.GetType().GetMethod("SaveLayout", Type.EmptyTypes)
                         ?? AssociatedObject.GetType().GetMethod("Save", Type.EmptyTypes);
                    if (mi != null && mi.ReturnType == typeof(string))
                    {
                        var xml = mi.Invoke(AssociatedObject, null) as string;
                        if (!string.IsNullOrEmpty(xml))
                            File.WriteAllText(_layoutFilePath, xml);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("DockingBehavior: save failed: " + ex);
            }
        }

        // Public helper to show/hide pane by PaneId (used by your ToggleButton property setter)
        public bool SetPaneVisibilityById(string paneId, bool visible)
        {
            if (AssociatedObject == null || string.IsNullOrWhiteSpace(paneId)) return false;

            // try fast search over top-level panes
            var pane = AssociatedObject.Items
                        .OfType<object>()
                        .OfType<RadPane>()
                        .FirstOrDefault(p => string.Equals(GetPaneId(p), paneId, StringComparison.OrdinalIgnoreCase));

            // fallback to deeper logical/visual search
            if (pane == null)
            {
                try
                {
                    pane = AssociatedObject.Descendents()
                           .OfType<RadPane>()
                           .FirstOrDefault(p => string.Equals(GetPaneId(p), paneId, StringComparison.OrdinalIgnoreCase));
                }
                catch { /* ignore */ }
            }

            if (pane == null) return false;

            try
            {
                if (visible)
                {
                    var mi = pane.GetType().GetMethod("Show");
                    if (mi != null) { mi.Invoke(pane, null); return true; }
                    var isHidden = pane.GetType().GetProperty("IsHidden");
                    if (isHidden != null && isHidden.CanWrite) { isHidden.SetValue(pane, false); return true; }
                }
                else
                {
                    var mi = pane.GetType().GetMethod("Hide");
                    if (mi != null) { mi.Invoke(pane, null); return true; }
                    var isHidden = pane.GetType().GetProperty("IsHidden");
                    if (isHidden != null && isHidden.CanWrite) { isHidden.SetValue(pane, true); return true; }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("DockingBehavior.SetPaneVisibilityById error: " + ex);
            }

            return false;
        }

        // Optional: call to force immediate save
        public void SaveLayout()
        {
            try
            {
                using (var ms = new MemoryStream())
                {
                    var mi = AssociatedObject.GetType().GetMethod("SaveLayout", new[] { typeof(Stream) })
                             ?? AssociatedObject.GetType().GetMethod("Save", new[] { typeof(Stream) });
                    if (mi != null)
                    {
                        mi.Invoke(AssociatedObject, new object[] { ms });
                        File.WriteAllBytes(_layoutFilePath, ms.ToArray());
                        return;
                    }
                    mi = AssociatedObject.GetType().GetMethod("SaveLayout", Type.EmptyTypes)
                         ?? AssociatedObject.GetType().GetMethod("Save", Type.EmptyTypes);
                    if (mi != null && mi.ReturnType == typeof(string))
                    {
                        var xml = mi.Invoke(AssociatedObject, null) as string;
                        if (!string.IsNullOrEmpty(xml)) File.WriteAllText(_layoutFilePath, xml);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("DockingBehavior.SaveLayout error: " + ex);
            }
        }
    }

    // Minimal extension methods used above. Keep them small to avoid extra references.
    internal static class RadDockingExtensions
    {
        public static System.Collections.Generic.IEnumerable<object> Descendents(this RadDocking docking)
        {
            foreach (var item in docking.Items) yield return item;
        }
    }
}