using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace ScheduleApp.Behaviors
{
    // Attach to DataGrid with: DataGridRowReorderBehavior.EnableRowReorder="True"
    public static class DataGridRowReorderBehavior
    {
        private const string DragDataFormat = "ScheduleApp:DragItems";

        public static readonly DependencyProperty EnableRowReorderProperty =
            DependencyProperty.RegisterAttached(
                "EnableRowReorder",
                typeof(bool),
                typeof(DataGridRowReorderBehavior),
                new PropertyMetadata(false, OnEnableRowReorderChanged));

        public static void SetEnableRowReorder(DependencyObject element, bool value) =>
            element.SetValue(EnableRowReorderProperty, value);

        public static bool GetEnableRowReorder(DependencyObject element) =>
            (bool)element.GetValue(EnableRowReorderProperty);

        private static void OnEnableRowReorderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is DataGrid grid)) return;

            if ((bool)e.NewValue)
                Attach(grid);
            else
                Detach(grid);
        }

        private static void Attach(DataGrid grid)
        {
            // Ensure the grid can accept drops
            grid.AllowDrop = true;

            grid.PreviewMouseLeftButtonDown += Grid_PreviewMouseLeftButtonDown;
            grid.MouseMove += Grid_MouseMove;
            grid.DragOver += Grid_DragOver;
            grid.Drop += Grid_Drop;
            grid.PreviewDragLeave += Grid_PreviewDragLeave;
            grid.Unloaded += Grid_Unloaded;

            // keyboard shortcuts for accessibility (Ctrl+Up / Ctrl+Down)
            grid.PreviewKeyDown += Grid_PreviewKeyDown;
        }

        private static void Detach(DataGrid grid)
        {
            grid.PreviewMouseLeftButtonDown -= Grid_PreviewMouseLeftButtonDown;
            grid.MouseMove -= Grid_MouseMove;
            grid.DragOver -= Grid_DragOver;
            grid.Drop -= Grid_Drop;
            grid.PreviewDragLeave -= Grid_PreviewDragLeave;
            grid.Unloaded -= Grid_Unloaded;

            grid.PreviewKeyDown -= Grid_PreviewKeyDown;
        }

        #region drag state

        private static Point _dragStartPoint;
        private static IList _draggedItems;
        private static DataGrid _sourceGrid;
        private static InsertionAdorner _insertionAdorner;
        private static DataGridRow _lastTargetRow;

        #endregion

        private static void Grid_Unloaded(object sender, RoutedEventArgs e)
        {
            if (sender is DataGrid dg) Detach(dg);
        }

        private static void Grid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is DataGrid grid)) return;

            _dragStartPoint = e.GetPosition(null);
            _draggedItems = null;
            _sourceGrid = null;

            // Use OriginalSource for reliable hit-testing
            var hit = e.OriginalSource as DependencyObject;
            if (hit == null) return;

            // If user clicked the named grip TextBlock inside the cell
            var gripText = GetParentOfType<TextBlock>(hit);
            if (gripText != null && string.Equals(gripText.Name, "DragGrip", StringComparison.Ordinal))
            {
                var row = GetParentOfType<DataGridRow>(gripText);
                if (row != null)
                {
                    // If multiple rows are selected and the clicked row is part of the selection,
                    // use the full selection for dragging; otherwise drag single row.
                    if (grid.SelectionMode != DataGridSelectionMode.Single && grid.SelectedItems != null && grid.SelectedItems.Contains(row.Item))
                    {
                        _draggedItems = grid.SelectedItems.Cast<object>().ToList();
                    }
                    else
                    {
                        _draggedItems = new List<object> { row.Item };
                    }

                    _sourceGrid = grid;
                    return;
                }
            }

            // Allow drag initiation when clicking anywhere in the first cell
            var row2 = GetParentOfType<DataGridRow>(hit);
            if (row2 != null)
            {
                var cell = GetParentOfType<DataGridCell>(hit);
                if (cell != null)
                {
                    var colIndex = cell.Column?.DisplayIndex ?? -1;
                    if (colIndex == 0)
                    {
                        if (grid.SelectionMode != DataGridSelectionMode.Single && grid.SelectedItems != null && grid.SelectedItems.Contains(row2.Item))
                        {
                            _draggedItems = grid.SelectedItems.Cast<object>().ToList();
                        }
                        else
                        {
                            _draggedItems = new List<object> { row2.Item };
                        }

                        _sourceGrid = grid;
                    }
                }
            }
        }

        private static void Grid_MouseMove(object sender, MouseEventArgs e)
        {
            if (_draggedItems == null || _sourceGrid == null) return;
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                _draggedItems = null;
                _sourceGrid = null;
                return;
            }

            var current = e.GetPosition(null);
            var dx = Math.Abs(current.X - _dragStartPoint.X);
            var dy = Math.Abs(current.Y - _dragStartPoint.Y);
            if (dx < SystemParameters.MinimumHorizontalDragDistance && dy < SystemParameters.MinimumVerticalDragDistance) return;

            // begin drag
            var data = new DataObject();
            // place the list of items into the DataObject
            data.SetData(DragDataFormat, _draggedItems);
            data.SetData(typeof(DataGrid), _sourceGrid);

            DragDrop.DoDragDrop(_sourceGrid, data, DragDropEffects.Move);

            // clear after drag
            _draggedItems = null;
            _sourceGrid = null;
            RemoveInsertionAdorner();
        }

        private static void Grid_DragOver(object sender, DragEventArgs e)
        {
            if (!(sender is DataGrid grid)) return;

            if (!e.Data.GetDataPresent(DragDataFormat) || !e.Data.GetDataPresent(typeof(DataGrid)))
            {
                e.Effects = DragDropEffects.None;
                RemoveInsertionAdorner();
                return;
            }

            var sourceGrid = e.Data.GetData(typeof(DataGrid)) as DataGrid;
            var dragged = e.Data.GetData(DragDataFormat) as IList;

            // restrict to same DataGrid
            if (!ReferenceEquals(sourceGrid, grid))
            {
                e.Effects = DragDropEffects.None;
                RemoveInsertionAdorner();
                return;
            }

            var pos = e.GetPosition(grid);
            var targetRow = GetRowUnderMouse(grid, pos);
            ShowInsertionAdorner(grid, targetRow);
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }

        private static void Grid_PreviewDragLeave(object sender, DragEventArgs e)
        {
            // remove visual when leaving
            RemoveInsertionAdorner();
        }

        private static void Grid_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (!(sender is DataGrid grid)) return;
                if (!e.Data.GetDataPresent(DragDataFormat) || !e.Data.GetDataPresent(typeof(DataGrid)))
                {
                    RemoveInsertionAdorner();
                    return;
                }

                var sourceGrid = e.Data.GetData(typeof(DataGrid)) as DataGrid;
                var dragged = e.Data.GetData(DragDataFormat) as IList;

                if (!ReferenceEquals(sourceGrid, grid))
                {
                    RemoveInsertionAdorner();
                    return;
                }

                var pos = e.GetPosition(grid);
                var targetRow = GetRowUnderMouse(grid, pos);
                int targetIndex;
                if (targetRow == null)
                {
                    targetIndex = grid.Items.Count;
                }
                else
                {
                    targetIndex = grid.ItemContainerGenerator.IndexFromContainer(targetRow);
                }

                var itemsSource = grid.ItemsSource as IList;
                if (itemsSource == null)
                {
                    RemoveInsertionAdorner();
                    return;
                }

                if (dragged == null || dragged.Count == 0)
                {
                    RemoveInsertionAdorner();
                    return;
                }

                // If dropping below the target row, and mouse is in lower half, insert after target
                if (targetRow != null)
                {
                    var rowPos = e.GetPosition(targetRow);
                    if (rowPos.Y > (targetRow.ActualHeight / 2.0))
                    {
                        targetIndex = targetIndex + 1;
                    }
                }
                else
                {
                    targetIndex = itemsSource.Count;
                }

                // Build ordered list of items to move with their original indices in the source list
                var pairs = new List<KeyValuePair<int, object>>();
                foreach (var obj in dragged.Cast<object>())
                {
                    int idx = itemsSource.IndexOf(obj);
                    if (idx >= 0)
                        pairs.Add(new KeyValuePair<int, object>(idx, obj));
                }

                if (pairs.Count == 0)
                {
                    RemoveInsertionAdorner();
                    return;
                }

                // Order by original index
                pairs = pairs.OrderBy(p => p.Key).ToList();

                // Validation rule: if inserting before a row whose Teacher/Name is empty OR whose Start is default -> disallow
                if (targetIndex < itemsSource.Count)
                {
                    var targetItem = itemsSource[targetIndex];
                    if (HasEmptyNameOrStart(targetItem))
                    {
                        RemoveInsertionAdorner();
                        e.Effects = DragDropEffects.None;
                        e.Handled = true;
                        return;
                    }
                }

                if (itemsSource.IsFixedSize || itemsSource.IsReadOnly)
                {
                    RemoveInsertionAdorner();
                    return;
                }

                // Adjust target index for removal of source items earlier in list
                int targetIndexBeforeRemoval = targetIndex;
                int removedBeforeTarget = pairs.Count(p => p.Key < targetIndexBeforeRemoval);

                // Remove items starting from highest index to preserve indices
                for (int i = pairs.Count - 1; i >= 0; i--)
                {
                    itemsSource.RemoveAt(pairs[i].Key);
                }

                // Compute adjusted target index after removal
                int adjustedTargetIndex = targetIndexBeforeRemoval - removedBeforeTarget;
                if (adjustedTargetIndex < 0) adjustedTargetIndex = 0;
                if (adjustedTargetIndex > itemsSource.Count) adjustedTargetIndex = itemsSource.Count;

                // Insert items in original order at adjustedTargetIndex
                foreach (var p in pairs)
                {
                    itemsSource.Insert(adjustedTargetIndex++, p.Value);
                }

                RemoveInsertionAdorner();
                e.Handled = true;
            }
            catch
            {
                RemoveInsertionAdorner();
            }
        }

        #region keyboard support (Ctrl+Up / Ctrl+Down)

        private static void Grid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!(sender is DataGrid grid)) return;

            // require Ctrl
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
            if (e.Key != Key.Up && e.Key != Key.Down) return;

            var items = grid.ItemsSource as IList;
            if (items == null || items.IsReadOnly || items.IsFixedSize) return;

            // Support multiple selection: use SelectedItems if more than one, otherwise fall back to SelectedItem
            IList selectedItemsList = null;
            if (grid.SelectedItems != null && grid.SelectedItems.Count > 1)
            {
                selectedItemsList = grid.SelectedItems;
            }
            else if (grid.SelectedItem != null)
            {
                selectedItemsList = new List<object> { grid.SelectedItem };
            }
            else
            {
                return;
            }

            // Build pairs of index+item, ordered by index
            var pairs = new List<KeyValuePair<int, object>>();
            foreach (var obj in selectedItemsList.Cast<object>())
            {
                int idx = items.IndexOf(obj);
                if (idx >= 0)
                    pairs.Add(new KeyValuePair<int, object>(idx, obj));
            }

            if (pairs.Count == 0) return;
            pairs = pairs.OrderBy(p => p.Key).ToList();

            if (e.Key == Key.Up)
            {
                int firstIndex = pairs.First().Key;
                if (firstIndex == 0) { e.Handled = true; return; }
                int targetIndexBeforeRemoval = firstIndex - 1;

                // validation: don't move above a target row that has empty TeacherName or Start
                if (HasEmptyNameOrStart(items[targetIndexBeforeRemoval]))
                {
                    e.Handled = true;
                    return;
                }

                if (items.IsReadOnly || items.IsFixedSize) { e.Handled = true; return; }

                // Remove from highest to lowest
                for (int i = pairs.Count - 1; i >= 0; i--)
                {
                    items.RemoveAt(pairs[i].Key);
                }

                // Compute adjusted target index after removals
                int removedBefore = pairs.Count(p => p.Key < targetIndexBeforeRemoval);
                int adjustedTargetIndex = targetIndexBeforeRemoval - removedBefore;
                if (adjustedTargetIndex < 0) adjustedTargetIndex = 0;

                // Insert in original order
                foreach (var p in pairs)
                {
                    items.Insert(adjustedTargetIndex++, p.Value);
                }

                // restore selection to moved items
                grid.SelectedItems.Clear();
                foreach (var p in pairs)
                    grid.SelectedItems.Add(p.Value);

                grid.ScrollIntoView(pairs.First().Value);
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                int lastIndex = pairs.Last().Key;
                if (lastIndex >= items.Count - 1) { e.Handled = true; return; }
                int targetIndexBeforeRemoval = lastIndex + 1;

                // validation: don't move above a target row that has empty TeacherName or Start
                if (HasEmptyNameOrStart(items[targetIndexBeforeRemoval]))
                {
                    e.Handled = true;
                    return;
                }

                if (items.IsReadOnly || items.IsFixedSize) { e.Handled = true; return; }

                // Remove from highest to lowest
                for (int i = pairs.Count - 1; i >= 0; i--)
                {
                    items.RemoveAt(pairs[i].Key);
                }

                // Compute adjusted target index after removals
                int removedBefore = pairs.Count(p => p.Key < targetIndexBeforeRemoval);
                int adjustedTargetIndex = targetIndexBeforeRemoval - removedBefore;
                if (adjustedTargetIndex < 0) adjustedTargetIndex = 0;
                if (adjustedTargetIndex > items.Count) adjustedTargetIndex = items.Count;

                // Insert in original order
                foreach (var p in pairs)
                {
                    items.Insert(adjustedTargetIndex++, p.Value);
                }

                // restore selection to moved items
                grid.SelectedItems.Clear();
                foreach (var p in pairs)
                    grid.SelectedItems.Add(p.Value);

                grid.ScrollIntoView(pairs.First().Value);
                e.Handled = true;
            }
        }

        #endregion

        #region helpers

        private static bool HasEmptyNameOrStart(object item)
        {
            if (item == null) return false;

            var nameProp = item.GetType().GetProperty("TeacherName") ?? item.GetType().GetProperty("Name");
            if (nameProp != null)
            {
                var val = nameProp.GetValue(item) as string;
                if (string.IsNullOrWhiteSpace(val)) return true;
            }

            var startProp = item.GetType().GetProperty("Start") ?? item.GetType().GetProperty("StartTime");
            if (startProp != null)
            {
                var sval = startProp.GetValue(item);
                if (sval is TimeSpan ts)
                {
                    if (ts == TimeSpan.Zero) return true;
                }
                else if (sval == null)
                {
                    return true;
                }
            }

            return false;
        }

        private static T GetParentOfType<T>(DependencyObject element) where T : DependencyObject
        {
            while (element != null)
            {
                if (element is T t) return t;
                element = VisualTreeHelper.GetParent(element);
            }
            return null;
        }

        private static DataGridRow GetRowUnderMouse(DataGrid grid, Point pt)
        {
            var hit = VisualTreeHelper.HitTest(grid, pt)?.VisualHit;
            if (hit == null) return null;
            var row = GetParentOfType<DataGridRow>(hit);
            return row;
        }

        #endregion

        #region insertion adorner

        private static void ShowInsertionAdorner(DataGrid grid, DataGridRow targetRow)
        {
            if (_insertionAdorner != null && _lastTargetRow == targetRow) return;

            RemoveInsertionAdorner();

            if (targetRow != null)
            {
                var layer = AdornerLayer.GetAdornerLayer(grid);
                if (layer != null)
                {
                    _insertionAdorner = new InsertionAdorner(targetRow, grid);
                    layer.Add(_insertionAdorner);
                    _lastTargetRow = targetRow;
                }
            }
        }

        private static void RemoveInsertionAdorner()
        {
            if (_insertionAdorner != null)
            {
                var layer = AdornerLayer.GetAdornerLayer(_insertionAdorner.AdornedElement);
                layer?.Remove(_insertionAdorner);
                _insertionAdorner = null;
                _lastTargetRow = null;
            }
        }

        // A simple adorner that draws a translucent rectangle over the target row (visual feedback / ghost).
        private class InsertionAdorner : Adorner
        {
            private readonly DataGridRow _row;
            // subtle translucent brush for ghosting
            private readonly Brush _brush = new SolidColorBrush(Color.FromArgb(48, 0, 0, 0));
            private readonly Pen _pen = new Pen(new SolidColorBrush(Color.FromArgb(96, 0, 122, 204)), 1) { DashStyle = DashStyles.Dash };

            public InsertionAdorner(DataGridRow row, UIElement adorned) : base(adorned)
            {
                _row = row;
                IsHitTestVisible = false;
            }

            protected override void OnRender(DrawingContext drawingContext)
            {
                base.OnRender(drawingContext);
                if (_row == null) return;

                var transform = _row.TransformToAncestor(AdornedElement as Visual) as Transform;
                var topLeft = transform.Transform(new Point(0, 0));
                var rect = new Rect(topLeft, new Size(_row.ActualWidth, _row.ActualHeight));
                drawingContext.DrawRectangle(_brush, _pen, rect);
            }
        }

        #endregion
    }
}