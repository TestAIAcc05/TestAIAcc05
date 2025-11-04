// url=https://github.com/TestAIAcc/SQLiteWithWPF/blob/main/Desktop.ImportTool/Views/DockingService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Telerik.Windows.Controls;
using Telerik.Windows.Controls.Docking;
using Desktop.ImportTool.Infrastructure;

namespace Desktop.ImportTool.Views
{
    public class DockingService : IDockingService, IDisposable
    {
        private readonly Window _owner;
        private readonly RadDocking _docking;
        private readonly RadPaneGroup _toolsGroup;

        private readonly Dictionary<string, RadPane> _panes = new Dictionary<string, RadPane>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, PlacementInfo> _lastPlacement = new Dictionary<string, PlacementInfo>(StringComparer.OrdinalIgnoreCase);

        // Monitoring timer to detect open/close changes robustly.
        private readonly DispatcherTimer _stateTimer;
        private readonly Dictionary<string, bool> _lastIsOpen = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private readonly string[] _paneKeys = new[] { "Tasks", "History" };

        public DockingService(Window owner, RadDocking docking, RadPaneGroup toolsGroup)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _docking = docking ?? throw new ArgumentNullException(nameof(docking));
            _toolsGroup = toolsGroup;

            // Initialize last-known open state for monitored panes
            foreach (var k in _paneKeys) _lastIsOpen[k] = false;

            // Start a timer to monitor pane presence across docked/floating containers.
            _stateTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _stateTimer.Tick += StateTimer_Tick;
            // Initialize state immediately
            UpdatePaneStatesAndRaiseIfChanged();
            _stateTimer.Start();
        }

        public event EventHandler<PaneStateChangedEventArgs> PaneStateChanged;

        private void StateTimer_Tick(object sender, EventArgs e)
        {
            UpdatePaneStatesAndRaiseIfChanged();
        }

        private void UpdatePaneStatesAndRaiseIfChanged()
        {
            try
            {
                foreach (var key in _paneKeys)
                {
                    bool isOpen = false;
                    try { isOpen = IsPaneOpen(key); } catch { isOpen = false; }

                    if (!_lastIsOpen.TryGetValue(key, out var last) || last != isOpen)
                    {
                        _lastIsOpen[key] = isOpen;
                        try { PaneStateChanged?.Invoke(this, new PaneStateChangedEventArgs(key, isOpen)); } catch { }
                    }
                }
            }
            catch { /* swallow monitoring errors */ }
        }

        public void OpenPane(string paneKey)
        {
            if (string.IsNullOrWhiteSpace(paneKey)) return;

            var vm = _owner.DataContext as dynamic; // loose coupling
            if (vm == null) return;

            // Refresh underlying VM data before showing
            try
            {
                if (paneKey.Equals("Tasks", StringComparison.OrdinalIgnoreCase)) vm.TasksVM?.LoadTasks();
                else if (paneKey.Equals("History", StringComparison.OrdinalIgnoreCase)) vm.HistoryVM?.LoadHistory();
            }
            catch { }

            object viewInstance = null;
            try
            {
                if (paneKey.Equals("Tasks", StringComparison.OrdinalIgnoreCase)) viewInstance = vm.TasksView;
                else if (paneKey.Equals("History", StringComparison.OrdinalIgnoreCase)) viewInstance = vm.HistoryView;
            }
            catch { }

            if (viewInstance == null) return;

            // If we have a stored pane reference and it's still present anywhere, select it.
            if (_panes.TryGetValue(paneKey, out var storedPane) && storedPane != null)
            {
                if (IsPanePresentAnywhere(storedPane))
                {
                    SelectPane(storedPane);
                    RaisePaneStateChanged(paneKey, true);
                    return;
                }

                _panes[paneKey] = null;
                storedPane = null;
            }

            // 1) Try to find existing pane by content
            var foundByContent = FindPaneByContent(viewInstance);
            if (foundByContent != null)
            {
                MovePaneToPreferredGroupIfNeeded(foundByContent, paneKey);
                AttachCloseWatcher(foundByContent, paneKey);
                _panes[paneKey] = foundByContent;
                SelectPane(foundByContent);
                RaisePaneStateChanged(paneKey, true);
                return;
            }

            // 2) Try to find canonical pane by tag/header
            var byTag = FindCanonicalPaneByTagOrHeader(paneKey);
            if (byTag != null)
            {
                try { if (!object.ReferenceEquals(byTag.Content, viewInstance)) byTag.Content = viewInstance; } catch { }
                MovePaneToPreferredGroupIfNeeded(byTag, paneKey);
                AttachCloseWatcher(byTag, paneKey);
                _panes[paneKey] = byTag;
                SelectPane(byTag);
                RaisePaneStateChanged(paneKey, true);
                return;
            }

            // 3) Create new pane and insert
            var pane = new RadPane { Header = paneKey, Content = viewInstance, CanUserClose = true };
            try { RadDocking.SetSerializationTag(pane, paneKey); } catch { }

            AttachCloseWatcher(pane, paneKey);

            var hostGroup = ResolveHostGroupForOpen(paneKey) ?? CreateNewPaneGroupAtStart();
            if (hostGroup == null) throw new InvalidOperationException("No RadPaneGroup found to host panes.");

            try { hostGroup.Items.Insert(0, pane); } catch { try { hostGroup.Items.Add(pane); } catch { } }

            _panes[paneKey] = pane;
            RaisePaneStateChanged(paneKey, true);
            SelectPane(pane);
        }

