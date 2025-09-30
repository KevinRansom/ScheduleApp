using System;
using System.Windows;
using System.Windows.Controls;

namespace ScheduleApp
{
    public partial class TeamLineupInnerTab : UserControl
    {
        // Expose events so MainWindow can attach its existing handlers
        public event EventHandler<DataGridRowEditEndingEventArgs> SetupDataGridRowEditEnding;
        public event EventHandler<DataGridCellEditEndingEventArgs> SetupDataGridCellEditEnding;

        public TeamLineupInnerTab()
        {
            InitializeComponent();

            // If the child control is present, forward its events to consumers of this control.
            if (ClassroomsAndTeachersControl != null)
            {
                ClassroomsAndTeachersControl.SetupDataGridRowEditEnding += (s, e) => SetupDataGridRowEditEnding?.Invoke(s, e);
                ClassroomsAndTeachersControl.SetupDataGridCellEditEnding += (s, e) => SetupDataGridCellEditEnding?.Invoke(s, e);
            }
        }

        // Handlers expected by XAML (forward to public events)
        public void OnSetupDataGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            // forward from any nested control's DataGrid
            SetupDataGridRowEditEnding?.Invoke(sender, e);
        }

        public void OnSetupDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            // forward from any nested control's DataGrid
            SetupDataGridCellEditEnding?.Invoke(sender, e);
        }

        // Internal handlers used when the child UserControl raises events (keeps naming parity)
        private void OnChildSetupDataGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            SetupDataGridRowEditEnding?.Invoke(sender, e);
        }

        private void OnChildSetupDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            SetupDataGridCellEditEnding?.Invoke(sender, e);
        }
    }
}