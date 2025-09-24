using System;
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
    public partial class ScheduleViewInnerTabContentControl : UserControl
    {
        public ScheduleViewInnerTabContentControl()
        {
            InitializeComponent();
        }

        // Public surface for MainWindow to control inner selection
        public void SelectInnerTab(int index)
        {
            try
            {
                if (ScheduleViewInnerTabControl != null && index >= 0 && index < ScheduleViewInnerTabControl.Items.Count)
                    ScheduleViewInnerTabControl.SelectedIndex = index;
            }
            catch { /* ignore */ }
        }

        // Public helper so MainWindow can re-apply selection -> VM when it re-enters the Schedule tab
        public void RefreshSelectionOnViewModel()
        {
            try
            {
                if (!(DataContext is MainViewModel vm)) return;

                // Delegate Supports to the child control to keep behavior
                SupportTabContent?.RefreshSelectionOnViewModel();

                // Teachers
                var teachers = TeacherListBox?.SelectedItems?.OfType<Teacher>().ToList();
                if (teachers != null && teachers.Count > 0)
                {
                    vm.Schedule.ShowTeachers(teachers);
                }
            }
            catch { /* ignore */ }
        }

        private void ScheduleViewInnerTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (!(sender is TabControl tc) || !tc.IsLoaded) return;
                if (!ReferenceEquals(e.OriginalSource, tc)) return;

                // Commit any pending edits in Setup grids so changes are pushed to view model.
                try
                {
                    var mw = Application.Current?.MainWindow as Window;
                    var teachersGrid = (mw as FrameworkElement)?.FindName("TeachersGrid") as DataGrid;
                    var supportsGrid = (mw as FrameworkElement)?.FindName("SupportsGrid") as DataGrid;

                    teachersGrid?.CommitEdit(DataGridEditingUnit.Cell, true);
                    teachersGrid?.CommitEdit(DataGridEditingUnit.Row, true);

                    supportsGrid?.CommitEdit(DataGridEditingUnit.Cell, true);
                    supportsGrid?.CommitEdit(DataGridEditingUnit.Row, true);
                }
                catch { /* ignore */ }

                if (DataContext is MainViewModel vm && vm.GenerateScheduleCommand != null && vm.GenerateScheduleCommand.CanExecute(null))
                {
                    try { vm.GenerateScheduleCommand.Execute(null); } catch { /* ignore */ }
                }
            }
            catch { /* ignore */ }
        }

        private void TeacherListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (!(DataContext is MainViewModel vm) || !(sender is ListBox lb)) return;

                var selectedTeachers = lb.SelectedItems.OfType<Teacher>().ToList();
                if (selectedTeachers.Count == 0)
                {
                    vm.Schedule.SelectedTeacherRows.Clear();
                    return;
                }

                vm.Schedule.ShowTeachers(selectedTeachers);
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

        private void TeamLineupFromTeacher_Click(object sender, RoutedEventArgs e)
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
    }
}