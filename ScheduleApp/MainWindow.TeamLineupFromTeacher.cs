using System.Windows;

namespace ScheduleApp
{
    public partial class MainWindow
    {
        private void TeamLineupFromTeacher_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Switch to Team Lineup top-level tab (index 1)
                if (MainTabControl != null)
                    MainTabControl.SelectedIndex = 1;

                // Select "Classrooms and Teachers" inner tab (index 0)
                if (TeamLineupInnerTabControl != null)
                    TeamLineupInnerTabControl.SelectedIndex = 0;
            }
            catch
            {
                // best-effort; don't crash UI
            }
        }
    }
}