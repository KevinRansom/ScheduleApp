using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Shapes;
using System.Windows.Controls.Primitives; // for DataGridColumnHeader / DataGridRowHeader / ScrollBar
using System.Windows.Media;              // <-- add this for VisualTreeHelper
using System.Collections.Specialized;
using System.ComponentModel;
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
                if (!ReferenceEquals(e.OriginalSource, tc)) return;
                if (tc.SelectedIndex != 0) return;

                // Update focused editor binding
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
                catch { }

                // Commit pending edits on Setup grids
                try
                {
                    TeachersGrid?.CommitEdit(DataGridEditingUnit.Cell, true);
                    TeachersGrid?.CommitEdit(DataGridEditingUnit.Row, true);
                    SupportsGrid?.CommitEdit(DataGridEditingUnit.Cell, true);
                    SupportsGrid?.CommitEdit(DataGridEditingUnit.Row, true);
                }
                catch { }

                // Move logical focus off editors
                try
                {
                    var scope = FocusManager.GetFocusScope(this);
                    FocusManager.SetFocusedElement(scope, MainTabControl);
                    Keyboard.ClearFocus();
                }
                catch { }

                // Generate schedule and refresh selection via the user control
                if (DataContext is MainViewModel vm && vm.GenerateScheduleCommand != null && vm.GenerateScheduleCommand.CanExecute(null))
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            if (vm.GenerateScheduleCommand.CanExecute(null))
                                vm.GenerateScheduleCommand.Execute(null);

                            ScheduleViewInnerTabContentControl?.RefreshSelectionOnViewModel();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Failed to regenerate schedule:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }), DispatcherPriority.ApplicationIdle);
                }
            }
            catch { /* keep UI stable */ }
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

        // Helper to walk up the visual tree
        private static T FindAncestor<T>(DependencyObject d) where T : DependencyObject
        {
            while (d != null)
            {
                if (d is T t) return t;
                d = System.Windows.Media.VisualTreeHelper.GetParent(d); // fully-qualified
            }
            return null;
        }

        // Central click handler used in several schedule display controls to force regenerate (wired in XAML)
        private void ScheduleArea_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                // Ignore clicks on DataGrid chrome (headers/row headers/scrollbars) so sorting won't regenerate
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
            catch
            {
                // swallow to keep UI responsive
            }
        }

        // --- Auto-save support for Setup data ---------------------------------

        // Called when Setup.* collections (Teachers/Supports/Preferences) change (add/remove/move)
        private void SetupCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            try
            {
                // Attach/detach item-level PropertyChanged if items implement INotifyPropertyChanged
                if (e.NewItems != null)
                {
                    foreach (var it in e.NewItems)
                        if (it is INotifyPropertyChanged npc) npc.PropertyChanged += Item_PropertyChanged;
                }
                if (e.OldItems != null)
                {
                    foreach (var it in e.OldItems)
                        if (it is INotifyPropertyChanged npc) npc.PropertyChanged -= Item_PropertyChanged;
                }

                // schedule debounced save
                ScheduleAutoSave();
            }
            catch
            {
                // swallow to keep UI stable
            }
        }

        // Top-level Setup property changed (SchoolName, SchoolAddress, SchoolPhone, SaveFolder, etc.)
        private void Setup_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            try
            {
                ScheduleAutoSave();
            }
            catch
            {
                // ignore
            }
        }

        // If an item in the collections implements INotifyPropertyChanged, changes will be captured here
        private void Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            try
            {
                ScheduleAutoSave();
            }
            catch
            {
                // ignore
            }
        }

        // Ensure edits are committed, then save (these are the single canonical handlers)
        private void SetupDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            try
            {
                // Defer to debounced save so we don't re-enter DataGrid handlers
                ScheduleAutoSave();
            }
            catch
            {
                // ignore
            }
        }

        private void SetupDataGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            try
            {
                // Defer to debounced save so we don't re-enter DataGrid handlers
                ScheduleAutoSave();
            }
            catch
            {
                // ignore
            }
        }

        // Debounce/autosave helper (add to the MainWindow partial class)

        // Delay after last change before performing a save
        private static readonly TimeSpan AutoSaveDelay = TimeSpan.FromMilliseconds(500);

        // DispatcherTimer running on UI thread to debounce saves
        private DispatcherTimer _autoSaveTimer;

        // Guard to avoid re-entrant saves
        private bool _isAutoSaving = false;

        // Call this from your change handlers instead of saving immediately.
        // Example call sites: collection CollectionChanged, item PropertyChanged, DataGrid.CellEditEnding, RowEditEnding, or Setup property change.
        private void ScheduleAutoSave()
        {
            try
            {
                // Lazily create the timer on the UI thread
                if (_autoSaveTimer == null)
                {
                    _autoSaveTimer = new DispatcherTimer(DispatcherPriority.Normal)
                    {
                        Interval = AutoSaveDelay
                    };
                    _autoSaveTimer.Tick += (s, e) =>
                    {
                        try
                        {
                            _autoSaveTimer.Stop();
                            DoAutoSave();
                        }
                        catch
                        {
                            // swallow to keep UI stable
                        }
                    };
                }

                // restart the timer (coalesces multiple calls into one)
                _autoSaveTimer.Stop();
                _autoSaveTimer.Start();
            }
            catch
            {
                // ignore
            }
        }

        // Actual save work; runs on UI thread. Uses a guard to avoid re-entrancy.
        private void DoAutoSave()
        {
            try
            {
                if (_isAutoSaving) return;
                _isAutoSaving = true;

                try
                {
                    if (!(DataContext is MainViewModel vm)) return;

                    // Indicate saving started on the VM so the UI can react
                    try { vm.IsAutoSaving = true; } catch { /* ignore */ }

                    // Prefer a silent save (no MessageBox)
                    try
                    {
                        vm.SaveSetupSilent();
                    }
                    catch
                    {
                        // fallback to SaveSetupCommand (may show UI)
                        try
                        {
                            if (vm.SaveSetupCommand != null && vm.SaveSetupCommand.CanExecute(null))
                                vm.SaveSetupCommand.Execute(null);
                        }
                        catch
                        {
                            // swallow
                        }
                    }
                }
                finally
                {
                    // Ensure UI flag cleared even on error
                    try { if (DataContext is MainViewModel vm2) vm2.IsAutoSaving = false; } catch { }
                    _isAutoSaving = false;
                }
            }
            catch
            {
                // ensure guard cleared
                try { if (DataContext is MainViewModel vm3) vm3.IsAutoSaving = false; } catch { }
                _isAutoSaving = false;
            }
        }
    }
}