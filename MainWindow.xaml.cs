using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

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
    }
}