        public bool IsPaneOpen(string paneKey)
        {
            if (string.IsNullOrWhiteSpace(paneKey)) return false;

            // 1) Known runtime pane
            if (_panes.TryGetValue(paneKey, out var p) && p != null && IsPanePresentAnywhere(p))
                return true;

            // 2) Any canonical pane by tag/header inside docking (covers restored/saved)
            if (FindCanonicalPaneByTagOrHeader(paneKey) != null) return true;

            // 3) Search all windows (floating)
            try
            {
                foreach (Window w in Application.Current.Windows)
                {
                    var found = FindPaneInVisualOrLogicalTree(w, paneKey);
                    if (found != null) return true;
                }
            }
            catch { }

            return false;
        }

        // Raise event helper
        private void RaisePaneStateChanged(string paneKey, bool isOpen)
        {
            try
            {
                _lastIsOpen[paneKey] = isOpen;
            }
            catch { }
            try { PaneStateChanged?.Invoke(this, new PaneStateChangedEventArgs(paneKey, isOpen)); } catch { }
        }

        // Watch pane Unloaded / removed as best-effort (kept for immediate reaction)
        private void AttachCloseWatcher(RadPane pane, string paneKey)
        {
            if (pane == null || string.IsNullOrWhiteSpace(paneKey)) return;

            try
            {
                RoutedEventHandler unloaded = null;
                unloaded = (s, e) =>
                {
                    try
                    {
                        if (_panes.ContainsKey(paneKey) && object.ReferenceEquals(_panes[paneKey], pane))
                            _panes[paneKey] = null;
                    }
                    catch { }
                    try { RaisePaneStateChanged(paneKey, false); } catch { }
                    try { pane.Unloaded -= unloaded; } catch { }
                };
                pane.Unloaded += unloaded;
            }
            catch { }

            try
            {
                var parentGroup = pane.Parent as RadPaneGroup;
                if (parentGroup != null && parentGroup.Items is System.Collections.Specialized.INotifyCollectionChanged items)
                {
                    System.Collections.Specialized.NotifyCollectionChangedEventHandler handler = null;
                    handler = (s, e) =>
                    {
                        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove ||
                            e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Replace)
                        {
                            var removed = e.OldItems?.OfType<object>().FirstOrDefault(x => object.ReferenceEquals(x, pane));
                            if (removed != null)
                            {
                                try
                                {
                                    if (_panes.ContainsKey(paneKey) && object.ReferenceEquals(_panes[paneKey], pane))
                                        _panes[paneKey] = null;
                                }
                                catch { }
                                try { RaisePaneStateChanged(paneKey, false); } catch { }
                                try { items.CollectionChanged -= handler; } catch { }
                            }
                        }
                    };
                    items.CollectionChanged += handler;
                }
            }
            catch { }
        }

        // Utility helpers (unchanged, robust lookups)

        private RadPane CreateOrFindPaneForKey(string paneKey, object viewInstance)
        {
            var byContent = FindPaneByContent(viewInstance);
            if (byContent != null) return byContent;
            var canonical = FindCanonicalPaneByTagOrHeader(paneKey);
            if (canonical != null) return canonical;
            // no existing - create new
            var pane = new RadPane { Header = paneKey, Content = viewInstance, CanUserClose = true };
            try { RadDocking.SetSerializationTag(pane, paneKey); } catch { }
            return pane;
        }

        private RadPaneGroup CreateNewPaneGroupAtStart()
        {
            try
            {
                var split = _docking.GetAllChildren().OfType<RadSplitContainer>().FirstOrDefault();
                if (split == null)
                {
                    split = new RadSplitContainer();
                    try { _docking.Items.Add(split); } catch { }
                }
                var created = new RadPaneGroup();
                try { split.Items.Insert(0, created); } catch { try { split.Items.Add(created); } catch { } }
                return created;
            }
            catch { return null; }
        }

        private void MovePaneToPreferredGroupIfNeeded(RadPane pane, string paneKey)
        {
            if (pane == null || string.IsNullOrWhiteSpace(paneKey)) return;
            try
            {
                RadPaneGroup preferred = null;
                if (_owner != null)
                {
                    preferred = _owner.FindName(paneKey + "PaneGroup") as RadPaneGroup;
                    if (preferred != null)
                    {
                        if (!_docking.GetAllChildren().OfType<RadPaneGroup>().Any(g => object.ReferenceEquals(g, preferred)))
                            preferred = null;
                    }
                }

                if (preferred != null)
                {
                    var current = pane.Parent as RadPaneGroup;
                    if (!object.ReferenceEquals(current, preferred))
                    {
                        try { current?.Items.Remove(pane); } catch { }
                        try { preferred.Items.Insert(0, pane); } catch { try { preferred.Items.Add(pane); } catch { } }
                        try { RadDocking.SetSerializationTag(pane, paneKey); } catch { }
                    }
                }
            }
            catch { }
        }

        private RadPane FindCanonicalPaneByTagOrHeader(string paneKey)
        {
            if (string.IsNullOrWhiteSpace(paneKey)) return null;
            try
            {
                var panes = _docking.GetAllChildren().OfType<RadPane>().Where(p =>
                {
                    try
                    {
                        var tag = (RadDocking.GetSerializationTag(p) ?? p.Header)?.ToString() ?? string.Empty;
                        var hdr = (p.Header ?? string.Empty).ToString() ?? string.Empty;
                        return string.Equals(tag, paneKey, StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(hdr, paneKey, StringComparison.OrdinalIgnoreCase);
                    }
                    catch { return false; }
                }).ToList();

                if (!panes.Any()) return null;

                try
                {
                    var namedPane = _owner?.FindName(paneKey + "Pane") as RadPane;
                    var namedGroup = _owner?.FindName(paneKey + "PaneGroup") as RadPaneGroup;
                    if (namedPane != null && namedGroup != null)
                    {
                        if (panes.Any(p => object.ReferenceEquals(p, namedPane) && p.Parent is RadPaneGroup parent && object.ReferenceEquals(parent, namedGroup)))
                            return panes.First(p => object.ReferenceEquals(p, namedPane));
                    }
                }
                catch { }

                var nonHidden = panes.FirstOrDefault(p => { try { return !p.IsHidden; } catch { return true; } });
                if (nonHidden != null) return nonHidden;

                return panes.First();
            }
            catch { return null; }
        }

        private RadPane FindPaneByContent(object content)
        {
            if (content == null) return null;
            try
            {
                var groups = _docking.GetAllChildren().OfType<RadPaneGroup>();
                foreach (var g in groups)
                {
                    try
                    {
                        var p = g.Items.OfType<RadPane>().FirstOrDefault(x => object.ReferenceEquals(x.Content, content));
                        if (p != null) return p;
                    }
                    catch { }
                }
            }
            catch { }
            return null;
        }

        private RadPane FindPaneInVisualOrLogicalTree(DependencyObject root, string paneKey)
        {
            if (root == null || string.IsNullOrWhiteSpace(paneKey)) return null;
            try
            {
                var stack = new Stack<DependencyObject>();
                stack.Push(root);
                while (stack.Count > 0)
                {
                    var node = stack.Pop();
                    if (node is RadPane rp)
                    {
                        try
                        {
                            var tag = (RadDocking.GetSerializationTag(rp) ?? rp.Header)?.ToString() ?? string.Empty;
                            var hdr = (rp.Header ?? string.Empty).ToString() ?? string.Empty;
                            if (string.Equals(tag, paneKey, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(hdr, paneKey, StringComparison.OrdinalIgnoreCase))
                                return rp;
                        }
                        catch { }
                    }

                    try
                    {
                        foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
                            stack.Push(child);
                    }
                    catch { }
                }
            }
            catch { }
            return null;
        }

        private bool IsPanePresentAnywhere(RadPane pane)
        {
            if (pane == null) return false;

            try
            {
                var parent = pane.Parent as RadPaneGroup;
                if (parent != null && _docking.GetAllChildren().OfType<RadPaneGroup>().Any(g => object.ReferenceEquals(g, parent)))
                {
                    try { return parent.Items.OfType<object>().Any(i => object.ReferenceEquals(i, pane)); } catch { }
                }
            }
            catch { }

            try
            {
                foreach (Window w in Application.Current.Windows)
                {
                    var found = FindPaneInVisualOrLogicalTree(w, (RadDocking.GetSerializationTag(pane) ?? pane.Header)?.ToString() ?? string.Empty);
                    if (found != null && object.ReferenceEquals(found, pane)) return true;
                }
            }
            catch { }

            return false;
        }

        private void SelectPane(RadPane pane)
        {
            if (pane == null) return;
            try { pane.IsSelected = true; } catch { }
            try { (pane as FrameworkElement)?.BringIntoView(); } catch { }
        }

        private RadPaneGroup ResolveHostGroupForOpen(string paneKey)
        {
            try
            {
                if (_owner != null)
                {
                    var named = _owner.FindName(paneKey + "PaneGroup") as RadPaneGroup;
                    if (named != null && _docking.GetAllChildren().OfType<RadPaneGroup>().Any(g => object.ReferenceEquals(g, named)))
                        return named;
                }

                var groups = _docking.GetAllChildren().OfType<RadPaneGroup>();
                foreach (var g in groups)
                {
                    try
                    {
                        var has = g.Items.OfType<RadPane>().Any(p =>
                        {
                            var tag = (RadDocking.GetSerializationTag(p) ?? p.Header)?.ToString() ?? string.Empty;
                            var hdr = (p.Header ?? string.Empty).ToString() ?? string.Empty;
                            return string.Equals(tag, paneKey, StringComparison.OrdinalIgnoreCase)
                                || string.Equals(hdr, paneKey, StringComparison.OrdinalIgnoreCase);
                        });
                        if (has) return g;
                    }
                    catch { }
                }

                if (_lastPlacement.TryGetValue(paneKey, out var placement) && placement != null && placement.Group != null)
                {
                    if (_docking.GetAllChildren().OfType<RadPaneGroup>().Any(g => object.ReferenceEquals(g, placement.Group)))
                        return placement.Group;
                }
            }
            catch { }

            return _docking.GetAllChildren().OfType<RadPaneGroup>().FirstOrDefault();
        }

        // IDisposable
        public void Dispose()
        {
            try
            {
                _stateTimer?.Stop();
                _stateTimer.Tick -= StateTimer_Tick;
            }
            catch { }
        }

        private class PlacementInfo { public RadPaneGroup Group; public int Index; }
    }
}