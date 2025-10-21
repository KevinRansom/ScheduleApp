using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ScheduleApp.Models;
using Xunit;

namespace ScheduleTests.TestHelpers
{
    // Build an expected schedule (subset) per support, then assert against actual.
    public class ExpectedScheduleBuilder
    {
        private readonly Dictionary<string, List<CoverageTask>> _map =
            new Dictionary<string, List<CoverageTask>>(StringComparer.OrdinalIgnoreCase);

        public ExpectedScheduleBuilder Support(string supportName)
        {
            if (!_map.ContainsKey(supportName)) _map[supportName] = new List<CoverageTask>();
            return this;
        }

        public ExpectedScheduleBuilder Coverage(string supportName, string teacherName, int minutes, string room = "---")
        {
            Add(supportName, new CoverageTask
            {
                SupportName = supportName,
                TeacherName = teacherName,
                RoomNumber = room,
                Kind = CoverageTaskKind.Coverage,
                Start = DateTime.MinValue,
                End = DateTime.MinValue.AddMinutes(minutes),
                BufferAfterMinutes = minutes == 10 ? 5 : 0
            });
            return this;
        }

        public ExpectedScheduleBuilder SelfCare(string supportName, CoverageTaskKind kind, int minutes)
        {
            Add(supportName, new CoverageTask
            {
                SupportName = supportName,
                TeacherName = supportName, // convention used in app for self-care/idle
                RoomNumber = "---",
                Kind = kind,
                Start = DateTime.MinValue,
                End = DateTime.MinValue.AddMinutes(minutes),
                BufferAfterMinutes = kind == CoverageTaskKind.Break ? 5 : 0
            });
            return this;
        }

        public Dictionary<string, List<CoverageTask>> Build() =>
            _map.ToDictionary(k => k.Key, v => v.Value.ToList(), StringComparer.OrdinalIgnoreCase);

        private void Add(string key, CoverageTask task)
        {
            if (!_map.TryGetValue(key, out var list))
            {
                list = new List<CoverageTask>();
                _map[key] = list;
            }
            list.Add(task);
        }
    }

    // Builder for teacher coverage tasks (flat list), used to validate scenario.TeacherTasks
    public class ExpectedTeacherScheduleBuilder
    {
        private readonly List<CoverageTask> _list = new List<CoverageTask>();
        private readonly DateTime? _date;

        public ExpectedTeacherScheduleBuilder() { }

        public ExpectedTeacherScheduleBuilder(DateTime date)
        {
            _date = date.Date;
        }

        // Duration-only (no time check)
        public ExpectedTeacherScheduleBuilder Coverage(string teacherName, int minutes, string room = "---")
        {
            _list.Add(new CoverageTask
            {
                TeacherName = teacherName,
                RoomNumber = room,
                Kind = CoverageTaskKind.Coverage,
                Start = DateTime.MinValue,
                End = DateTime.MinValue.AddMinutes(minutes),
                BufferAfterMinutes = minutes == 10 ? 5 : 0
            });
            return this;
        }

        // Time + duration (enables time check)
        public ExpectedTeacherScheduleBuilder Coverage(string teacherName, string startHHmm, int minutes, string room = "---")
        {
            if (!_date.HasValue)
                throw new InvalidOperationException("ExpectedTeacherScheduleBuilder was constructed without a base date. Use new ExpectedTeacherScheduleBuilder(date) to specify times.");
            var start = _date.Value + ParseHHmm(startHHmm);

            _list.Add(new CoverageTask
            {
                TeacherName = teacherName,
                RoomNumber = room,
                Kind = CoverageTaskKind.Coverage,
                Start = start,
                End = start.AddMinutes(minutes),
                BufferAfterMinutes = minutes == 10 ? 5 : 0
            });
            return this;
        }

        public List<CoverageTask> Build() => _list.ToList();

        private static TimeSpan ParseHHmm(string s)
        {
            if (TimeSpan.TryParseExact(s, "hh\\:mm", CultureInfo.InvariantCulture, out var ts)) return ts;
            if (TimeSpan.TryParse(s, CultureInfo.InvariantCulture, out ts)) return ts;
            throw new FormatException("Time must be in HH:mm");
        }
    }

    public static class ExpectedScheduleAssertions
    {
        // Ensure all expected tasks exist in actual result (subset comparison).
        public static void AssertEquivalent(
            Dictionary<string, List<CoverageTask>> actual,
            Dictionary<string, List<CoverageTask>> expected)
        {
            foreach (var kvp in expected)
            {
                var support = kvp.Key;
                var expectedList = kvp.Value;

                Assert.True(actual.ContainsKey(support), $"Support '{support}' missing from actual schedule.");
                var actualList = actual[support] ?? new List<CoverageTask>();

                foreach (var e in expectedList)
                {
                    var match = actualList.Any(a =>
                        a.Kind == e.Kind &&
                        a.Minutes == e.Minutes &&
                        string.Equals(a.TeacherName ?? "", e.TeacherName ?? "", StringComparison.OrdinalIgnoreCase));
                    Assert.True(match, $"Expected task not found for support '{support}': Kind={e.Kind}, Minutes={e.Minutes}, Teacher='{e.TeacherName}'.");
                }
            }
        }

