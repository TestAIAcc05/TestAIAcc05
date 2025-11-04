using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Desktop.ImportTool.Views
{
    internal static class DockingExtensions
    {
        public static IEnumerable<System.Windows.DependencyObject> GetAllChildren(this System.Windows.DependencyObject parent)
        {
            if (parent == null) yield break;
            var children = LogicalTreeHelper.GetChildren(parent).OfType<System.Windows.DependencyObject>().ToList();
            foreach (var child in children)
            {
                yield return child;
                foreach (var desc in GetAllChildren(child))
                    yield return desc;
            }
        }
    }
}