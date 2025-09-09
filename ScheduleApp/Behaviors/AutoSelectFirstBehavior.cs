using System;
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace ScheduleApp.Behaviors
{
    /// <summary>
    /// Attached behavior: when enabled on a ListBox it will auto-select the first item
    /// when the list is populated or becomes visible, but only when no item is already selected.
    /// Safe for MVVM: it sets ListBox.SelectedItem, which updates any binding to the VM.
    /// </summary>
    public static class AutoSelectFirstBehavior
    {
        public static readonly DependencyProperty EnableAutoSelectProperty =
            DependencyProperty.RegisterAttached(
                "EnableAutoSelect",
                typeof(bool),
                typeof(AutoSelectFirstBehavior),
                new PropertyMetadata(false, OnEnableAutoSelectChanged));

        public static void SetEnableAutoSelect(DependencyObject obj, bool value) => obj.SetValue(EnableAutoSelectProperty, value);
        public static bool GetEnableAutoSelect(DependencyObject obj) => (bool)obj.GetValue(EnableAutoSelectProperty);

        private static void OnEnableAutoSelectChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is ListBox lb)) return;

            if ((bool)e.NewValue)
            {
                lb.Loaded += OnLoaded;
                lb.IsVisibleChanged += OnIsVisibleChanged;
                DependencyPropertyDescriptor.FromProperty(ItemsControl.ItemsSourceProperty, typeof(ItemsControl))
                    .AddValueChanged(lb, OnItemsSourceChanged);
                AttachCollectionChanged(lb, lb.ItemsSource as INotifyCollectionChanged);
            }
            else
            {
                lb.Loaded -= OnLoaded;
                lb.IsVisibleChanged -= OnIsVisibleChanged;
                DependencyPropertyDescriptor.FromProperty(ItemsControl.ItemsSourceProperty, typeof(ItemsControl))
                    .RemoveValueChanged(lb, OnItemsSourceChanged);
                DetachCollectionChanged(lb);
            }
        }

        // store handlers so we can detach later
        private static readonly ConditionalWeakTable<ListBox, NotifyCollectionChangedEventHandler> _handlers
            = new ConditionalWeakTable<ListBox, NotifyCollectionChangedEventHandler>();

        private static void AttachCollectionChanged(ListBox lb, INotifyCollectionChanged coll)
        {
            DetachCollectionChanged(lb);
            if (coll == null) return;

            NotifyCollectionChangedEventHandler handler = (s, e) => TrySelectFirst(lb);
            coll.CollectionChanged += handler;
            _handlers.Add(lb, handler);
        }

        private static void DetachCollectionChanged(ListBox lb)
        {
            if (_handlers.TryGetValue(lb, out var handler) && lb.ItemsSource is INotifyCollectionChanged coll)
            {
                coll.CollectionChanged -= handler;
                _handlers.Remove(lb);
            }
        }

        private static void OnItemsSourceChanged(object sender, EventArgs e)
        {
            if (sender is ListBox lb)
            {
                AttachCollectionChanged(lb, lb.ItemsSource as INotifyCollectionChanged);
                TrySelectFirst(lb);
            }
        }

        private static void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is ListBox lb) TrySelectFirst(lb);
        }

        private static void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is ListBox lb && lb.IsVisible) TrySelectFirst(lb);
        }

        private static void TrySelectFirst(ListBox lb)
        {
            try
            {
                if (!GetEnableAutoSelect(lb)) return;

                // Respect existing selection (do not override user choice)
                if (lb.SelectedItem != null) return;

                // Check Items first (covers both direct items and a bound ItemsSource)
                if (lb.Items == null || lb.Items.Count == 0) return;

                // Select the first non-null item
                for (int i = 0; i < lb.Items.Count; i++)
                {
                    var candidate = lb.Items[i];
                    if (candidate != null)
                    {
                        lb.SelectedItem = candidate;
                        return;
                    }
                }
            }
            catch
            {
                // Swallow exceptions to avoid breaking UI; behavior is best-effort.
            }
        }
    }
}