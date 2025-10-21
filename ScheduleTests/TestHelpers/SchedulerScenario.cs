using System;
using System.Collections.Generic;
using System.Linq;
using ScheduleApp.Models;
using ScheduleApp.Services;

namespace ScheduleTests.TestHelpers
{
    // Encapsulates the common scheduler flow used by tests:
    // Build inputs -> GenerateTeacherCoverageTasks -> AssignSupportToTeacherTasks (optionally ScheduleSupportSelfCare)

    public sealed class SchedulerScenario
    {
        private readonly DateTime _date;
        private readonly List<Teacher> _teachers = new List<Teacher>();
        private readonly List<Support> _supports = new List<Support>();

        public DayContext Day { get; private set; }
        public List<CoverageTask> TeacherTasks { get; private set; }
        public Dictionary<string, List<CoverageTask>> BySupport { get; private set; }

        private SchedulerScenario(DateTime date) => _date = date.Date;

        public static SchedulerScenario For(DateTime date) => new SchedulerScenario(date);

        public SchedulerScenario AddTeacher(Action<TeacherBuilder> configure)
        {
            var b = TeacherBuilder.Create();
            configure?.Invoke(b);
            _teachers.Add(b.Build());
            return this;
        }

        public SchedulerScenario AddSupport(Action<SupportBuilder> configure)
        {
            var b = SupportBuilder.Create();
            configure?.Invoke(b);
            _supports.Add(b.Build());
            return this;
        }

        public SchedulerScenario Run(SchedulerService scheduler)
        {
            Day = new DayContext
            {
                Date = _date,
                Teachers = _teachers.ToList(),
                Supports = _supports.ToList(),
                Preferences = new List<RoomPreference>()
            };

            TeacherTasks = scheduler.GenerateTeacherCoverageTasks(Day);
            BySupport = scheduler.AssignSupportToTeacherTasks(Day, TeacherTasks);
            return this;
        }

        public SchedulerScenario ScheduleSelfCare(SchedulerService scheduler)
        {
            if (Day == null || BySupport == null) throw new InvalidOperationException("Run() must be called before ScheduleSelfCare().");
            scheduler.ScheduleSupportSelfCare(Day, BySupport);
            return this;
        }
    }
}