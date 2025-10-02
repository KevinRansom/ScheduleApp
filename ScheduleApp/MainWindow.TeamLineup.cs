using System.Windows;

namespace ScheduleApp
{
    public partial class MainWindow
    {
        private void TeamLineupButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (MainTabControl != null)
                    MainTabControl.SelectedIndex = 1;

                // Select "Classrooms and Teachers" inner tab (index 0)
                if (TeamLineupInnerTabControl != null)
                    TeamLineupInnerTabControl.SelectedIndex = 0;
            }
            catch
            {
                // best-effort; do not throw from UI handler
            }
        }
    }
}