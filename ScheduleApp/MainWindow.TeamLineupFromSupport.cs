using System.Windows;

namespace ScheduleApp
{
    public partial class MainWindow
    {
        private void TeamLineupFromSupport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Switch to Team Lineup top-level tab (index 1)
                if (MainTabControl != null)
                    MainTabControl.SelectedIndex = 1;

                // Select "Support Staff" inner tab (index 1)
                if (TeamLineupInnerTab != null)
                    TeamLineupInnerTab.SelectedIndex = 1;
            }
            catch
            {
                // Best-effort; do not throw from UI handler
            }
        }
    }
}