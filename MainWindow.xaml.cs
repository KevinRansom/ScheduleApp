using System.Windows;

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
    }
}
