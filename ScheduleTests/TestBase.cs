using System;
using System.Linq;
using System.Collections.Generic;
using ScheduleApp.Services;
using ScheduleApp.Models;

namespace ScheduleTests
{
    public abstract class TestBase
    {
        protected readonly SchedulerService _scheduler = new SchedulerService();

        protected DayContext MakeDay(DateTime date, IEnumerable<Teacher> teachers, IEnumerable<Support> supports)
        {
            return new DayContext
            {
                Date = date.Date,
                Teachers = teachers.ToList(),
                Supports = supports.ToList(),
                Preferences = new List<RoomPreference>()
            };
        }

        // New factory for the ScheduledDay helper used by tests
        protected ScheduledDay MakeScheduledDay(DateTime date, IEnumerable<Teacher> teachers, IEnumerable<Support> supports)
        {
            return new ScheduledDay(date, teachers, supports);
        }
    }
}