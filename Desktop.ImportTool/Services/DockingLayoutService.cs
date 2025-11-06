using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Timers;
using System.Xml.Serialization;
using System.Windows;
using System.Reflection;
using Desktop.ImportTool.Models;
using Telerik.Windows.Controls;
using Telerik.Windows.Controls.Docking;

namespace Desktop.ImportTool.Services
{
    public class DockingLayoutService
    {
        private readonly RadDocking docking;
        private readonly string storageFolder;
        private readonly Dictionary<string, RadPane> registeredPanes = new Dictionary<string, RadPane>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, PaneState> paneStates = new Dictionary<string, PaneState>(StringComparer.OrdinalIgnoreCase);
        private readonly Timer saveDebounceTimer;

        private const string LayoutFileName = "telerik_layout.xml";
        private const string PaneStatesFileName = "pane_states.xml";
        private const int SaveDebounceMillis = 500;

        public DockingLayoutService(RadDocking docking, string storageFolder = null)
        {
            this.docking = docking ?? throw new ArgumentNullException(nameof(docking));
            this.storageFolder = storageFolder ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppDomain.CurrentDomain.FriendlyName ?? "App");

            Directory.CreateDirectory(this.storageFolder);

            saveDebounceTimer = new Timer(SaveDebounceMillis) { AutoReset = false };
            saveDebounceTimer.Elapsed += (s, e) => { SavePaneStatesToFile(); SaveLayoutToFile(); };
        }

        public void RegisterPane(string id, RadPane pane)
        {
            if (string.IsNullOrWhiteSpace(id) || pane == null) return;

            registeredPanes[id] = pane;

            if (paneStates.TryGetValue(id, out var state))
                ApplyPaneStateToPane(pane, state);

            TryAttachHiddenChanged(pane, (p) =>
            {
                var visible = GetPaneVisible(p);
                paneStates[id] = new PaneState { Id = id, IsVisible = visible };
                DebouncedSave();
            });
        }

        public void TogglePane(string id)
        {
            if (!registeredPanes.TryGetValue(id, out var pane)) return;
            var currentlyVisible = GetPaneVisible(pane);
            SetPaneVisible(pane, !currentlyVisible);
            paneStates[id] = new PaneState { Id = id, IsVisible = !currentlyVisible };
            DebouncedSave();
        }

        public void ShowPane(string id)
        {
            if (!registeredPanes.TryGetValue(id, out var pane)) return;
            SetPaneVisible(pane, true);
            paneStates[id] = new PaneState { Id = id, IsVisible = true };
            DebouncedSave();
        }

        public void HidePane(string id)
        {
            if (!registeredPanes.TryGetValue(id, out var pane)) return;
            SetPaneVisible(pane, false);
            paneStates[id] = new PaneState { Id = id, IsVisible = false };
            DebouncedSave();
        }

        private void DebouncedSave()
        {
            saveDebounceTimer.Stop();
            saveDebounceTimer.Start();
        }

        #region Persistence

        private string LayoutFilePath => Path.Combine(storageFolder, LayoutFileName);
        private string PaneStatesFilePath => Path.Combine(storageFolder, PaneStatesFileName);

        public void SaveLayoutToFile()
        {
            try
            {
                using (var fs = new FileStream(LayoutFilePath, FileMode.Create, FileAccess.Write))
                    docking.SaveLayout(fs);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"SaveLayout failed: {ex}"); }
        }

        public void LoadLayoutFromFile()
        {
            try
            {
                if (!File.Exists(LayoutFilePath)) return;
                using (var fs = new FileStream(LayoutFilePath, FileMode.Open, FileAccess.Read))
                    docking.LoadLayout(fs);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"LoadLayout failed: {ex}"); }
        }

        private void SavePaneStatesToFile()
        {
            try
            {
                var list = paneStates.Values.ToList();
                var ser = new XmlSerializer(typeof(List<PaneState>));
                using (var fs = new FileStream(PaneStatesFilePath, FileMode.Create, FileAccess.Write))
                    ser.Serialize(fs, list);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"SavePaneStates failed: {ex}"); }
        }

        private void LoadPaneStatesFromFile()
        {
            try
            {
                if (!File.Exists(PaneStatesFilePath)) return;
                var ser = new XmlSerializer(typeof(List<PaneState>));
                using (var fs = new FileStream(PaneStatesFilePath, FileMode.Open, FileAccess.Read))
                {
                    var list = ser.Deserialize(fs) as List<PaneState>;
                    if (list != null)
                    {
                        paneStates.Clear();
                        foreach (var s in list) paneStates[s.Id] = s;
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"LoadPaneStates failed: {ex}"); }
        }

        public void Initialize()
        {
            LoadPaneStatesFromFile();
            LoadLayoutFromFile();
            foreach (var kv in registeredPanes)
                if (paneStates.TryGetValue(kv.Key, out var state))
                    ApplyPaneStateToPane(kv.Value, state);
        }

        public void Shutdown()
        {
            SavePaneStatesToFile();
            SaveLayoutToFile();
        }

        #endregion

        #region Pane visibility helpers (reflection / fallback)

        private bool GetPaneVisible(RadPane pane)
        {
            if (pane == null) return false;
            var t = pane.GetType();

            var propIsHidden = t.GetProperty("IsHidden", BindingFlags.Public | BindingFlags.Instance);
            if (propIsHidden != null && propIsHidden.PropertyType == typeof(bool))
                return !((bool)propIsHidden.GetValue(pane));

            var propIsClosed = t.GetProperty("IsClosed", BindingFlags.Public | BindingFlags.Instance);
            if (propIsClosed != null && propIsClosed.PropertyType == typeof(bool))
                return !((bool)propIsClosed.GetValue(pane));

            return pane.Visibility == Visibility.Visible;
        }

        private void SetPaneVisible(RadPane pane, bool visible)
        {
            if (pane == null) return;
            var t = pane.GetType();

            var propIsHidden = t.GetProperty("IsHidden", BindingFlags.Public | BindingFlags.Instance);
            if (propIsHidden != null && propIsHidden.PropertyType == typeof(bool))
            {
                propIsHidden.SetValue(pane, !visible);
                return;
            }

            var propIsClosed = t.GetProperty("IsClosed", BindingFlags.Public | BindingFlags.Instance);
            if (propIsClosed != null && propIsClosed.PropertyType == typeof(bool))
            {
                propIsClosed.SetValue(pane, !visible);
                return;
            }

            pane.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            if (visible) { try { pane.IsSelected = true; } catch { } }
        }

        private void ApplyPaneStateToPane(RadPane pane, PaneState state)
        {
            if (pane == null || state == null) return;
            SetPaneVisible(pane, state.IsVisible);
        }

        private void TryAttachHiddenChanged(RadPane pane, Action<RadPane> onChanged)
        {
            if (pane == null || onChanged == null) return;
            var t = pane.GetType();

            var ev = t.GetEvent("IsHiddenChanged", BindingFlags.Public | BindingFlags.Instance);
            if (ev != null) { EventHandler handler = (s, e) => onChanged((RadPane)s); ev.AddEventHandler(pane, handler); return; }

            ev = t.GetEvent("IsClosedChanged", BindingFlags.Public | BindingFlags.Instance);
            if (ev != null) { EventHandler handler = (s, e) => onChanged((RadPane)s); ev.AddEventHandler(pane, handler); return; }

            pane.Unloaded += (s, e) => onChanged(pane);
        }

        #endregion
    }
}