        // Subset comparison for teacher coverage tasks with optional start-time tolerance.
        // If an expected item has Start != DateTime.MinValue, its time will be validated.
        public static void AssertTeacherTasksEquivalent(
            List<CoverageTask> teacherTasks,
            string teacher,
            List<CoverageTask> expected,
            int startToleranceMinutes = 0)
        {
            var actualPool = teacherTasks
                .Where(t => string.Equals(t.TeacherName ?? "", teacher ?? "", StringComparison.OrdinalIgnoreCase))
                .Where(t => t.Kind == CoverageTaskKind.Coverage)
                .OrderBy(t => t.Start)
                .ToList();

            foreach (var e in expected)
            {
                var idx = actualPool.FindIndex(a =>
                    a.Kind == e.Kind &&
                    a.Minutes == e.Minutes &&
                    (e.Start == DateTime.MinValue ||
                     Math.Abs((a.Start - e.Start).TotalMinutes) <= startToleranceMinutes));

                Assert.True(idx >= 0, $"Expected teacher task not found for '{teacher}': Kind={e.Kind}, Minutes={e.Minutes}" +
                                      (e.Start == DateTime.MinValue ? "" : $" @ {e.Start:HH:mm} (±{startToleranceMinutes}m)"));

                // consume match to avoid double-matching when there are duplicates
                actualPool.RemoveAt(idx);
            }
        }

        // Exact set comparison for teacher tasks (coverage only): presence, times (with tolerance), and no extras.
        public static void AssertTeacherTasksExactlyEqual(
            List<CoverageTask> teacherTasks,
            string teacher,
            List<CoverageTask> expected,
            int startToleranceMinutes = 0)
        {
            var actualPool = teacherTasks
                .Where(t => string.Equals(t.TeacherName ?? "", teacher ?? "", StringComparison.OrdinalIgnoreCase))
                .Where(t => t.Kind == CoverageTaskKind.Coverage)
                .OrderBy(t => t.Start)
                .ToList();

            // match and consume actuals
            foreach (var e in expected)
            {
                var idx = actualPool.FindIndex(a =>
                    a.Kind == e.Kind &&
                    a.Minutes == e.Minutes &&
                    (e.Start == DateTime.MinValue ||
                     Math.Abs((a.Start - e.Start).TotalMinutes) <= startToleranceMinutes));

                Assert.True(idx >= 0, $"Expected teacher task not found for '{teacher}': Kind={e.Kind}, Minutes={e.Minutes}" +
                                      (e.Start == DateTime.MinValue ? "" : $" @ {e.Start:HH:mm} (±{startToleranceMinutes}m)"));
                actualPool.RemoveAt(idx);
            }

            // ensure no extras remain
            Assert.True(actualPool.Count == 0,
                $"Unexpected extra tasks for '{teacher}': {string.Join(", ", actualPool.Select(x => x.Start.ToString("HH:mm") + "/" + x.Minutes + "m"))}");
        }

        // Convenience: assert a support contains at least one task of a kind
        public static void AssertSupportContainsKind(Dictionary<string, List<CoverageTask>> actual, string support, CoverageTaskKind kind)
        {
            Assert.True(actual.ContainsKey(support), $"Support '{support}' missing from actual schedule.");
            Assert.Contains(actual[support], t => t.Kind == kind);
        }

        public static void AssertSupportDoesNotContainKind(Dictionary<string, List<CoverageTask>> actual, string support, CoverageTaskKind kind)
        {
            Assert.True(actual.ContainsKey(support), $"Support '{support}' missing from actual schedule.");
            Assert.DoesNotContain(actual[support], t => t.Kind == kind);
        }

        // Coverage-specific helpers
        public static void AssertSupportContainsCoverage(Dictionary<string, List<CoverageTask>> actual, string support, string teacher, int minutes)
        {
            Assert.True(actual.ContainsKey(support), $"Support '{support}' missing from actual schedule.");
            Assert.Contains(actual[support], t =>
                t.Kind == CoverageTaskKind.Coverage &&
                t.Minutes == minutes &&
                string.Equals(t.TeacherName, teacher, StringComparison.OrdinalIgnoreCase));
        }

        public static void AssertSupportDoesNotContainCoverage(Dictionary<string, List<CoverageTask>> actual, string support, string teacher, int minutes)
        {
            Assert.True(actual.ContainsKey(support), $"Support '{support}' missing from actual schedule.");
            Assert.DoesNotContain(actual[support], t =>
                t.Kind == CoverageTaskKind.Coverage &&
                t.Minutes == minutes &&
                string.Equals(t.TeacherName, teacher, StringComparison.OrdinalIgnoreCase));
        }

        // Existing single-item teacher helper remains available
        public static void AssertTeacherTasksContain(List<CoverageTask> tasks, string teacher, int minutes)
        {
            Assert.Contains(tasks, t =>
                t.Kind == CoverageTaskKind.Coverage &&
                t.Minutes == minutes &&
                string.Equals(t.TeacherName, teacher, StringComparison.OrdinalIgnoreCase));
        }
    }
}