using System;
using System.ComponentModel;
using System.Windows;
using Desktop.ImportTool.Services;
using Telerik.Windows.Controls;
using Telerik.Windows.Controls.Docking;

namespace Desktop.ImportTool.Services
{
    public static class DockingBehavior
    {
        public static readonly DependencyProperty PaneIdProperty =
            DependencyProperty.RegisterAttached("PaneId", typeof(string), typeof(DockingBehavior),
                new PropertyMetadata(null, OnPaneIdChanged));

        public static void SetPaneId(DependencyObject element, string value) => element.SetValue(PaneIdProperty, value);
        public static string GetPaneId(DependencyObject element) => (string)element.GetValue(PaneIdProperty);

        private static void OnPaneIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (DesignerProperties.GetIsInDesignMode(d)) return;
            if (!(d is RadPane pane)) return;
            var id = e.NewValue as string;
            if (string.IsNullOrWhiteSpace(id)) return;

            if (Application.Current.Resources["DockingLayoutService"] is DockingLayoutService svc)
                svc.RegisterPane(id, pane);
        }
    }
}