using System;
using System.Windows.Controls;

namespace ScheduleApp
{
    public partial class TeamLineupInnerControlSupportStaff : UserControl
    {
        // Events parent expects to subscribe to (same pattern used by other inner controls)
        public event EventHandler<DataGridRowEditEndingEventArgs> SetupDataGridRowEditEnding;
        public event EventHandler<DataGridCellEditEndingEventArgs> SetupDataGridCellEditEnding;

        public TeamLineupInnerControlSupportStaff()
        {
            InitializeComponent();
        }

        // Forward DataGrid events to the public events so the parent (TeamLineupInnerTab) receives them
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