using System;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Xaml.Behaviors;
using Telerik.Windows.Controls;
using Telerik.Windows.Controls.Docking;

namespace Desktop.ImportTool.Services
{
    public class DockingBehavior : Behavior<RadDocking>
    {
        public static DockingBehavior Instance { get; private set; }

        public static readonly DependencyProperty PaneIdProperty =
            DependencyProperty.RegisterAttached("PaneId", typeof(string), typeof(DockingBehavior), new PropertyMetadata(null));

        public static void SetPaneId(DependencyObject element, string value) => element.SetValue(PaneIdProperty, value);
        public static string GetPaneId(DependencyObject element) => (string)element.GetValue(PaneIdProperty);

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
            Instance = this;
            if (AssociatedObject != null)
                AssociatedObject.Loaded += AssociatedObject_Loaded;
            Application.Current.Exit += Application_Exit;
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
            if (AssociatedObject != null)
                AssociatedObject.Loaded -= AssociatedObject_Loaded;
            Application.Current.Exit -= Application_Exit;
            if (Instance == this) Instance = null;
        }

        private void AssociatedObject_Loaded(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(_layoutFilePath)) return;
            try
            {
                var bytes = File.ReadAllBytes(_layoutFilePath);
                using (var ms = new MemoryStream(bytes))
                {
                    var mi = AssociatedObject.GetType().GetMethod("LoadLayout", new[] { typeof(Stream) })
                             ?? AssociatedObject.GetType().GetMethod("RestoreLayout", new[] { typeof(Stream) });
                    if (mi != null) { ms.Position = 0; mi.Invoke(AssociatedObject, new object[] { ms }); return; }

                    ms.Position = 0;
                    var xml = new StreamReader(ms).ReadToEnd();
                    mi = AssociatedObject.GetType().GetMethod("LoadLayout", new[] { typeof(string) })
                         ?? AssociatedObject.GetType().GetMethod("RestoreLayout", new[] { typeof(string) });
                    mi?.Invoke(AssociatedObject, new object[] { xml });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("DockingBehavior.Restore error: " + ex);
            }
        }

        private void Application_Exit(object sender, ExitEventArgs e)
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
                System.Diagnostics.Debug.WriteLine("DockingBehavior.Save error: " + ex);
            }
        }

        public bool SetPaneVisibilityById(string paneId, bool visible)
        {
            if (AssociatedObject == null || string.IsNullOrWhiteSpace(paneId)) return false;

            var pane = AssociatedObject.Items.OfType<object>().OfType<RadPane>().FirstOrDefault(p => string.Equals(GetPaneId(p), paneId, StringComparison.OrdinalIgnoreCase));
            if (pane == null)
            {
                try { pane = AssociatedObject.Descendents().OfType<RadPane>().FirstOrDefault(p => string.Equals(GetPaneId(p), paneId, StringComparison.OrdinalIgnoreCase)); }
                catch { }
            }
            if (pane == null) return false;

            try
            {
                if (visible)
                {
                    pane.GetType().GetMethod("Show")?.Invoke(pane, null);
                    return true;
                }
                pane.GetType().GetMethod("Hide")?.Invoke(pane, null);
                var isHidden = pane.GetType().GetProperty("IsHidden");
                if (isHidden != null && isHidden.CanWrite) { isHidden.SetValue(pane, true); return true; }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("SetPaneVisibilityById error: " + ex);
            }
            return false;
        }

        // Optional API for explicit save
        public void SaveLayout()
        {
            Application_Exit(this, null);
        }
    }

    internal static class RadDockingExtensions
    {
        public static System.Collections.Generic.IEnumerable<object> Descendents(this RadDocking docking)
        {
            foreach (var item in docking.Items) yield return item;
        }
    }
}