using System;
using System.Windows;
using System.Windows.Controls;

namespace ScheduleApp
{
    public partial class TeamLineupInnerTab : UserControl
    {
        public event EventHandler<DataGridRowEditEndingEventArgs> SetupDataGridRowEditEnding;
        public event EventHandler<DataGridCellEditEndingEventArgs> SetupDataGridCellEditEnding;

        public TeamLineupInnerTab()
        {
            InitializeComponent();
        }

        // Expose inner TabControl selection to parent windows.
        public int SelectedIndex
        {
            get => InnerTabControl?.SelectedIndex ?? -1;
            set
            {
                if (InnerTabControl != null &&
                    value >= 0 &&
                    value < InnerTabControl.Items.Count)
                {
                    InnerTabControl.SelectedIndex = value;
                }
            }
        }

        public void OnSetupDataGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
            => SetupDataGridRowEditEnding?.Invoke(sender, e);

        public void OnSetupDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
            => SetupDataGridCellEditEnding?.Invoke(sender, e);

        private void OnChildSetupDataGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
            => SetupDataGridRowEditEnding?.Invoke(sender, e);

        private void OnChildSetupDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
            => SetupDataGridCellEditEnding?.Invoke(sender, e);
    }
}