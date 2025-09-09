using System;

namespace ScheduleApp.Models
{
    // Lightweight view model for items shown in the By Support list.
    public class SupportStaffEntry
    {
        public string Name { get; set; }
        public bool IsUnscheduled { get; set; }

        public SupportStaffEntry() { }

        public SupportStaffEntry(string name, bool isUnscheduled = false)
        {
            Name = name;
            IsUnscheduled = isUnscheduled;
        }

        public override string ToString() => Name;
    }
}