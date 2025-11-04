using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media;
using Telerik.Windows.Controls;

namespace Desktop.ImportTool.Behaviors
{
    public static class GridViewSelectionBehavior
    {
        public static readonly DependencyProperty BindableSelectedItemsProperty =
            DependencyProperty.RegisterAttached(
                "BindableSelectedItems",
                typeof(IList),
                typeof(GridViewSelectionBehavior),
                new PropertyMetadata(null, OnBindableSelectedItemsChanged));

        public static IList GetBindableSelectedItems(DependencyObject obj)
        {
            return (IList)obj.GetValue(BindableSelectedItemsProperty);
        }

        public static void SetBindableSelectedItems(DependencyObject obj, IList value)
        {
            obj.SetValue(BindableSelectedItemsProperty, value);
        }

        private static bool _isSyncing;

        private static void OnBindableSelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var grid = d as RadGridView;
            if (grid == null)
                return;

            grid.SelectionChanged -= Grid_SelectionChanged;

            var oldCollection = e.OldValue as INotifyCollectionChanged;
            if (oldCollection != null)
                oldCollection.CollectionChanged -= BoundCollection_CollectionChanged;

            var newCollection = e.NewValue as INotifyCollectionChanged;
            if (newCollection != null)
                newCollection.CollectionChanged += BoundCollection_CollectionChanged;

            grid.SelectionChanged += Grid_SelectionChanged;

            SyncToGrid(grid, GetBindableSelectedItems(grid));
        }

        private static void Grid_SelectionChanged(object sender, SelectionChangeEventArgs e)
        {
            if (_isSyncing) return;
            _isSyncing = true;

            try
            {
                var grid = sender as RadGridView;
                var boundCollection = GetBindableSelectedItems(grid);

                if (boundCollection == null)
                    return;

                foreach (var item in e.AddedItems)
                {
                    if (!boundCollection.Contains(item))
                        boundCollection.Add(item);
                }

                var toRemove = new List<object>();
                foreach (var item in e.RemovedItems)
                {
                    if (boundCollection.Contains(item))
                        toRemove.Add(item);
                }
                foreach (var item in toRemove)
                {
                    boundCollection.Remove(item);
                }
            }
            finally
            {
                _isSyncing = false;
            }
        }

        private static void BoundCollection_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (_isSyncing) return;
            _isSyncing = true;

            try
            {
                var boundCollection = sender as IList;
                if (boundCollection == null) return;

                var grid = FindGridForCollection(boundCollection);
                if (grid != null)
                {
                    SyncToGrid(grid, boundCollection);
                }
            }
            finally
            {
                _isSyncing = false;
            }
        }

        private static void SyncToGrid(RadGridView grid, IList boundCollection)
        {
            if (grid == null || boundCollection == null)
                return;

            grid.SelectionChanged -= Grid_SelectionChanged;
            grid.SelectedItems.Clear();

            foreach (var item in boundCollection)
                grid.SelectedItems.Add(item);

            grid.SelectionChanged += Grid_SelectionChanged;
        }

        private static RadGridView FindGridForCollection(IList collection)
        {
            foreach (Window window in Application.Current.Windows)
            {
                foreach (var obj in FindVisualChildren<RadGridView>(window))
                {
                    if (GetBindableSelectedItems(obj) == collection)
                        return obj;
                }
            }
            return null;
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null)
                yield break;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T t)
                    yield return t;

                foreach (T childOfChild in FindVisualChildren<T>(child))
                    yield return childOfChild;
            }
        }
    }
}