using System;
using System.Windows;
using System.Windows.Controls;

namespace ScheduleApp
{
    public partial class TeamLineupInnerControlClassroomsAndTeachers : UserControl
    {
        // Forwarding events so parent control can attach
        public event EventHandler<DataGridRowEditEndingEventArgs> SetupDataGridRowEditEnding;
        public event EventHandler<DataGridCellEditEndingEventArgs> SetupDataGridCellEditEnding;

        public TeamLineupInnerControlClassroomsAndTeachers()
        {
            InitializeComponent();
        }

        private void OnSetupDataGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            SetupDataGridRowEditEnding?.Invoke(sender, e);
        }

        private void OnSetupDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            SetupDataGridCellEditEnding?.Invoke(sender, e);
        }
    }
}