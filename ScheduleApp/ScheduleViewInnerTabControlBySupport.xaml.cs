using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ScheduleApp.Models;
using ScheduleApp.ViewModels;

namespace ScheduleApp
{
    public partial class ScheduleViewInnerTabControlBySupport : UserControl
    {
        public ScheduleViewInnerTabControlBySupport()
        {
            InitializeComponent();
        }

        // Called by parent to re-apply selection to VM
        public void RefreshSelectionOnViewModel()
        {
            try
            {
                if (!(DataContext is MainViewModel vm)) return;

                var selectedEntries = SupportListBox?.SelectedItems?.OfType<SupportStaffEntry>().ToList();
                if (selectedEntries != null && selectedEntries.Count > 0)
                {
                    var supports = selectedEntries.Select(se => new Support { Name = se.Name }).ToList();
                    vm.Schedule.ShowSupports(supports);
                }
            }
            catch { /* ignore */ }
        }

        private void SupportListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (!(DataContext is MainViewModel vm) || !(sender is ListBox lb)) return;

                var selectedEntries = lb.SelectedItems.OfType<SupportStaffEntry>().ToList();
                if (selectedEntries.Count == 0)
                {
                    vm.Schedule.SelectedSupportRows.Clear();
                    return;
                }

                var selectedSupports = selectedEntries.Select(en => new Support { Name = en.Name }).ToList();
                vm.Schedule.ShowSupports(selectedSupports);
            }
            catch { /* ignore */ }
        }

        private void ScheduleArea_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var src = e.OriginalSource as DependencyObject;
                if (src != null &&
                    (FindAncestor<DataGridColumnHeader>(src) != null ||
                     FindAncestor<DataGridRowHeader>(src) != null ||
                     FindAncestor<ScrollBar>(src) != null))
                {
                    return;
                }

                if (!(DataContext is MainViewModel vm)) return;

                var cmd = vm.GenerateScheduleCommand;
                if (cmd != null && cmd.CanExecute(null))
                    cmd.Execute(null);
            }
            catch { /* ignore */ }
        }

        private void TeamLineupFromSupport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var mw = Application.Current?.MainWindow as FrameworkElement;
                var mainTabs = (mw as FrameworkElement)?.FindName("MainTabControl") as TabControl;
                if (mainTabs != null) mainTabs.SelectedIndex = 1; // Team Lineup
            }
            catch { /* ignore */ }
        }

        private void SchedulePreview_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var mw = Application.Current?.MainWindow as FrameworkElement;
                var mainTabs = (mw as FrameworkElement)?.FindName("MainTabControl") as TabControl;
                if (mainTabs != null) mainTabs.SelectedIndex = 2; // Preview
            }
            catch { /* ignore */ }
        }

        private static T FindAncestor<T>(DependencyObject d) where T : DependencyObject
        {
            while (d != null)
            {
                if (d is T t) return t;
                d = VisualTreeHelper.GetParent(d);
            }
            return null;
        }
    }
}