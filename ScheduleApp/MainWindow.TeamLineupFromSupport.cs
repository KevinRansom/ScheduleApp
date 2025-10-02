using System.Windows;

namespace ScheduleApp
{
    public partial class MainWindow
    {
        private void TeamLineupFromSupport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (MainTabControl != null)
                    MainTabControl.SelectedIndex = 1;

                // Support Staff tab (index 1)
                if (TeamLineupInnerTabControl != null)
                    TeamLineupInnerTabControl.SelectedIndex = 1;
            }
            catch
            {
                // Best-effort; do not throw from UI handler
            }
        }
    }
}