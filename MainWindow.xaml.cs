using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes; // <- added to resolve 'Path'
using System.Linq; // for OfType
using System.ComponentModel; // using for INotifyPropertyChanged
using System.Collections.Specialized; // for NotifyCollectionChangedEventArgs

namespace ScheduleApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new ScheduleApp.ViewModels.MainViewModel();

            // Auto-save hookup: subscribe to setup collection / property notifications.
            var vm = DataContext as ScheduleApp.ViewModels.MainViewModel;
            if (vm != null && vm.Setup != null)
            {
                // Top-level setup property changes (SchoolName, SaveFolder, etc.)
                vm.Setup.PropertyChanged += Setup_PropertyChanged;

                // Collections: additions/removals/moves
                vm.Setup.Teachers.CollectionChanged += SetupCollectionChanged;
                vm.Setup.Supports.CollectionChanged += SetupCollectionChanged;
                vm.Setup.Preferences.CollectionChanged += SetupCollectionChanged;

                // If items implement INotifyPropertyChanged, listen for item-level changes
                foreach (var it in vm.Setup.Teachers.OfType<INotifyPropertyChanged>())
                    it.PropertyChanged += Item_PropertyChanged;
                foreach (var it in vm.Setup.Supports.OfType<INotifyPropertyChanged>())
                    it.PropertyChanged += Item_PropertyChanged;
                foreach (var it in vm.Setup.Preferences.OfType<INotifyPropertyChanged>())
                    it.PropertyChanged += Item_PropertyChanged;
            }
        }

        // Ensure popup and toggle stay in sync: when popup closes (outside click or StaysOpen=false),
        // untoggle the hamburger button.
        private void HamburgerPopup_Closed(object sender, System.EventArgs e)
        {
            if (HamburgerToggle != null)
                HamburgerToggle.IsChecked = false;
        }

        // Navigate to the Schedule tab (top-level index 0) and close popup.
        private void MenuSchedule_Click(object sender, RoutedEventArgs e)
        {
            if (MainTabControl != null)
                MainTabControl.SelectedIndex = 0; // Schedule

            // Also select the top-level Schedule inner tab "By Support"
            if (ScheduleViewInnerTabControl != null)
                ScheduleViewInnerTabControl.SelectedIndex = 0; // By Support

            if (HamburgerToggle != null)
                HamburgerToggle.IsChecked = false;
        }

        // Navigate to the Print Preview inner tab (inside Schedule) and close popup.
        private void MenuPreview_Click(object sender, RoutedEventArgs e)
        {
            if (MainTabControl != null)
                MainTabControl.SelectedIndex = 0; // Schedule

            if (ScheduleViewInnerTabControl != null)
                ScheduleViewInnerTabControl.SelectedIndex = 2; // Print Preview

            if (HamburgerToggle != null)
                HamburgerToggle.IsChecked = false;
        }

        // Navigate to Schedule and select the By Teacher inner tab
        private void MenuTeachers_Click(object sender, RoutedEventArgs e)
        {
            if (MainTabControl != null)
                MainTabControl.SelectedIndex = 0; // Schedule

            if (ScheduleViewInnerTabControl != null)
                ScheduleViewInnerTabControl.SelectedIndex = 1; // By Teacher

            if (HamburgerToggle != null)
                HamburgerToggle.IsChecked = false;
        }

        // Navigate to Preferences top-level tab (was School Details)
        private void MenuPreferences_Click(object sender, RoutedEventArgs e)
        {
            if (MainTabControl != null)
                MainTabControl.SelectedIndex = 2; // Preferences (top-level)

            // close hamburger
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

        // Prevent starting a drag when maximized.
        private void ResizeThumb_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                e.Handled = true;
                return;
            }
        }

        // Edge handlers
        private void ThumbLeft_DragDelta(object sender, DragDeltaEventArgs e) => ResizeFrom("Left", e.HorizontalChange, e.VerticalChange);
        private void ThumbRight_DragDelta(object sender, DragDeltaEventArgs e) => ResizeFrom("Right", e.HorizontalChange, e.VerticalChange);
        private void ThumbTop_DragDelta(object sender, DragDeltaEventArgs e) => ResizeFrom("Top", e.HorizontalChange, e.VerticalChange);
        private void ThumbBottom_DragDelta(object sender, DragDeltaEventArgs e) => ResizeFrom("Bottom", e.HorizontalChange, e.VerticalChange);

        // Corner handlers
        private void ThumbTopLeft_DragDelta(object sender, DragDeltaEventArgs e) => ResizeFrom("TopLeft", e.HorizontalChange, e.VerticalChange);
        private void ThumbTopRight_DragDelta(object sender, DragDeltaEventArgs e) => ResizeFrom("TopRight", e.HorizontalChange, e.VerticalChange);
        private void ThumbBottomLeft_DragDelta(object sender, DragDeltaEventArgs e) => ResizeFrom("BottomLeft", e.HorizontalChange, e.VerticalChange);
        private void ThumbBottomRight_DragDelta(object sender, DragDeltaEventArgs e) => ResizeFrom("BottomRight", e.HorizontalChange, e.VerticalChange);

        // Shared helper that applies the resize. Deltas are in device-independent units (DIPs).
        // If you need pixel-level math you can query DPI via VisualTreeHelper.GetDpi(this).
        private void ResizeFrom(string direction, double horizChange, double vertChange)
        {
            try
            {
                if (WindowState == WindowState.Maximized) return;

                // WPF DragDelta provides changes in device independent pixels (DIPs) already.
                // If you need the system DPI scale for any pixel conversions:
                // var dpi = VisualTreeHelper.GetDpi(this);
                // double scaleX = dpi.DpiScaleX; double scaleY = dpi.DpiScaleY;

                // Current window extents (use ActualWidth/Height for current rendered size)
                double curWidth = Math.Max(0.0, this.ActualWidth);
                double curHeight = Math.Max(0.0, this.ActualHeight);

                // Proposed new values
                double newLeft = this.Left;
                double newTop = this.Top;
                double newWidth = curWidth;
                double newHeight = curHeight;

                // Minimums
                double minW = (this.MinWidth > 0.0) ? this.MinWidth : 100.0;
                double minH = (this.MinHeight > 0.0) ? this.MinHeight : 100.0;

                switch (direction)
                {
                    case "Left":
                        // moving mouse right (horizChange > 0) shrinks window; leftwards expands
                        newWidth = Math.Max(minW, curWidth - horizChange);
                        newLeft = this.Left + (curWidth - newWidth);
                        break;

                    case "Right":
                        newWidth = Math.Max(minW, curWidth + horizChange);
                        break;

                    case "Top":
                        newHeight = Math.Max(minH, curHeight - vertChange);
                        newTop = this.Top + (curHeight - newHeight);
                        break;

                    case "Bottom":
                        newHeight = Math.Max(minH, curHeight + vertChange);
                        break;

                    case "TopLeft":
                        newWidth = Math.Max(minW, curWidth - horizChange);
                        newLeft = this.Left + (curWidth - newWidth);
                        newHeight = Math.Max(minH, curHeight - vertChange);
                        newTop = this.Top + (curHeight - newHeight);
                        break;

                    case "TopRight":
                        newWidth = Math.Max(minW, curWidth + horizChange);
                        newHeight = Math.Max(minH, curHeight - vertChange);
                        newTop = this.Top + (curHeight - newHeight);
                        break;

                    case "BottomLeft":
                        newWidth = Math.Max(minW, curWidth - horizChange);
                        newLeft = this.Left + (curWidth - newWidth);
                        newHeight = Math.Max(minH, curHeight + vertChange);
                        newTop = this.Top + (curHeight - newHeight);
                        break;

                    case "BottomRight":
                        newWidth = Math.Max(minW, curWidth + horizChange);
                        newHeight = Math.Max(minH, curHeight + vertChange);
                        break;

                    default:
                        return;
                }

                // Apply values — set Left/Top only when size change occurred to avoid visual jitter.
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
            catch
            {
                // Keep UI stable if anything unexpected happens.
            }
        }
    }
}
