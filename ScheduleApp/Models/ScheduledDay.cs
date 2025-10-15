using System;
using System.Collections.Generic;
using System.Linq;
using ScheduleApp.Services;

namespace ScheduleApp.Models
{
    /// <summary>
    /// Test helper used by unit tests. Wraps a DayContext and provides a small helper
    /// to run the scheduler's teacher-coverage generation + assignment step.
    /// </summary>
    public class ScheduledDay
    {
        public DayContext Context { get; }

        public ScheduledDay(DateTime date, IEnumerable<Teacher> teachers, IEnumerable<Support> supports)
        {
            Context = new DayContext
            {
                Date = date.Date,
                Teachers = teachers?.ToList() ?? new List<Teacher>(),
                Supports = supports?.ToList() ?? new List<Support>(),
                Preferences = new List<RoomPreference>()
            };
        }

        /// <summary>
        /// Generate teacher coverage tasks and assign them to supports using the provided scheduler.
        /// Returns the by-support mapping produced by the scheduler.
        /// </summary>
        public Dictionary<string, List<CoverageTask>> AssignAndSchedule(SchedulerService scheduler)
        {
            if (scheduler == null) throw new ArgumentNullException(nameof(scheduler));

            var teacherTasks = scheduler.GenerateTeacherCoverageTasks(Context);
            var bySupport = scheduler.AssignSupportToTeacherTasks(Context, teacherTasks) ?? new Dictionary<string, List<CoverageTask>>();

            // Ensure Unscheduled bucket exists for tests that expect it
            if (!bySupport.ContainsKey("Unscheduled"))
                bySupport["Unscheduled"] = new List<CoverageTask>();

            return bySupport;
        }
    }
}