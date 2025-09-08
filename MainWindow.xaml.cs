using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Controls;         // <- for ListBox, SelectionChangedEventArgs
using ScheduleApp.Models;             // <- for Teacher

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
            if (HamburgerToggle != null)
                HamburgerToggle.IsChecked = false;
        }

        // Navigate to the Print Preview tab (index 2) and close popup.
        private void MenuPreview_Click(object sender, RoutedEventArgs e)
        {
            if (MainTabControl != null)
                MainTabControl.SelectedIndex = 2; // Print Preview
            if (HamburgerToggle != null)
                HamburgerToggle.IsChecked = false;
        }

        // Navigate to Setup tab and inner Teachers tab
        private void MenuTeachers_Click(object sender, RoutedEventArgs e)
        {
            if (MainTabControl != null)
                MainTabControl.SelectedIndex = 0; // Setup
            if (SetupInnerTab != null)
                SetupInnerTab.SelectedIndex = 0; // Classrooms and Teachers
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
                if (DataContext is ScheduleApp.ViewModels.MainViewModel vm && sender is ListBox lb)
                {
                    var selected = lb.SelectedItems.Cast<Teacher>().ToList();
                    // Ask the ScheduleViewModel to display rows for all selected teachers
                    vm.Schedule.ShowTeachers(selected);
                }
            }
            catch
            {
                // swallow UI hookup errors to avoid breaking startup; real exceptions surface in debug
            }
        }
    }
}
