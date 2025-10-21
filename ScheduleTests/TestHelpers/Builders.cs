using System;
using System.Collections.Generic;
using ScheduleApp.Models;

namespace ScheduleTests.TestHelpers
{
    // Fluent data builders used by tests.

    public sealed class TeacherBuilder
    {
        private readonly Teacher _t = new Teacher
        {
            Name = "Teacher",
            RoomNumber = "R1",
            Start = TimeSpan.FromHours(8),
            End = TimeSpan.FromHours(15)
        };

        public static TeacherBuilder Create() => new TeacherBuilder();

        public TeacherBuilder Named(string name) { _t.Name = name; return this; }
        public TeacherBuilder InRoom(string room) { _t.RoomNumber = room; return this; }
        public TeacherBuilder StartsAt(int hour, int minute = 0) { _t.Start = new TimeSpan(hour, minute, 0); return this; }
        public TeacherBuilder EndsAt(int hour, int minute = 0) { _t.End = new TimeSpan(hour, minute, 0); return this; }

        public Teacher Build() => _t;
    }

    public sealed class SupportBuilder
    {
        private readonly Support _s = new Support
        {
            Name = "Support",
            Start = TimeSpan.FromHours(9),
            End = TimeSpan.FromHours(15)
        };

        public static SupportBuilder Create() => new SupportBuilder();

        public SupportBuilder Named(string name) { _s.Name = name; return this; }
        public SupportBuilder StartsAt(int hour, int minute = 0) { _s.Start = new TimeSpan(hour, minute, 0); return this; }
        public SupportBuilder EndsAt(int hour, int minute = 0) { _s.End = new TimeSpan(hour, minute, 0); return this; }

        public Support Build() => _s;
    }
}