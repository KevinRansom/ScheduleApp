using System;

namespace ScheduleApp.Models
{
    public class Teacher
    {
        public string RoomNumber { get; set; }
        public string Name { get; set; }
        public TimeSpan Start { get; set; }
        public TimeSpan End { get; set; }

        public double ShiftHours
        {
            get
            {
                var span = End - Start;
                return Math.Max(0, span.TotalHours);
            }
        }

        public bool LunchRequired
        {
            get { return ShiftHours > 5.0; }
        }
    }
}