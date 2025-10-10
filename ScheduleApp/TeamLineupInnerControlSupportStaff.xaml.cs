using System;
using System.Windows;
using System.Windows.Controls;

namespace ScheduleApp
{
    public partial class TeamLineupInnerControlSupportStaff : UserControl
    {
        // Expose events so parent XAML can attach handlers:
        // <local:TeamLineupInnerControlSupportStaff SetupDataGridRowEditEnding="..." />
        public event EventHandler<DataGridRowEditEndingEventArgs> SetupDataGridRowEditEnding;
        public event EventHandler<DataGridCellEditEndingEventArgs> SetupDataGridCellEditEnding;

        public TeamLineupInnerControlSupportStaff()
        {
            InitializeComponent();
        }

        // Private handlers wired to the internal DataGrid in XAML.
        // They forward the calls to the public events above so the parent control can subscribe.
        private void OnSetupDataGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            // Forward to any external subscriber (parent)
            SetupDataGridRowEditEnding?.Invoke(this, e);

            // Intentionally left blank for internal behavior — VM handles logic in current design.
        }

        private void OnSetupDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            // Forward to any external subscriber (parent)
            SetupDataGridCellEditEnding?.Invoke(this, e);

            // Intentionally left blank for internal behavior — VM handles logic in current design.
        }
    }
}