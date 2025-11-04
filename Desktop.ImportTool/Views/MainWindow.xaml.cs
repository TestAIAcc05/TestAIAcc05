using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using System.Collections.Generic;
using Telerik.Windows.Controls;
using Telerik.Windows.Controls.Docking;
using Desktop.ImportTool.ViewModels;

namespace Desktop.ImportTool.Views
{
    public partial class MainWindow : Window
    {
        private DockingService _dockingService;
        private const string PersistFile = "panePositions.cfg";

        public MainWindow()
        {
            InitializeComponent();

            // Avoid biasing DockingService with a single group - pass null
            _dockingService = new DockingService(this, MainDocking, null);

            DataContext = new MainWindowViewModel(_dockingService);

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // We no longer rely on RadDocking.LoadLayout for persistence. Use our simple layout.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try { DeduplicateLayout(); } catch { }
                try { LoadSimpleLayoutAndRestorePanes(); } catch { RestorePaneContents(); }
                try { MainDocking.UpdateLayout(); } catch { }
            }), DispatcherPriority.Loaded);
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            try
            {
                // Make sure logical tree is stabilized and we dedupe before save
                try { DeduplicateLayout(); } catch { }
                try { RestorePaneContents(); } catch { }
                try { MainDocking.UpdateLayout(); } catch { }

                // Persist our small layout: open flags and indices
                try { SaveSimpleLayout(); } catch { }
            }
            catch { }
        }

        // ---------- SIMPLE LAYOUT PERSISTENCE ----------
        // Format:
        // TasksIndex=0
        // TasksOpen=1
        // HistoryIndex=1
        // HistoryOpen=1

        private void SaveSimpleLayout()
        {
            try
            {
                var split = MainDocking.GetAllChildren().OfType<RadSplitContainer>().FirstOrDefault();
                var groupsInSplit = split?.Items.OfType<RadPaneGroup>().ToList() ?? MainDocking.GetAllChildren().OfType<RadPaneGroup>().ToList();

                bool tasksOpen = false, historyOpen = false;
                int? tasksIndex = null, historyIndex = null;

                var tasksPane = FindAnyPaneByKey("Tasks");
                var historyPane = FindAnyPaneByKey("History");

                if (tasksPane != null)
                {
                    tasksOpen = true;
                    var parent = tasksPane.Parent as RadPaneGroup;
                    if (parent != null) tasksIndex = groupsInSplit.IndexOf(parent);
                }

                if (historyPane != null)
                {
                    historyOpen = true;
                    var parent = historyPane.Parent as RadPaneGroup;
                    if (parent != null) historyIndex = groupsInSplit.IndexOf(parent);
                }

                var lines = new List<string>
                {
                    $"TasksIndex={(tasksIndex.HasValue ? tasksIndex.Value.ToString() : "0")}",
                    $"TasksOpen={(tasksOpen ? "1" : "0")}",
                    $"HistoryIndex={(historyIndex.HasValue ? historyIndex.Value.ToString() : "1")}",
                    $"HistoryOpen={(historyOpen ? "1" : "0")}"
                };

                File.WriteAllLines(PersistFile, lines);
            }
            catch { /* ignore persistence errors */ }
        }

        private void LoadSimpleLayoutAndRestorePanes()
        {
            // Deduplicate in case there are leftover duplicates.
            try { DeduplicateLayout(); } catch { }

            int tasksIndex = 0, historyIndex = 1;
            bool tasksOpen = true, historyOpen = true;
            if (File.Exists(PersistFile))
            {
                try
                {
                    var lines = File.ReadAllLines(PersistFile);
                    foreach (var line in lines)
                    {
                        var parts = line.Split(new[] { '=' }, 2);
                        if (parts.Length != 2) continue;
                        var key = parts[0].Trim();
                        var val = parts[1].Trim();
                        if (string.Equals(key, "TasksIndex", StringComparison.OrdinalIgnoreCase)) int.TryParse(val, out tasksIndex);
                        if (string.Equals(key, "HistoryIndex", StringComparison.OrdinalIgnoreCase)) int.TryParse(val, out historyIndex);
                        if (string.Equals(key, "TasksOpen", StringComparison.OrdinalIgnoreCase)) tasksOpen = val == "1";
                        if (string.Equals(key, "HistoryOpen", StringComparison.OrdinalIgnoreCase)) historyOpen = val == "1";
                    }
                }
                catch { /* ignore parse errors */ }
            }

            // Ensure split and enough groups exist
            var split = MainDocking.GetAllChildren().OfType<RadSplitContainer>().FirstOrDefault();
            if (split == null)
            {
                split = new RadSplitContainer();
                try { MainDocking.Items.Add(split); } catch { }
            }

            int neededGroups = Math.Max(tasksIndex, historyIndex) + 1;
            for (int i = 0; i < neededGroups; i++)
            {
                if (split.Items.OfType<RadPaneGroup>().ElementAtOrDefault(i) == null)
                {
                    var g = new RadPaneGroup();
                    try { split.Items.Add(g); } catch { }
                }
            }

            var groupsList = split.Items.OfType<RadPaneGroup>().ToList();
            var vm = DataContext as MainWindowViewModel;
            if (vm == null) return;

            // Helper to remove all panes for a key (used when persisted Open=false)
            void RemoveAllPanesForKey(string key)
            {
                try
                {
                    var matches = MainDocking.GetAllChildren().OfType<RadPane>().Where(p =>
                    {
                        var t = (RadDocking.GetSerializationTag(p) ?? p.Header)?.ToString() ?? string.Empty;
                        var h = (p.Header ?? string.Empty).ToString() ?? string.Empty;
                        return string.Equals(t, key, StringComparison.OrdinalIgnoreCase) || string.Equals(h, key, StringComparison.OrdinalIgnoreCase);
                    }).ToList();

                    foreach (var extra in matches)
                    {
                        try { if (extra.Parent is RadPaneGroup pg) pg.Items.Remove(extra); } catch { }
                    }
                }
                catch { }
            }

            // If persisted closed, ensure no placeholder exists for that key
            if (!tasksOpen) RemoveAllPanesForKey("Tasks");
            if (!historyOpen) RemoveAllPanesForKey("History");

            // Now for each pane that should be open, ensure exactly one canonical pane exists and attach VM view.
            // TASKS
            if (tasksOpen)
            {
                try
                {
                    // Try to find an existing canonical pane
                    var existing = FindCanonicalPaneByTagOrHeader("Tasks");

                    if (existing != null)
                    {
                        // Attach VM view instance to it (ensures it's not empty)
                        if (!object.ReferenceEquals(existing.Content, vm.TasksView))
                            existing.Content = vm.TasksView;
                        TrySetSerializationTag(existing, "Tasks");

                        // Move into saved group if available; otherwise keep where it was
                        var targetGroup = groupsList.ElementAtOrDefault(tasksIndex) ?? groupsList.First();
                        MovePaneToGroupIfNotAlready(existing, targetGroup, "Tasks");
                    }
                    else
                    {
                        // Not found: create exactly one placeholder in target group and attach content
                        var tg = groupsList.ElementAtOrDefault(tasksIndex) ?? groupsList.First();
                        var newPane = this.FindName("TasksPane") as RadPane ?? new RadPane { Header = "Tasks", CanUserClose = true };
                        if (!object.ReferenceEquals(newPane.Content, vm.TasksView))
                            newPane.Content = vm.TasksView;
                        TrySetSerializationTag(newPane, "Tasks");
                        if (!tg.Items.OfType<object>().Any(x => object.ReferenceEquals(x, newPane)))
                        {
                            try { tg.Items.Insert(0, newPane); } catch { try { tg.Items.Add(newPane); } catch { } }
                        }
                    }
                }
                catch { }
            }

            // HISTORY
            if (historyOpen)
            {
                try
                {
                    var existing = FindCanonicalPaneByTagOrHeader("History");

                    if (existing != null)
                    {
                        if (!object.ReferenceEquals(existing.Content, vm.HistoryView))
                            existing.Content = vm.HistoryView;
                        TrySetSerializationTag(existing, "History");

                        var targetGroup = groupsList.ElementAtOrDefault(historyIndex) ?? (groupsList.Count > 1 ? groupsList[1] : groupsList.First());
                        MovePaneToGroupIfNotAlready(existing, targetGroup, "History");
                    }
                    else
                    {
                        var hg = groupsList.ElementAtOrDefault(historyIndex) ?? (groupsList.Count > 1 ? groupsList[1] : groupsList.First());
                        var newPane = this.FindName("HistoryPane") as RadPane ?? new RadPane { Header = "History", CanUserClose = true };
                        if (!object.ReferenceEquals(newPane.Content, vm.HistoryView))
                            newPane.Content = vm.HistoryView;
                        TrySetSerializationTag(newPane, "History");
                        if (!hg.Items.OfType<object>().Any(x => object.ReferenceEquals(x, newPane)))
                        {
                            try { hg.Items.Insert(0, newPane); } catch { try { hg.Items.Add(newPane); } catch { } }
                        }
                    }
                }
                catch { }
            }
        }

        // Find any pane with matching tag/header (first found)
        private RadPane FindAnyPaneByKey(string paneKey)
        {
            try
            {
                var all = MainDocking.GetAllChildren().OfType<RadPane>();
                foreach (var p in all)
                {
                    try
                    {
                        var tag = (RadDocking.GetSerializationTag(p) ?? p.Header)?.ToString() ?? string.Empty;
                        var hdr = (p.Header ?? string.Empty).ToString() ?? string.Empty;
                        if (string.Equals(tag, paneKey, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(hdr, paneKey, StringComparison.OrdinalIgnoreCase))
                            return p;
                    }
                    catch { }
                }
            }
            catch { }
            return null;
        }

        // RestorePaneContents kept as defensive fallback (attach VM views to any restored panes)
        private void RestorePaneContents()
        {
            var vm = DataContext as MainWindowViewModel;
            if (vm == null) return;

            try
            {
                var all = MainDocking.GetAllChildren().OfType<RadPane>().ToList();
                foreach (var pane in all)
                {
                    try
                    {
                        var headerText = (pane.Header ?? string.Empty).ToString();
                        var serTag = (RadDocking.GetSerializationTag(pane) ?? string.Empty).ToString();

                        if (string.Equals(headerText, "Tasks", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(serTag, "Tasks", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!object.ReferenceEquals(pane.Content, vm.TasksView))
                                pane.Content = vm.TasksView;
                            TrySetSerializationTag(pane, "Tasks");
                        }
                        else if (string.Equals(headerText, "History", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(serTag, "History", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!object.ReferenceEquals(pane.Content, vm.HistoryView))
                                pane.Content = vm.HistoryView;
                            TrySetSerializationTag(pane, "History");
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        // Add this method inside the MainWindow class (near other helper methods)
        private RadPane FindCanonicalPaneByTagOrHeader(string paneKey)
        {
            if (string.IsNullOrWhiteSpace(paneKey)) return null;

            try
            {
                var panes = MainDocking.GetAllChildren().OfType<RadPane>().Where(p =>
                {
                    try
                    {
                        var tag = (RadDocking.GetSerializationTag(p) ?? p.Header)?.ToString() ?? string.Empty;
                        var hdr = (p.Header ?? string.Empty).ToString() ?? string.Empty;
                        return string.Equals(tag, paneKey, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(hdr, paneKey, StringComparison.OrdinalIgnoreCase);
                    }
                    catch { return false; }
                }).ToList();

                if (!panes.Any()) return null;

                // 1) prefer XAML-named pane inside named group
                try
                {
                    var namedPane = this.FindName(paneKey + "Pane") as RadPane;
                    var namedGroup = this.FindName(paneKey + "PaneGroup") as RadPaneGroup;
                    if (namedPane != null && namedGroup != null)
                    {
                        if (panes.Any(p => object.ReferenceEquals(p, namedPane) && p.Parent is RadPaneGroup parent && object.ReferenceEquals(parent, namedGroup)))
                            return panes.First(p => object.ReferenceEquals(p, namedPane));
                    }
                }
                catch { }

                // 2) prefer first non-hidden pane
                var nonHidden = panes.FirstOrDefault(p =>
                {
                    try { return !p.IsHidden; } catch { return true; }
                });
                if (nonHidden != null) return nonHidden;

                // 3) fallback: first
                return panes.First();
            }
            catch { }
            return null;
        }

        // Deduplicate panes with same tag/header, keeping one canonical instance
        private void DeduplicateLayout()
        {
            try
            {
                var panes = MainDocking.GetAllChildren().OfType<RadPane>().ToList();
                if (!panes.Any()) return;
                var keys = new[] { "Tasks", "History" };

                foreach (var key in keys)
                {
                    var matches = panes.Where(p =>
                    {
                        var tag = (RadDocking.GetSerializationTag(p) ?? p.Header)?.ToString() ?? string.Empty;
                        var hdr = (p.Header ?? string.Empty).ToString() ?? string.Empty;
                        return string.Equals(tag, key, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(hdr, key, StringComparison.OrdinalIgnoreCase);
                    }).ToList();

                    if (matches.Count <= 1) continue;

                    RadPane keeper = null;

                    try
                    {
                        var namedPane = this.FindName(key + "Pane") as RadPane;
                        var namedGroup = this.FindName(key + "PaneGroup") as RadPaneGroup;
                        if (namedPane != null && namedGroup != null)
                        {
                            if (matches.Any(p => object.ReferenceEquals(p, namedPane) && p.Parent is RadPaneGroup parent && object.ReferenceEquals(parent, namedGroup)))
                                keeper = matches.First(p => object.ReferenceEquals(p, namedPane));
                        }
                    }
                    catch { }

                    if (keeper == null)
                        keeper = matches.FirstOrDefault(p => { try { return !p.IsHidden; } catch { return true; } });

                    if (keeper == null)
                        keeper = matches.First();

                    foreach (var extra in matches.Where(p => !object.ReferenceEquals(p, keeper)).ToList())
                    {
                        try { if (extra.Parent is RadPaneGroup parent) parent.Items.Remove(extra); } catch { }
                    }

                    panes = MainDocking.GetAllChildren().OfType<RadPane>().ToList();
                }
            }
            catch { }
        }

        private static void TrySetSerializationTag(RadPane pane, string tag)
        {
            try { RadDocking.SetSerializationTag(pane, tag); } catch { }
        }

        private void MovePaneToGroupIfNotAlready(RadPane pane, RadPaneGroup targetGroup, string tag)
        {
            try
            {
                if (pane == null || targetGroup == null) return;
                var currentParent = pane.Parent as RadPaneGroup;
                if (object.ReferenceEquals(currentParent, targetGroup)) return;
                try { currentParent?.Items.Remove(pane); } catch { }
                try { targetGroup.Items.Insert(0, pane); } catch { try { targetGroup.Items.Add(pane); } catch { } }
                TrySetSerializationTag(pane, tag);
            }
            catch { }
        }
    }
}