using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel; // <-- required for ObservableCollection<T>
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using ScheduleApp.Models;

namespace ScheduleApp.Infrastructure
{
    // Wraps an ObservableCollection<Teacher> and appends placeholder rows to visually fill a DataGrid.
    public sealed class PaddedCollection : IList, IList<Teacher>, INotifyCollectionChanged, INotifyPropertyChanged
    {
        private readonly ObservableCollection<Teacher> _inner;
        private readonly List<Teacher> _padding = new List<Teacher>();

        public PaddedCollection(ObservableCollection<Teacher> inner)
        {
            if (inner == null) throw new ArgumentNullException(nameof(inner));
            _inner = inner;
            _inner.CollectionChanged += Inner_CollectionChanged;
            if (_inner is INotifyPropertyChanged npc)
                npc.PropertyChanged += (_, __) => RaisePropertyChanged("Item[]");
        }

        // Adjust the number of placeholder rows so that total visible rows equals rowsFit (or real count if larger).
        public void SetPaddingToFill(int rowsFit)
        {
            if (rowsFit < 0) rowsFit = 0;
            var targetPad = Math.Max(0, rowsFit - _inner.Count);
            if (targetPad == _padding.Count) return;

            if (targetPad > _padding.Count)
            {
                var toAdd = targetPad - _padding.Count;
                for (int i = 0; i < toAdd; i++)
                    _padding.Add(CreatePlaceholder());
            }
            else
            {
                var toRemove = _padding.Count - targetPad;
                _padding.RemoveRange(_padding.Count - toRemove, toRemove);
            }

            RaiseReset();
            RaiseCountChanged();
        }

        private static Teacher CreatePlaceholder()
        {
            return new Teacher
            {
                Name = string.Empty,
                RoomNumber = string.Empty,
                Start = TimeSpan.Zero,
                End = TimeSpan.Zero,
                IsPlaceholder = true
            };
        }

        private void Inner_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RaiseReset();
            RaiseCountChanged();
        }

        #region IList<Teacher>
        public Teacher this[int index]
        {
            get
            {
                if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
                if (index < _inner.Count) return _inner[index];
                return _padding[index - _inner.Count];
            }
            set
            {
                if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
                if (index < _inner.Count) _inner[index] = value;
            }
        }

        public int Count => _inner.Count + _padding.Count;
        public bool IsReadOnly => false;

        public void Add(Teacher item)
        {
            if (item == null || item.IsPlaceholder) return;
            _inner.Add(item);
        }

        public void Clear() => _inner.Clear();

        public bool Contains(Teacher item)
        {
            if (item == null) return false;
            if (!item.IsPlaceholder) return _inner.Contains(item);
            return _padding.Contains(item);
        }

        public void CopyTo(Teacher[] array, int arrayIndex)
        {
            if (array == null) throw new ArgumentNullException(nameof(array));
            if (arrayIndex < 0) throw new ArgumentOutOfRangeException(nameof(arrayIndex));
            foreach (var t in _inner) array[arrayIndex++] = t;
            foreach (var p in _padding) array[arrayIndex++] = p;
        }

        public IEnumerator<Teacher> GetEnumerator()
        {
            foreach (var t in _inner) yield return t;
            foreach (var p in _padding) yield return p;
        }

        public int IndexOf(Teacher item)
        {
            if (item == null) return -1;
            if (!item.IsPlaceholder)
            {
                var idx = _inner.IndexOf(item);
                if (idx >= 0) return idx;
            }
            var pidx = _padding.IndexOf(item);
            return pidx >= 0 ? _inner.Count + pidx : -1;
        }

        public void Insert(int index, Teacher item)
        {
            if (item == null || item.IsPlaceholder) return;
            var clamped = Math.Max(0, Math.Min(index, _inner.Count));
            _inner.Insert(clamped, item);
        }

        public bool Remove(Teacher item)
        {
            if (item == null || item.IsPlaceholder) return false;
            return _inner.Remove(item);
        }

        public void RemoveAt(int index)
        {
            if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
            if (index < _inner.Count) _inner.RemoveAt(index);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        #endregion

        #region non-generic IList
        public bool IsFixedSize => false;
        public object SyncRoot => this;
        public bool IsSynchronized => false;

        object IList.this[int index]
        {
            get => this[index];
            set { if (value is Teacher t) this[index] = t; }
        }

        int IList.Add(object value)
        {
            if (value is Teacher t && !t.IsPlaceholder)
            {
                _inner.Add(t);
                return _inner.Count - 1;
            }
            return -1;
        }

        bool IList.Contains(object value) => value is Teacher t && Contains(t);
        int IList.IndexOf(object value) => value is Teacher t ? IndexOf(t) : -1;
        void IList.Insert(int index, object value) { if (value is Teacher t && !t.IsPlaceholder) Insert(index, t); }
        void IList.Remove(object value) { if (value is Teacher t) Remove(t); }

        void ICollection.CopyTo(Array array, int index)
        {
            if (array is Teacher[] ta) { CopyTo(ta, index); return; }
            int i = index;
            foreach (var t in _inner) array.SetValue(t, i++);
            foreach (var p in _padding) array.SetValue(p, i++);
        }
        #endregion

        #region notifications
        public event NotifyCollectionChangedEventHandler CollectionChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private void RaiseReset()
            => CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

        private void RaiseCountChanged()
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
        #endregion
    }
}