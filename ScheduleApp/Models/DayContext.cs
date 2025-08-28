using System;
using System.Collections.Generic;

namespace ScheduleApp.Models
{
    public class DayContext
    {
        public DateTime Date { get; set; }
        public List<Teacher> Teachers { get; set; }
        public List<Support> Supports { get; set; }
        public List<RoomPreference> Preferences { get; set; }

        public DayContext()
        {
            Teachers = new List<Teacher>();
            Supports = new List<Support>();
            Preferences = new List<RoomPreference>();
        }
    }
}