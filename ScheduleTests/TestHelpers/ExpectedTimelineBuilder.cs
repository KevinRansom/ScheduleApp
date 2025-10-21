using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ScheduleApp.Models;
using Xunit;

namespace ScheduleTests.TestHelpers
{
    public class ExpectedTimeline
    {
        private readonly DateTime _date;
        private readonly Dictionary<string, List<Entry>> _bySupport =
            new Dictionary<string, List<Entry>>(StringComparer.OrdinalIgnoreCase);

        public ExpectedTimeline(DateTime date) => _date = date.Date;

        public SupportTimeline ForSupport(string supportName)
        {
            if (!_bySupport.ContainsKey(supportName))
                _bySupport[supportName] = new List<Entry>();
            return new SupportTimeline(this, supportName, _date, _bySupport[supportName]);
        }

        // NEW: convenience for the Unscheduled bucket
        public SupportTimeline ForUnscheduled() => ForSupport("Unscheduled");

        public IReadOnlyDictionary<string, IReadOnlyList<Entry>> Build()
        {
            return _bySupport.ToDictionary(k => k.Key, v => (IReadOnlyList<Entry>)v.Value.OrderBy(e => e.Start).ToList(),
                StringComparer.OrdinalIgnoreCase);
        }

        public sealed class SupportTimeline
        {
            private readonly ExpectedTimeline _root;
            private readonly string _supportName;
            private readonly DateTime _date;
            private readonly List<Entry> _list;

            internal SupportTimeline(ExpectedTimeline root, string supportName, DateTime date, List<Entry> list)
            {
                _root = root;
                _supportName = supportName;
                _date = date;
                _list = list;
            }

            public SupportTimeline Coverage(string teacher, string startHHmm, int minutes, string room = "---")
            {
                _list.Add(new Entry
                {
                    Support = _supportName,
                    Teacher = teacher,
                    Kind = CoverageTaskKind.Coverage,
                    Start = _date + ParseHHmm(startHHmm),
                    Minutes = minutes,
                    Room = room
                });
                return this;
            }

            public SupportTimeline Lunch(string startHHmm, int minutes = 30) =>
                SelfCare(CoverageTaskKind.Lunch, startHHmm, minutes);

            public SupportTimeline Break(string startHHmm, int minutes = 10) =>
                SelfCare(CoverageTaskKind.Break, startHHmm, minutes);

            public SupportTimeline Free(string startHHmm, int minutes)
            {
                _list.Add(new Entry
                {
                    Support = _supportName,
                    Teacher = _supportName, // app uses support name for self/idle
                    Kind = CoverageTaskKind.Idle,
                    Start = _date + ParseHHmm(startHHmm),
                    Minutes = minutes,
                    Room = "---"
                });
                return this;
            }

            public ExpectedTimeline End() => _root;

            private static TimeSpan ParseHHmm(string s)
            {
                if (TimeSpan.TryParseExact(s, "hh\\:mm", CultureInfo.InvariantCulture, out var ts)) return ts;
                if (TimeSpan.TryParse(s, CultureInfo.InvariantCulture, out ts)) return ts;
                throw new FormatException("Time must be in HH:mm");
            }

            private SupportTimeline SelfCare(CoverageTaskKind kind, string startHHmm, int minutes)
            {
                _list.Add(new Entry
                {
                    Support = _supportName,
                    Teacher = _supportName,
                    Kind = kind,
                    Start = _date + ParseHHmm(startHHmm),
                    Minutes = minutes,
                    Room = "---"
                });
                return this;
            }
        }

        public sealed class Entry
        {
            public string Support { get; set; }
            public string Teacher { get; set; }
            public CoverageTaskKind Kind { get; set; }
            public DateTime Start { get; set; }
            public int Minutes { get; set; }
            public string Room { get; set; }
        }
    }

    public static class TimelineAssertions
    {
        // Strict, ordered comparison for one support's entire timeline.
        // Optional tolerance (minutes) for start time comparisons (default 0).
        public static void AssertSupportTimelineEquals(
            Dictionary<string, List<CoverageTask>> actual,
            string support,
            IReadOnlyList<ExpectedTimeline.Entry> expected,
            int startToleranceMinutes = 0)
        {
            Assert.True(actual.ContainsKey(support), $"Support '{support}' missing in actual schedule.");
            var a = actual[support].OrderBy(t => t.Start).ToList();
            var e = expected.OrderBy(t => t.Start).ToList();

            Assert.Equal(e.Count, a.Count);

            for (int i = 0; i < e.Count; i++)
            {
                var ei = e[i];
                var ai = a[i];

                // Kind
                Assert.Equal(ei.Kind, ai.Kind);

                // Minutes (End - Start)
                Assert.Equal(ei.Minutes, ai.Minutes);

                // Teacher (self-care uses support name)
                Assert.Equal(Norm(ei.Teacher), Norm(ai.TeacherName));

                // Start (with optional tolerance)
                var diff = Math.Abs((ai.Start - ei.Start).TotalMinutes);
                Assert.True(diff <= startToleranceMinutes,
                    $"Start mismatch at index {i} for support '{support}'. Expected {ei.Start:HH:mm}, Actual {ai.Start:HH:mm}, Diff {diff} min (tolerance {startToleranceMinutes}).");
            }
        }

        // Subset comparison: expected entries must appear in order within the actual list.
        public static void AssertSupportTimelineContainsInOrder(
            Dictionary<string, List<CoverageTask>> actual,
            string support,
            IReadOnlyList<ExpectedTimeline.Entry> expectedSubset,
            int startToleranceMinutes = 0)
        {
            Assert.True(actual.ContainsKey(support), $"Support '{support}' missing in actual schedule.");
            var a = actual[support].OrderBy(t => t.Start).ToList();

            int cursor = 0;
            for (int i = 0; i < expectedSubset.Count; i++)
            {
                var e = expectedSubset[i];
                var found = false;
                for (; cursor < a.Count; cursor++)
                {
                    var ai = a[cursor];
                    if (ai.Kind != e.Kind) continue;
                    if (!string.Equals(Norm(ai.TeacherName), Norm(e.Teacher), StringComparison.OrdinalIgnoreCase)) continue;
                    if (ai.Minutes != e.Minutes) continue;

                    var diff = Math.Abs((ai.Start - e.Start).TotalMinutes);
                    if (diff > startToleranceMinutes) continue;

                    found = true;
                    cursor++; // move after the matched element
                    break;
                }
                Assert.True(found, $"Expected entry not found in order for '{support}': {e.Kind} {e.Teacher} @ {e.Start:HH:mm} ({e.Minutes}m).");
            }
        }

        // NEW: convenience wrappers for the Unscheduled bucket
        public static void AssertUnscheduledTimelineEquals(
            Dictionary<string, List<CoverageTask>> actual,
            IReadOnlyList<ExpectedTimeline.Entry> expected,
            int startToleranceMinutes = 0) =>
            AssertSupportTimelineEquals(actual, "Unscheduled", expected, startToleranceMinutes);

        public static void AssertUnscheduledTimelineContainsInOrder(
            Dictionary<string, List<CoverageTask>> actual,
            IReadOnlyList<ExpectedTimeline.Entry> expectedSubset,
            int startToleranceMinutes = 0) =>
            AssertSupportTimelineContainsInOrder(actual, "Unscheduled", expectedSubset, startToleranceMinutes);

        private static string Norm(string s) => string.IsNullOrWhiteSpace(s) ? "" : s.Trim();
    }
}