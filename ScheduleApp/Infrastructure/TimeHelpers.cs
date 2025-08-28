using System;

namespace ScheduleApp.Infrastructure
{
    public static class TimeHelpers
    {
        public static DateTime RoundUpToQuarter(DateTime dt)
        {
            var minutes = dt.Minute;
            var mod = minutes % 15;
            if (mod == 0 && dt.Second == 0 && dt.Millisecond == 0) return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, minutes, 0);
            var add = 15 - mod;
            var result = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, minutes, 0).AddMinutes(add);
            return result;
        }

        public static DateTime RoundDownToQuarter(DateTime dt)
        {
            var minutes = dt.Minute;
            var mod = minutes % 15;
            var result = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, minutes, 0).AddMinutes(-mod);
            return result;
        }

        public static DateTime RoundToNearestQuarter(DateTime dt)
        {
            var down = RoundDownToQuarter(dt);
            var up = RoundUpToQuarter(dt);
            var diffDown = (dt - down).TotalMinutes;
            var diffUp = (up - dt).TotalMinutes;
            return diffDown <= diffUp ? down : up;
        }

        public static DateTime ClampToQuarterWithin(DateTime target, DateTime windowStart, DateTime windowEnd)
        {
            var proposed = RoundToNearestQuarter(target);
            if (proposed < windowStart) proposed = RoundUpToQuarter(windowStart);
            if (proposed.AddMinutes(1) > windowEnd) return DateTime.MinValue;
            return proposed;
        }
    }
}