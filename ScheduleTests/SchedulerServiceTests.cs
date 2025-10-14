using Xunit;
using System;
using System.Linq;
using System.Collections.Generic;
using ScheduleApp.Services;
using ScheduleApp.Models;

namespace ScheduleTests
{
    public class SchedulerServiceTests
    {
        private readonly SchedulerService _scheduler = new SchedulerService();

        private DayContext MakeDay(DateTime date, IEnumerable<Teacher> teachers, IEnumerable<Support> supports)
        {
            return new DayContext
            {
                Date = date.Date,
                Teachers = teachers.ToList(),
                Supports = supports.ToList(),
                Preferences = new List<RoomPreference>()
            };
        }

        [Fact]
        public void Teacher7To15_Support9_30To15_AssignsBreaksAndLunches()
        {
            var date = new DateTime(2025, 10, 13);
            var teacher = new Teacher { Name = "T1", RoomNumber = "R1", Start = new TimeSpan(7, 0, 0), End = new TimeSpan(15, 0, 0) };
            var support = new Support { Name = "S1", Start = new TimeSpan(9, 30, 0), End = new TimeSpan(15, 0, 0) };

            var day = MakeDay(date, new[] { teacher }, new[] { support });

            var teacherTasks = _scheduler.GenerateTeacherCoverageTasks(day);
            // teacher should have a lunch (30min) and at least one break (10min)
            Assert.Contains(teacherTasks, t => t.TeacherName == "T1" && (t.End - t.Start).TotalMinutes == 30);
            Assert.Contains(teacherTasks, t => t.TeacherName == "T1" && (t.End - t.Start).TotalMinutes == 10);

            var bySupport = _scheduler.AssignSupportToTeacherTasks(day, teacherTasks);

            // support should have been assigned the teacher coverage tasks
            Assert.True(bySupport.ContainsKey("S1"));
            var assigned = bySupport["S1"];
            Assert.Contains(assigned, t => t.TeacherName == "T1" && (t.End - t.Start).TotalMinutes == 30);
            Assert.Contains(assigned, t => t.TeacherName == "T1" && (t.End - t.Start).TotalMinutes == 10);

            // schedule support self-care (should add Lunch/Break for support as needed)
            _scheduler.ScheduleSupportSelfCare(day, bySupport);

            Assert.Contains(bySupport["S1"], t => t.Kind == CoverageTaskKind.Lunch);
            Assert.Contains(bySupport["S1"], t => t.Kind == CoverageTaskKind.Break);
        }

        [Fact]
        public void Teacher7To11_Support9To11_AssignsBreaks_NoLunches()
        {
            var date = new DateTime(2025, 10, 13);
            var teacher = new Teacher { Name = "T2", RoomNumber = "R2", Start = new TimeSpan(7, 0, 0), End = new TimeSpan(11, 0, 0) };
            var support = new Support { Name = "S2", Start = new TimeSpan(9, 0, 0), End = new TimeSpan(11, 0, 0) };

            var day = MakeDay(date, new[] { teacher }, new[] { support });

            var teacherTasks = _scheduler.GenerateTeacherCoverageTasks(day);

            // teacher shift is 4 hours -> expect at least one 10min break and no 30min lunch
            Assert.Contains(teacherTasks, t => t.TeacherName == "T2" && (t.End - t.Start).TotalMinutes == 10);
            Assert.DoesNotContain(teacherTasks, t => t.TeacherName == "T2" && (t.End - t.Start).TotalMinutes == 30);

            var bySupport = _scheduler.AssignSupportToTeacherTasks(day, teacherTasks);

            Assert.True(bySupport.ContainsKey("S2"));
            var assigned = bySupport["S2"];

            // support should have at least one assigned break coverage
            Assert.Contains(assigned, t => t.TeacherName == "T2" && (t.End - t.Start).TotalMinutes == 10);

            // schedule self-care: support should NOT have a Lunch (shift < 5h)
            _scheduler.ScheduleSupportSelfCare(day, bySupport);
            Assert.DoesNotContain(bySupport["S2"], t => t.Kind == CoverageTaskKind.Lunch);
            Assert.Contains(bySupport["S2"], t => t.Kind == CoverageTaskKind.Break || t.Kind == CoverageTaskKind.Idle);
        }

        [Fact]
        public void Teacher7To15_NoSupport_ReportsUnscheduledBreaksAndLunch()
        {
            var date = new DateTime(2025, 10, 13);
            var teacher = new Teacher { Name = "T3", RoomNumber = "R3", Start = new TimeSpan(7, 0, 0), End = new TimeSpan(15, 0, 0) };

            var day = MakeDay(date, new[] { teacher }, Enumerable.Empty<Support>());

            var teacherTasks = _scheduler.GenerateTeacherCoverageTasks(day);

            // Expect lunch + breaks present as teacher coverage tasks
            Assert.Contains(teacherTasks, t => t.TeacherName == "T3" && (t.End - t.Start).TotalMinutes == 30);
            Assert.Contains(teacherTasks, t => t.TeacherName == "T3" && (t.End - t.Start).TotalMinutes == 10);

            var bySupport = _scheduler.AssignSupportToTeacherTasks(day, teacherTasks);

            // No supports -> all teacher coverage tasks should be in Unscheduled bucket
            Assert.True(bySupport.ContainsKey("Unscheduled"));
            var unscheduled = bySupport["Unscheduled"];
            Assert.Equal(teacherTasks.Count, unscheduled.Count);

            // Verify unscheduled contains both lunch and break tasks for the teacher
            Assert.Contains(unscheduled, t => t.TeacherName == "T3" && (t.End - t.Start).TotalMinutes == 30);
            Assert.Contains(unscheduled, t => t.TeacherName == "T3" && (t.End - t.Start).TotalMinutes == 10);
        }
    }
}