using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using ScheduleApp.Infrastructure;
using ScheduleApp.Models;

namespace ScheduleApp
{
    public partial class TeamLineupInnerControlClassroomsAndTeachers : UserControl
    {
        // Forwarding events so parent control can attach
        public event EventHandler<DataGridRowEditEndingEventArgs> SetupDataGridRowEditEnding;
        public event EventHandler<DataGridCellEditEndingEventArgs> SetupDataGridCellEditEnding;

        public TeamLineupInnerControlClassroomsAndTeachers()
        {
            InitializeComponent();
        }

        private void OnSetupDataGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            SetupDataGridRowEditEnding?.Invoke(sender, e);
        }

        private void OnSetupDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            SetupDataGridCellEditEnding?.Invoke(sender, e);
        }

        // Padding infrastructure
        private PaddedCollection _padded;
        private ObservableCollection<Teacher> _inner;
        private bool _loaded;

        private void TeachersGrid_Loaded(object sender, RoutedEventArgs e)
        {
            if (_loaded) return;
            _loaded = true;

            // Capture the original ItemsSource (ObservableCollection<Teacher>)
            _inner = TeachersGrid.ItemsSource as ObservableCollection<Teacher>;
            if (_inner == null)
            {
                // Try resolving via DataContext if binding hasn't materialized yet
                var setupProp = DataContext?.GetType().GetProperty("Setup")?.GetValue(DataContext);
                var teachers = setupProp?.GetType().GetProperty("Teachers")?.GetValue(setupProp) as ObservableCollection<Teacher>;
                _inner = teachers;
            }

            if (_inner == null) return;

            _padded = new PaddedCollection(_inner);
            TeachersGrid.ItemsSource = _padded;

            // React to inner changes
            _inner.CollectionChanged += (s, _) => UpdatePadding();

            // Recompute padding on layout updates (row/header size changes)
            TeachersGrid.LayoutUpdated += TeachersGrid_LayoutUpdated;
            UpdatePadding();
        }

        private void TeachersGrid_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                TeachersGrid.LayoutUpdated -= TeachersGrid_LayoutUpdated;
            }
            catch { }
            _loaded = false;
        }

        private void TeachersGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdatePadding();
        }

        private void TeachersGrid_LayoutUpdated(object sender, EventArgs e)
        {
            // Coalesce frequent layout passes by only updating when necessary
            UpdatePadding();
        }

        private void UpdatePadding()
        {
            if (_padded == null || TeachersGrid.ActualHeight <= 0) return;

            // Compute header height
            double headerHeight = 0;
            var headers = FindDescendant<DataGridColumnHeadersPresenter>(TeachersGrid);
            if (headers != null)
                headerHeight = headers.ActualHeight;

            // Estimate row height
            double rowHeight = TeachersGrid.RowHeight;
            if (double.IsNaN(rowHeight) || rowHeight <= 0)
            {
                // Try a realized row
                var row = TeachersGrid.ItemContainerGenerator.ContainerFromIndex(0) as DataGridRow;
                if (row != null && row.ActualHeight > 0)
                    rowHeight = row.ActualHeight;
            }
            if (double.IsNaN(rowHeight) || rowHeight <= 0)
                rowHeight = 28; // sensible fallback

            // Compute available height below header (exclude horizontal scrollbar if visible)
            var sv = FindDescendant<ScrollViewer>(TeachersGrid);
            double hScroll = (sv != null && sv.ComputedHorizontalScrollBarVisibility == Visibility.Visible)
                ? SystemParameters.HorizontalScrollBarHeight
                : 0;

            var available = Math.Max(0.0, TeachersGrid.ActualHeight - headerHeight - hScroll);
            int rowsFit = (int)Math.Floor(available / rowHeight);
            if (rowsFit < 0) rowsFit = 0;

            _padded.SetPaddingToFill(rowsFit);
        }

        private static T FindDescendant<T>(DependencyObject start) where T : DependencyObject
        {
            if (start == null) return null;
            var count = VisualTreeHelper.GetChildrenCount(start);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(start, i);
                if (child is T t) return t;
                var result = FindDescendant<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        // Keep placeholders always sorted to the bottom while preserving requested sort
        private void TeachersGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            if (_padded == null) return;

            e.Handled = true;

            var view = CollectionViewSource.GetDefaultView(TeachersGrid.ItemsSource) as ListCollectionView;
            if (view == null) return;

            // Toggle sort direction
            var newDir = (e.Column.SortDirection != ListSortDirection.Ascending)
                ? ListSortDirection.Ascending
                : ListSortDirection.Descending;

            e.Column.SortDirection = newDir;

            var memberPath = e.Column.SortMemberPath;
            view.CustomSort = new PlaceholderAwareComparer(memberPath, newDir);
            view.Refresh();
        }

        private sealed class PlaceholderAwareComparer : System.Collections.IComparer
        {
            private readonly string _member;
            private readonly ListSortDirection _dir;

            public PlaceholderAwareComparer(string member, ListSortDirection dir)
            {
                _member = member;
                _dir = dir;
            }

            public int Compare(object x, object y)
            {
                var tx = x as Teacher;
                var ty = y as Teacher;

                bool px = tx?.IsPlaceholder == true;
                bool py = ty?.IsPlaceholder == true;

                if (px && py) return 0;
                if (px && !py) return 1;  // placeholders after real items
                if (!px && py) return -1;

                // Real items: compare by requested member
                object vx = GetMemberValue(tx, _member);
                object vy = GetMemberValue(ty, _member);

                int result;
                if (vx == null && vy == null) result = 0;
                else if (vx == null) result = -1;
                else if (vy == null) result = 1;
                else if (vx is IComparable cx && vy is IComparable cy) result = cx.CompareTo(cy);
                else result = string.Compare(vx.ToString(), vy.ToString(), StringComparison.CurrentCulture);

                return _dir == ListSortDirection.Ascending ? result : -result;
            }

            private static object GetMemberValue(object obj, string name)
            {
                if (obj == null || string.IsNullOrWhiteSpace(name)) return null;
                var pi = obj.GetType().GetProperty(name);
                return pi?.GetValue(obj);
            }
        }

        // Wrapper that exposes inner items + placeholder items without mutating the inner collection.
        private sealed class PaddedCollection : IList, INotifyCollectionChanged, INotifyPropertyChanged
        {
            private readonly ObservableCollection<Teacher> _inner;
            private Teacher[] _placeholders = Array.Empty<Teacher>();

            public PaddedCollection(ObservableCollection<Teacher> inner)
            {
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));
                _inner.CollectionChanged += Inner_CollectionChanged;
            }

            public void SetPaddingToFill(int rowsFit)
            {
                if (rowsFit < 0) rowsFit = 0;

                var innerCount = _inner.Count;
                var desiredPadding = Math.Max(0, rowsFit - innerCount);

                if (desiredPadding == _placeholders.Length) return;

                _placeholders = Enumerable.Range(0, desiredPadding)
                    .Select(_ => new Teacher
                    {
                        Name = string.Empty,
                        RoomNumber = string.Empty,
                        Start = TimeSpan.Zero,
                        End = TimeSpan.Zero,
                        IsPlaceholder = true
                    })
                    .ToArray();

                RaiseReset();
            }

            private void Inner_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
            {
                // Simply reset so the composed view stays correct without complex index math.
                RaiseReset();
            }

            private void RaiseReset()
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }

            #region IList (non-generic) implementation targeting the inner collection

            public int Count => _inner.Count + _placeholders.Length;
            public bool IsFixedSize => false;
            public bool IsReadOnly => false;
            public bool IsSynchronized => false;
            public object SyncRoot => this;

            public object this[int index]
            {
                get
                {
                    if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
                    if (index < _inner.Count) return _inner[index];
                    return _placeholders[index - _inner.Count];
                }
                set
                {
                    // Forward writes only to real items; ignore writes into placeholders
                    if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
                    if (index < _inner.Count)
                    {
                        if (!(value is Teacher t)) throw new ArgumentException("Value must be Teacher");
                        _inner[index] = t;
                    }
                }
            }

            public int Add(object value)
            {
                if (value is Teacher t && !t.IsPlaceholder)
                {
                    _inner.Add(t);
                    return _inner.Count - 1;
                }
                return _inner.Count; // no-op for placeholders
            }

            public void Clear() => _inner.Clear();

            public bool Contains(object value)
            {
                if (value is Teacher t)
                {
                    if (!t.IsPlaceholder) return _inner.Contains(t);
                    return _placeholders.Contains(t);
                }
                return false;
            }

            public int IndexOf(object value)
            {
                if (value is Teacher t)
                {
                    var i = _inner.IndexOf(t);
                    if (i >= 0) return i;
                    var j = Array.IndexOf(_placeholders, t);
                    if (j >= 0) return _inner.Count + j;
                }
                return -1;
            }

            public void Insert(int index, object value)
            {
                if (!(value is Teacher t) || t.IsPlaceholder) return;

                if (index < 0) index = 0;
                if (index > _inner.Count) index = _inner.Count; // clamp insert beyond real items
                _inner.Insert(index, t);
            }

            public void Remove(object value)
            {
                if (value is Teacher t && !t.IsPlaceholder)
                    _inner.Remove(t);
            }

            public void RemoveAt(int index)
            {
                if (index < 0 || index >= Count) return;
                if (index < _inner.Count)
                    _inner.RemoveAt(index);
                // ignore removals targeting placeholders
            }

            public void CopyTo(Array array, int index)
            {
                var all = this.Cast<object>().ToArray();
                Array.Copy(all, 0, array, index, all.Length);
            }

            public IEnumerator GetEnumerator()
            {
                foreach (var t in _inner) yield return t;
                foreach (var p in _placeholders) yield return p;
            }

            #endregion

            public event NotifyCollectionChangedEventHandler CollectionChanged;
            public event PropertyChangedEventHandler PropertyChanged;
        }
    }
}