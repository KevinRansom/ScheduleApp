using System;
using System.Windows.Controls;
using System.Windows;
using ScheduleApp.ViewModels;

namespace ScheduleApp
{
    public partial class ScheduleViewInnerTabContentControl : UserControl
    {
        public ScheduleViewInnerTabContentControl()
        {
            InitializeComponent();
        }

        // Public surface for MainWindow to control inner selection
        public void SelectInnerTab(int index)
        {
            try
            {
                if (ScheduleViewInnerTabControl != null &&
                    index >= 0 &&
                    index < ScheduleViewInnerTabControl.Items.Count)
                {
                    ScheduleViewInnerTabControl.SelectedIndex = index;
                }
            }
            catch { /* ignore */ }
        }

        // Re-apply selection to underlying view models (delegates to child controls)
        public void RefreshSelectionOnViewModel()
        {
            try
            {
                if (!(DataContext is MainViewModel)) return;

                SupportTabContent?.RefreshSelectionOnViewModel();
                TeacherTabContent?.RefreshSelectionOnViewModel();
            }
            catch { /* ignore */ }
        }

        private void ScheduleViewInnerTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (!(sender is TabControl tc) || !tc.IsLoaded) return;
                if (!ReferenceEquals(e.OriginalSource, tc)) return;

                // Commit any pending edits in external grids (Teachers/Supports) before regeneration
                try
                {
                    var mw = Application.Current?.MainWindow as Window;
                    var teachersGrid = (mw as FrameworkElement)?.FindName("TeachersGrid") as DataGrid;
                    var supportsGrid = (mw as FrameworkElement)?.FindName("SupportsGrid") as DataGrid;

                    teachersGrid?.CommitEdit(DataGridEditingUnit.Cell, true);
                    teachersGrid?.CommitEdit(DataGridEditingUnit.Row, true);
                    supportsGrid?.CommitEdit(DataGridEditingUnit.Cell, true);
                    supportsGrid?.CommitEdit(DataGridEditingUnit.Row, true);
                }
                catch { /* ignore */ }

                if (DataContext is MainViewModel vm &&
                    vm.GenerateScheduleCommand != null &&
                    vm.GenerateScheduleCommand.CanExecute(null))
                {
                    try { vm.GenerateScheduleCommand.Execute(null); } catch { /* ignore */ }
                }
            }
            catch { /* ignore */ }
        }
    }
}