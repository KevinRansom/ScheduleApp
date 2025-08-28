using System;
using System.Collections.ObjectModel;

namespace ScheduleApp.ViewModels
{
    public class ScheduleViewModel : BaseViewModel
    {
        public ObservableCollection<SupportTabViewModel> SupportTabs { get; } = new ObservableCollection<SupportTabViewModel>();

        // View mode: "Text", "Visual", "Grid"
        private string _viewMode = "Visual";
        public string ViewMode
        {
            get { return _viewMode; }
            set
            {
                if (_viewMode == value) return;
                _viewMode = value;
                Raise();
                Raise(nameof(ShowText));
                Raise(nameof(ShowVisual));
                Raise(nameof(ShowGrid));
            }
        }

        public bool ShowText  { get { return string.Equals(ViewMode, "Text",   StringComparison.OrdinalIgnoreCase); } }
        public bool ShowVisual{ get { return string.Equals(ViewMode, "Visual", StringComparison.OrdinalIgnoreCase); } }
        public bool ShowGrid  { get { return string.Equals(ViewMode, "Grid",   StringComparison.OrdinalIgnoreCase); } }

        public void LoadTabs(SupportTabViewModel[] tabs)
        {
            SupportTabs.Clear();
            for (int i = 0; i < tabs.Length; i++)
            {
                SupportTabs.Add(tabs[i]);
            }
            Raise(nameof(SupportTabs));
        }
    }
}