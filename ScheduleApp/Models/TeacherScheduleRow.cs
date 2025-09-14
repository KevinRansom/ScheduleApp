using System;

namespace ScheduleApp.Models
{
    public class TeacherScheduleRow
    {
        public string TeacherName { get; set; }
        public string SupportStaff { get; set; } // empty for Start of Day
        public string Activity { get; set; }     // "Start of Day", "Break", "Lunch"
        public string Duration { get; set; }     // e.g. "10min", "30min", empty for Start of Day
        public string Start { get; set; }        // "HH:mm" display
        public DateTime SortKey { get; set; }    // used to sort rows by time
    }
}