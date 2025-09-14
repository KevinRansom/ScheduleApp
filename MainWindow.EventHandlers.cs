using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Shapes;
using ScheduleApp.Models;
using ScheduleApp.ViewModels;

namespace ScheduleApp
{
    // Add these event handler implementations in a separate partial class file so your existing
    // MainWindow.xaml.cs is not modified directly. Paste this file into your project.
    public partial class MainWindow
    {
        // Top-level TabControl selection changed (wired in XAML: MainTabControl)
        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (!(sender is TabControl tc) || !tc.IsLoaded) return;

                // Only handle events raised by the TabControl itself (ignore bubbled child events)
                if (!ReferenceEquals(e.OriginalSource, tc)) return;

                // Only regenerate when the Schedule View tab (top-level index 0) is selected
                if (tc.SelectedIndex != 0) return;

                // 1) Try to update binding on any focused editor (ComboBox/TextBox inside DataGrid)
                try
                {
                    var focused = Keyboard.FocusedElement as DependencyObject;
                    if (focused is FrameworkElement fe)
                    {
                        var be = fe.GetBindingExpression(System.Windows.Controls.Primitives.Selector.SelectedItemProperty)
                                 ?? fe.GetBindingExpression(System.Windows.Controls.ComboBox.SelectedValueProperty)
                                 ?? fe.GetBindingExpression(System.Windows.Controls.Primitives.RangeBase.ValueProperty)
                                 ?? fe.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty);
                        be?.UpdateSource();
                    }
                }
                catch
                {
                    // ignore binding update failures
                }

                // 2) Commit any pending DataGrid edits (cell + row) on Setup grids.
                try
                {
                    TeachersGrid?.CommitEdit(DataGridEditingUnit.Cell, true);
                    TeachersGrid?.CommitEdit(DataGridEditingUnit.Row, true);

                    SupportsGrid?.CommitEdit(DataGridEditingUnit.Cell, true);
                    SupportsGrid?.CommitEdit(DataGridEditingUnit.Row, true);
                }
                catch
                {
                    // ignore commit errors
                }

                // 3) Move logical focus off editors (helps end edit mode)
                try
                {
                    var scope = FocusManager.GetFocusScope(this);
                    FocusManager.SetFocusedElement(scope, MainTabControl);
                    Keyboard.ClearFocus();
                }
                catch
                {
                    // ignore
                }

                // 4) Run GenerateSchedule at ApplicationIdle so UI/binding ops complete first
                if (DataContext is MainViewModel vm && vm.GenerateScheduleCommand != null && vm.GenerateScheduleCommand.CanExecute(null))
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            if (vm.GenerateScheduleCommand.CanExecute(null))
                                vm.GenerateScheduleCommand.Execute(null);

                            // Refresh displayed support/teacher rows for current selections (best-effort)
                            try
                            {
                                var selectedSupports = SupportListBox?.SelectedItems?.OfType<Support>().ToList();
                                if (selectedSupports != null && selectedSupports.Count > 0)
                                    vm.Schedule.ShowSupports(selectedSupports);

                                var selectedTeachers = TeacherListBox?.SelectedItems?.OfType<Teacher>().ToList();
                                if (selectedTeachers != null && selectedTeachers.Count > 0)
                                    vm.Schedule.ShowTeachers(selectedTeachers);
                            }
                            catch
                            {
                                // ignore refresh failures
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Failed to regenerate schedule:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }), DispatcherPriority.ApplicationIdle);
                }
            }
            catch
            {
                // swallow top-level to keep UI stable
            }
        }

        // Inner Schedule view TabControl selection changed (wired in XAML: ScheduleViewInnerTabControl)
        private void ScheduleViewInnerTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (!(sender is TabControl tc) || !tc.IsLoaded) return;
                if (!ReferenceEquals(e.OriginalSource, tc)) return;

                // Commit any pending edits in Setup grids so changes are pushed to view model
                try
                {
                    TeachersGrid?.CommitEdit(DataGridEditingUnit.Cell, true);
                    TeachersGrid?.CommitEdit(DataGridEditingUnit.Row, true);

                    SupportsGrid?.CommitEdit(DataGridEditingUnit.Cell, true);
                    SupportsGrid?.CommitEdit(DataGridEditingUnit.Row, true);
                }
                catch
                {
                    // ignore
                }

                // Trigger schedule generation synchronously (caller expects immediate refresh)
                if (DataContext is MainViewModel vm && vm.GenerateScheduleCommand != null && vm.GenerateScheduleCommand.CanExecute(null))
                {
                    try
                    {
                        vm.GenerateScheduleCommand.Execute(null);
                    }
                    catch
                    {
                        // swallow to avoid UI crash
                    }
                }
            }
            catch
            {
                // ignore
            }
        }

        // Support list selection changed (wired in XAML: SupportListBox)
        private void SupportListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (!(DataContext is MainViewModel vm) || !(sender is ListBox lb)) return;

                var selectedEntries = lb.SelectedItems.OfType<ScheduleApp.Models.SupportStaffEntry>().ToList();
                if (selectedEntries.Count == 0)
                {
                    vm.Schedule.SelectedSupportRows.Clear();
                    return;
                }

                var selectedSupports = selectedEntries.Select(en => new Support { Name = en.Name }).ToList();
                vm.Schedule.ShowSupports(selectedSupports);
            }
            catch
            {
                // swallow UI hookup errors
            }
        }

        // Teacher list selection changed (wired in XAML: TeacherListBox)
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
            catch
            {
                // swallow UI hookup errors
            }
        }

        // Central click handler used in several schedule display controls to force regenerate (wired in XAML)
        private void ScheduleArea_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (!(DataContext is MainViewModel vm)) return;

                var cmd = vm.GenerateScheduleCommand;
                if (cmd != null && cmd.CanExecute(null))
                    cmd.Execute(null);
            }
            catch
            {
                // swallow to keep UI responsive
            }
        }
    }
}