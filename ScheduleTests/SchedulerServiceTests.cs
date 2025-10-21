using Xunit;
using System;
using System.Linq;
using System.Collections.Generic;
using ScheduleApp.Models;
using ScheduleTests.TestHelpers;

namespace ScheduleTests
{
    public class SchedulerServiceTests : TestBase
    {
        [Fact]
        public void Teacher7To15_Support9_30To15_AssignsBreaksAndLunches()
        {
            var date = new DateTime(2025, 10, 13);

            var scenario = SchedulerScenario.For(date)
                .AddTeacher(t => t.Named("T1").InRoom("R1").StartsAt(7, 0).EndsAt(15, 0))
                .AddSupport(s => s.Named("S1").StartsAt(9, 30).EndsAt(15, 0))
                .Run(_scheduler)
                .ScheduleSelfCare(_scheduler);

            // High-level subset validation (ExpectedScheduleAssertions) for the support schedule
            var expected = new ExpectedScheduleBuilder()
                .Support("S1")
                .Coverage("S1", "T1", 30)
                .Coverage("S1", "T1", 10)
                .SelfCare("S1", CoverageTaskKind.Lunch, 30)
                .SelfCare("S1", CoverageTaskKind.Break, 10)
                .Build();

            ExpectedScheduleAssertions.AssertEquivalent(scenario.BySupport, expected);

            // Exact, ordered timeline for S1 (times + durations)
            var timeline = new ExpectedTimeline(date)
                .ForSupport("S1")
                    .Coverage("T1", "09:30", 10)
                    .Free(          "09:45", 75)
                    .Coverage("T1", "11:00", 30)
                    .Free(          "11:30", 45)
                    .Lunch(         "12:15", 30)
                    .Break(         "12:45", 10)
                    .Free(          "13:00", 30)
                    .Coverage("T1", "13:30", 10)
                    .Free(          "13:45", 75)
                .End()
                .Build();

            TimelineAssertions.AssertSupportTimelineEquals(
                scenario.BySupport,
                "S1",
                timeline["S1"]);

            // Teacher schedule: use builder with times for readability
            var expectedTeacher = new ExpectedTeacherScheduleBuilder(date)
                .Coverage("T1", "09:00", 10)
                .Coverage("T1", "11:00", 30)
                .Coverage("T1", "13:30", 10)
                .Build();

            // Exact match: presence, times (tolerance 0), and no extras
            ExpectedScheduleAssertions.AssertTeacherTasksExactlyEqual(
                scenario.TeacherTasks,
                "T1",
                expectedTeacher,
                startToleranceMinutes: 0);
        }

        [Fact]
        public void Teacher7To11_Support9To11_AssignsBreaks_NoLunches()
        {
            var date = new DateTime(2025, 10, 13);

            var scenario = SchedulerScenario
                           .For(date)
                           .AddTeacher(t => t.Named("T2").InRoom("R2").StartsAt(7, 0).EndsAt(11, 0))
                           .AddSupport(s => s.Named("S2").StartsAt(9, 0).EndsAt(11, 0))
                           .Run(_scheduler)
                           .ScheduleSelfCare(_scheduler);

            // Exact, ordered expected timeline for S2:
            // - one 10m coverage at 09:00 (teacher T2)
            // - remaining time filled with Idle (self time) after the 5m buffer (starts 09:15)
            var expectedTimeline = new ExpectedTimeline(date)
                .ForSupport("S2")
                    .Coverage("T2", "09:00", 10)
                    .Free(          "09:15", 105)
                .End()
                .Build();

            TimelineAssertions.AssertSupportTimelineEquals(scenario.BySupport,"S2", expectedTimeline["S2"]);
        }

        [Fact]
        public void Teacher7To15_NoSupport_ReportsUnscheduledBreaksAndLunch()
        {
            var date = new DateTime(2025, 10, 13);

            var scenario = SchedulerScenario.For(date)
                .AddTeacher(t => t.Named("T3").InRoom("R3").StartsAt(7, 0).EndsAt(15, 0))
                .Run(_scheduler);

            // Exact expected Unscheduled timeline (teacher coverage with no supports)
            var expectedUnscheduled = new ExpectedTimeline(date)
                .ForSupport("Unscheduled")
                    .Coverage("T3", "09:00", 10)
                    .Coverage("T3", "11:00", 30)
                    .Coverage("T3", "13:30", 10)
                .End()
                .Build();

            TimelineAssertions.AssertSupportTimelineEquals(
                scenario.BySupport,
                "Unscheduled",
                expectedUnscheduled["Unscheduled"]);
        }
    }
}