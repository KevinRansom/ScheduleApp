using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Controls;         // <- for ListBox, SelectionChangedEventArgs
using ScheduleApp.Models;             // <- for Teacher, Support

namespace ScheduleApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new ScheduleApp.ViewModels.MainViewModel();
        }

        // Ensure popup and toggle stay in sync: when popup closes (outside click or StaysOpen=false),
        // untoggle the hamburger button.
        private void HamburgerPopup_Closed(object sender, System.EventArgs e)
        {
            if (HamburgerToggle != null)
                HamburgerToggle.IsChecked = false;
        }

        // Navigate to the Schedule View tab (index 1) and close popup.
        private void MenuSchedule_Click(object sender, RoutedEventArgs e)
        {
            if (MainTabControl != null)
                MainTabControl.SelectedIndex = 1; // Schedule View

            // Also select the top-level Schedule inner tab "By Support" so the user lands on that view.
            // Safe-guard: only set if the inner TabControl is present.
            if (ScheduleViewInnerTabControl != null)
                ScheduleViewInnerTabControl.SelectedIndex = 0; // By Support

            if (HamburgerToggle != null)
                HamburgerToggle.IsChecked = false;
        }

        // Navigate to the Print Preview inner tab (inside Schedule View) and close popup.
        private void MenuPreview_Click(object sender, RoutedEventArgs e)
        {
            // Select the top-level Schedule View tab
            if (MainTabControl != null)
                MainTabControl.SelectedIndex = 1; // Schedule View

            // Select the inner Schedule tab "Print Preview" (index 2). Guard against null.
            if (ScheduleViewInnerTabControl != null)
                ScheduleViewInnerTabControl.SelectedIndex = 2; // Print Preview

            if (HamburgerToggle != null)
                HamburgerToggle.IsChecked = false;
        }

        // Navigate to Schedule View and select the By Teacher inner tab
        private void MenuTeachers_Click(object sender, RoutedEventArgs e)
        {
            // Select the top-level Schedule View tab
            if (MainTabControl != null)
                MainTabControl.SelectedIndex = 1; // Schedule View

            // Select the inner Schedule tab "By Teacher" (index 1). Guard against null.
            if (ScheduleViewInnerTabControl != null)
                ScheduleViewInnerTabControl.SelectedIndex = 1; // By Teacher

            if (HamburgerToggle != null)
                HamburgerToggle.IsChecked = false;
        }

        // Navigate to Setup tab (School area) — selects Setup but does not change inner tab.
        private void MenuSchool_Click(object sender, RoutedEventArgs e)
        {
            if (MainTabControl != null)
                MainTabControl.SelectedIndex = 0; // Setup
            if (HamburgerToggle != null)
                HamburgerToggle.IsChecked = false;
        }

        // Navigate to Setup tab and Preferences inner tab
        private void MenuPreferences_Click(object sender, RoutedEventArgs e)
        {
            if (MainTabControl != null)
                MainTabControl.SelectedIndex = 0; // Setup
            if (SetupInnerTab != null)
                SetupInnerTab.SelectedIndex = 2; // Classroom Support Preferences
            if (HamburgerToggle != null)
                HamburgerToggle.IsChecked = false;
        }

        // Algorithm placeholder: close popup and leave developer to wire actual behavior.
        private void MenuAlgorithm_Click(object sender, RoutedEventArgs e)
        {
            if (HamburgerToggle != null)
                HamburgerToggle.IsChecked = false;
            // TODO: wire actual algorithm dialog or action
        }

        // New: Exit application
        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            if (HamburgerToggle != null)
                HamburgerToggle.IsChecked = false;

            // Gracefully shut down the application
            Application.Current.Shutdown();
        }

        // Open the About (formerly Help) window with reduced content.
        private void AboutMenu_Click(object sender, RoutedEventArgs e)
        {
            if (HamburgerToggle != null)
                HamburgerToggle.IsChecked = false;

            var about = new AboutWindow
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            about.ShowDialog();
        }

        // Title-bar mouse handling: drag or double-click to toggle maximize/restore
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximizeRestore();
            }
            else
            {
                try
                {
                    DragMove();
                }
                catch
                {
                    // ignore if DragMove fails (e.g. during maximized state transitions)
                }
            }
        }

        private void ToggleMaximizeRestore()
        {
            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
            else
                WindowState = WindowState.Maximized;

            UpdateMaximizeIcon();
        }

        private void MaximizeRestore_Click(object sender, RoutedEventArgs e)
        {
            ToggleMaximizeRestore();
        }

        private void UpdateMaximizeIcon()
        {
            if (MaximizeButton == null) return;

            MaximizeButton.ApplyTemplate();
            var icon = MaximizeButton.Template.FindName("PART_Icon", MaximizeButton) as Path;
            if (icon == null) return;

            var geomKey = WindowState == WindowState.Maximized ? "RestoreGeometry" : "MaximizeGeometry";
            if (TryFindResource(geomKey) is Geometry g)
                icon.Data = g;
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // Ensure thumbs don't try to start a resize when window is maximized.
        private void ResizeThumb_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                e.Handled = true;
                return;
            }
        }

        // Central drag handler for all thumbs. Tag on each Thumb indicates direction.
        private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (WindowState == WindowState.Maximized) return;
            if (!(sender is Thumb t) || t.Tag == null) return;

            string dir = t.Tag.ToString();
            double horiz = e.HorizontalChange;
            double vert = e.VerticalChange;

            // Use ActualWidth/Height because Width/Height may be NaN if auto-sizing in some layouts.
            double curWidth = Math.Max(0.0, this.ActualWidth);
            double curHeight = Math.Max(0.0, this.ActualHeight);
            double newLeft = this.Left;
            double newTop = this.Top;
            double newWidth = curWidth;
            double newHeight = curHeight;

            // Helpers to clamp to MinWidth/MinHeight
            double minW = this.MinWidth > 0 ? this.MinWidth : 100;
            double minH = this.MinHeight > 0 ? this.MinHeight : 100;

            switch (dir)
            {
                case "Left":
                    newWidth = Math.Max(minW, curWidth - horiz);
                    newLeft = this.Left + (curWidth - newWidth);
                    break;
                case "Right":
                    newWidth = Math.Max(minW, curWidth + horiz);
                    break;
                case "Top":
                    newHeight = Math.Max(minH, curHeight - vert);
                    newTop = this.Top + (curHeight - newHeight);
                    break;
                case "Bottom":
                    newHeight = Math.Max(minH, curHeight + vert);
                    break;
                case "TopLeft":
                    newWidth = Math.Max(minW, curWidth - horiz);
                    newLeft = this.Left + (curWidth - newWidth);
                    newHeight = Math.Max(minH, curHeight - vert);
                    newTop = this.Top + (curHeight - newHeight);
                    break;
                case "TopRight":
                    newWidth = Math.Max(minW, curWidth + horiz);
                    newHeight = Math.Max(minH, curHeight - vert);
                    newTop = this.Top + (curHeight - newHeight);
                    break;
                case "BottomLeft":
                    newWidth = Math.Max(minW, curWidth - horiz);
                    newLeft = this.Left + (curWidth - newWidth);
                    newHeight = Math.Max(minH, curHeight + vert);
                    newTop = this.Top + (curHeight - newHeight);
                    break;
                case "BottomRight":
                    newWidth = Math.Max(minW, curWidth + horiz);
                    newHeight = Math.Max(minH, curHeight + vert);
                    break;
                default:
                    return;
            }

            // Apply computed values
            // Prevent negative width/height or moving window off-screen excessively; keep simple clamps
            if (newWidth >= minW)
            {
                this.Width = newWidth;
                this.Left = newLeft;
            }
            if (newHeight >= minH)
            {
                this.Height = newHeight;
                this.Top = newTop;
            }
        }

        // New: respond to multi-selection changes in the teacher list and update schedule view
        private void TeacherListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (!(DataContext is ScheduleApp.ViewModels.MainViewModel vm) || !(sender is ListBox lb))
                    return;

                // Use OfType to avoid invalid casts if SelectedItems contains unexpected wrappers
                var selectedTeachers = lb.SelectedItems.OfType<Teacher>().ToList();

                // If nothing selected, clear the view
                if (selectedTeachers.Count == 0)
                {
                    vm.Schedule.SelectedTeacherRows.Clear();
                    return;
                }

                // Ask the ScheduleViewModel to display rows for all selected teachers
                vm.Schedule.ShowTeachers(selectedTeachers);
            }
            catch
            {
                // swallow UI hookup errors to avoid breaking startup; real exceptions surface in debug
            }
        }

        // Updated selection handler to use SupportStaffEntry and map to Support by Name (no assignment changes).
        private void SupportListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (!(DataContext is ScheduleApp.ViewModels.MainViewModel vm) || !(sender is ListBox lb))
                    return;

                var selectedEntries = lb.SelectedItems.OfType<ScheduleApp.Models.SupportStaffEntry>().ToList();

                if (selectedEntries.Count == 0)
                {
                    vm.Schedule.SelectedSupportRows.Clear();
                    return;
                }

                // Map entries to lightweight Support objects by name so ScheduleViewModel.ShowSupports can reuse its lookup logic.
                var selectedSupports = selectedEntries.Select(en => new Support { Name = en.Name }).ToList();

                vm.Schedule.ShowSupports(selectedSupports);
            }
            catch
            {
                // swallow UI hookup errors to avoid breaking startup; real exceptions surface in debug
            }
        }

        // New: respond to inner Schedule View tab selection changes and regenerate schedule
        private void ScheduleViewInnerTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                // Only trigger when the TabControl is fully loaded (avoid initial XAML hookup)
                if (!(sender is TabControl tc) || !tc.IsLoaded) return;

                // Ignore SelectionChanged that bubbled from child controls (ListBox selections etc.)
                // Only proceed when the TabControl itself raised the event.
                if (!ReferenceEquals(e.OriginalSource, tc)) return;

                // Commit any pending edits in the Setup data grids so changes are pushed to the view model
                try
                {
                    // TeachersGrid and SupportsGrid are defined in XAML (Setup tab). Commit both cell and row edits.
                    TeachersGrid?.CommitEdit(DataGridEditingUnit.Cell, true);
                    TeachersGrid?.CommitEdit(DataGridEditingUnit.Row, true);

                    SupportsGrid?.CommitEdit(DataGridEditingUnit.Cell, true);
                    SupportsGrid?.CommitEdit(DataGridEditingUnit.Row, true);
                }
                catch
                {
                    // swallow commit errors; proceed to regenerate schedule
                }

                // Execute GenerateSchedule on the main view model if available
                if (DataContext is ScheduleApp.ViewModels.MainViewModel vm && vm.GenerateScheduleCommand != null)
                {
                    if (vm.GenerateScheduleCommand.CanExecute(null))
                        vm.GenerateScheduleCommand.Execute(null);
                }
            }
            catch
            {
                // swallow to avoid UI breaks; exceptions surface during debugging
            }
        }

        // Add this method to the MainWindow class (keeps behavior consistent with existing handlers)
        private void ScheduleArea_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (!(DataContext is ScheduleApp.ViewModels.MainViewModel vm)) return;

                // Run GenerateSchedule to pick up any edits made in Setup before showing schedule.
                var cmd = vm.GenerateScheduleCommand;
                if (cmd != null && cmd.CanExecute(null))
                    cmd.Execute(null);
            }
            catch
            {
                // swallow — keep UI responsive; debugging will show exceptions
            }
        }

        // Replace existing MainTabControl_SelectionChanged implementation with this
        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (!(sender is TabControl tc) || !tc.IsLoaded) return;

                // Only handle when the TabControl itself raised the event (avoid bubbled events).
                if (!ReferenceEquals(e.OriginalSource, tc)) return;

                // Only regenerate when the Schedule View tab is selected (index 1)
                if (tc.SelectedIndex != 1) return;

                // 1) Force the focused editor (ComboBox/TextBox inside DataGrid) to update its binding source.
                try
                {
                    var focused = Keyboard.FocusedElement as DependencyObject;
                    if (focused is FrameworkElement fe)
                    {
                        // Try common binding targets used inside the DataGrids:
                        var be = fe.GetBindingExpression(System.Windows.Controls.Primitives.Selector.SelectedItemProperty)
                                 ?? fe.GetBindingExpression(System.Windows.Controls.ComboBox.SelectedValueProperty)
                                 ?? fe.GetBindingExpression(System.Windows.Controls.Primitives.RangeBase.ValueProperty)
                                 ?? fe.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty);

                        be?.UpdateSource();
                    }
                }
                catch
                {
                    // ignore binding update failures; we'll still try commit below
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

                // 4) Run GenerateSchedule at ApplicationIdle so all UI/binding operations finish first
                if (DataContext is ScheduleApp.ViewModels.MainViewModel vm && vm.GenerateScheduleCommand != null)
                {
                    if (vm.GenerateScheduleCommand.CanExecute(null))
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                if (vm.GenerateScheduleCommand.CanExecute(null))
                                    vm.GenerateScheduleCommand.Execute(null);

                                // After regenerating the schedule, refresh the schedule view content for any
                                // currently-selected supports/teachers so the Selected*Rows collections
                                // are rebuilt from the new schedule data (prevents showing stale times).
                                try
                                {
                                    var selectedSupports = SupportListBox?.SelectedItems?.OfType<Support>().ToList();
                                    if (selectedSupports != null && selectedSupports.Count > 0)
                                    {
                                        vm.Schedule.ShowSupports(selectedSupports);
                                    }

                                    var selectedTeachers = TeacherListBox?.SelectedItems?.OfType<Teacher>().ToList();
                                    if (selectedTeachers != null && selectedTeachers.Count > 0)
                                    {
                                        vm.Schedule.ShowTeachers(selectedTeachers);
                                    }
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
                        }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("MainTabControl_SelectionChanged error: " + ex);
            }
        }
    }
}
