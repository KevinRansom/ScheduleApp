using System.Windows;

namespace ScheduleApp
{
    public partial class MainWindow
    {
        private void TeamLineupButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Switch to Team Lineup top-level tab (index 1) and select Classrooms and Teachers inner tab (index 0)
                if (MainTabControl != null)
                    MainTabControl.SelectedIndex = 1;

                if (SetupInnerTab != null)
                    SetupInnerTab.SelectedIndex = 0;
            }
            catch
            {
                // best-effort; do not throw from UI handler
            }
        }
    }
}