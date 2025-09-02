using System;
using System.Collections.Generic;
using System.Linq;
using ScheduleApp.Models;
using ScheduleApp.Infrastructure;

namespace ScheduleApp.Services
{
    public class SchedulerService
    {
        private const int FlexMinutes = 30;   // allow ±30m flex for teacher coverage when needed
        private static readonly TimeSpan FlexStep = TimeSpan.FromMinutes(15);

        public List<CoverageTask> GenerateTeacherCoverageTasks(DayContext day)
        {
            var tasks = new List<CoverageTask>();
            foreach (var t in day.Teachers)
            {
                if (t.Start >= t.End) continue;

                var date = day.Date.Date;
                var shiftStart = date.Add(t.Start);
                var shiftEnd = date.Add(t.End);

                // 10-minute breaks every 3 hours
                var totalHours = (shiftEnd - shiftStart).TotalHours;
                var breakCount = (int)Math.Floor(totalHours / 3.0);
                var tick = shiftStart.AddHours(3);

                for (int i = 0; i < breakCount; i++)
                {
                    var planned = TimeHelpers.RoundUpToQuarter(tick);
                    var start = planned;
                    var end = start.AddMinutes(10);

                    if (end > shiftEnd)
                    {
                        // try shifting earlier to fit
                        var latestStart = TimeHelpers.RoundDownToQuarter(shiftEnd.AddMinutes(-10));
                        if (latestStart >= shiftStart)
                        {
                            start = latestStart;
                            end = start.AddMinutes(10);
                        }
                        else
                        {
                            break;
                        }
                    }

                    tasks.Add(new CoverageTask
                    {
                        RoomNumber = t.RoomNumber,
                        TeacherName = t.Name,
                        Kind = CoverageTaskKind.Coverage,
                        Start = start,
                        End = end,
                        BufferAfterMinutes = 5 // 5-min buffer required after 10-min break
                    });

                    tick = tick.AddHours(3);
                }

                // 30-minute lunch if shift > 5h, place near midpoint
                if (totalHours > 5.0)
                {
                    var midpoint = shiftStart + TimeSpan.FromTicks((shiftEnd - shiftStart).Ticks / 2);
                    var lunchStart = TimeHelpers.RoundToNearestQuarter(midpoint);
                    var latestLunchStart = date.AddHours(14); // NEW: cap at 2:00 PM

                    // NEW: clamp to latest lunch start (2:00 PM)
                    if (lunchStart > latestLunchStart)
                    {
                        lunchStart = TimeHelpers.RoundDownToQuarter(latestLunchStart);
                    }

                    var lunchEnd = lunchStart.AddMinutes(30);

                    if (lunchEnd > shiftEnd)
                    {
                        lunchStart = TimeHelpers.RoundDownToQuarter(shiftEnd.AddMinutes(-30));
                        lunchEnd = lunchStart.AddMinutes(30);
                    }

                    if (lunchStart < shiftStart)
                    {
                        lunchStart = TimeHelpers.RoundUpToQuarter(shiftStart);
                        lunchEnd = lunchStart.AddMinutes(30);
                    }

                    if (lunchEnd <= shiftEnd)
                    {
                        tasks.Add(new CoverageTask
                        {
                            RoomNumber = t.RoomNumber,
                            TeacherName = t.Name,
                            Kind = CoverageTaskKind.Coverage,
                            Start = lunchStart,
                            End = lunchEnd,
                            BufferAfterMinutes = 0
                        });
                    }
                }
            }

            // Deduplicate or resolve overlaps within same teacher if rounding caused conflicts
            tasks = tasks.OrderBy(x => x.TeacherName).ThenBy(x => x.Start).ToList();
            tasks = ResolveOverlapsPerTeacher(tasks);
            return tasks.OrderBy(x => x.Start).ToList();
        }

        public Dictionary<string, List<CoverageTask>> AssignSupportToTeacherTasks(DayContext day, List<CoverageTask> teacherTasks)
        {
            var bySupport = day.Supports.ToDictionary(s => s.Name, s => new List<CoverageTask>());

            // Bucket for unassigned coverage tasks
            bySupport["Unscheduled"] = new List<CoverageTask>();

            // Track reservations per support
            var supportWindows = day.Supports.ToDictionary(s => s.Name, s =>
                new List<Tuple<DateTime, DateTime>>());

            // Track last room per support to encourage consistency
            var lastRoomBySupport = day.Supports.ToDictionary(s => s.Name, s => (string)null);

            // Build preference map: room -> list of preferred support names (case-insensitive)
            var prefMap = day.Preferences
                .Where(p => !string.IsNullOrWhiteSpace(p.RoomNumber) &&
                            !string.IsNullOrWhiteSpace(p.PreferredSupportName))
                .GroupBy(p => p.RoomNumber, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.PreferredSupportName)
                          .Where(n => !string.IsNullOrWhiteSpace(n))
                          .Distinct(StringComparer.OrdinalIgnoreCase)
                          .ToList(),
                    StringComparer.OrdinalIgnoreCase);

            foreach (var task in teacherTasks.OrderBy(t => t.Start))
            {
                var candidates = new List<Tuple<Support, int>>(); // support, score

                foreach (var s in day.Supports)
                {
                    var sStart = day.Date.Date.Add(s.Start);
                    var sEnd = day.Date.Date.Add(s.End);
                    if (task.Start < sStart || task.EffectiveEnd > sEnd) continue;

                    if (IsFree(supportWindows[s.Name], task.Start, task.EffectiveEnd))
                    {
                        int score = 0;

                        // Prefer any listed preferred support for the room
                        if (prefMap.TryGetValue(task.RoomNumber, out var preferredList) &&
                            preferredList.Any(n => string.Equals(n, s.Name, StringComparison.OrdinalIgnoreCase)))
                        {
                            score -= 3;
                        }

                        // Prefer support who last covered this room
                        if (!string.IsNullOrEmpty(lastRoomBySupport[s.Name]) &&
                            string.Equals(lastRoomBySupport[s.Name], task.RoomNumber, StringComparison.OrdinalIgnoreCase))
                        {
                            score -= 2;
                        }

                        // Prefer the one that stays most utilized (minimize idle fragmentation)
                        var lastEnd = GetLastEffectiveEnd(supportWindows[s.Name]);
                        var idle = (task.Start - lastEnd).TotalMinutes;
                        if (idle > 45) score += 1; // slight penalty for long idle before this task

                        candidates.Add(Tuple.Create(s, score));
                    }
                }

                candidates = candidates.OrderBy(t => t.Item2).ThenBy(t => t.Item1.Name).ToList();

                if (candidates.Count > 0)
                {
                    var chosen = candidates[0].Item1;
                    AddAssigned(bySupport, supportWindows, lastRoomBySupport, chosen, task, task.Start, task.End);
                }
                else
                {
                    // Try to flex the task within ±30 minutes to find the nearest available slot
                    var flex = TryAssignWithFlex(day, task, bySupport, supportWindows, prefMap, lastRoomBySupport);
                    if (!flex)
                    {
                        // Track unassigned coverage
                        bySupport["Unscheduled"].Add(new CoverageTask
                        {
                            RoomNumber = task.RoomNumber,
                            TeacherName = task.TeacherName,
                            SupportName = "Unscheduled",
                            Kind = CoverageTaskKind.Coverage,
                            Start = task.Start,
                            End = task.End,
                            BufferAfterMinutes = task.BufferAfterMinutes
                        });
                    }
                }
            }

            // Sort tasks per support (including Unscheduled)
            foreach (var key in bySupport.Keys.ToList())
            {
                bySupport[key] = bySupport[key].OrderBy(t => t.Start).ToList();
            }

            return bySupport;
        }

        private bool TryAssignWithFlex(
            DayContext day,
            CoverageTask task,
            Dictionary<string, List<CoverageTask>> bySupport,
            Dictionary<string, List<Tuple<DateTime, DateTime>>> supportWindows,
            Dictionary<string, List<string>> prefMap,
            Dictionary<string, string> lastRoomBySupport)
        {
            var duration = task.End - task.Start;
            var isLunch = duration.TotalMinutes >= 25; // lunch is 30m, breaks are 10m
            var lunchEarliest = day.Date.Date.AddHours(11); // 11:00 AM lower bound for lunch
            var lunchLatest   = day.Date.Date.AddHours(14); // NEW: 2:00 PM latest lunch start

            // NEW: teacher shift bounds to keep breaks/lunch within the teacher’s day
            var teacher = day.Teachers.FirstOrDefault(t =>
                string.Equals(t.Name, task.TeacherName, StringComparison.OrdinalIgnoreCase));
            var teacherShiftStart = teacher != null ? day.Date.Date.Add(teacher.Start) : task.Start;
            var teacherShiftEnd   = teacher != null ? day.Date.Date.Add(teacher.End)   : task.End;

            var bestSupport = (Support)null;
            DateTime bestStart = DateTime.MinValue;
            int bestScore = int.MaxValue;
            long bestDelta = long.MaxValue;

            foreach (var s in day.Supports)
            {
                var sStart = day.Date.Date.Add(s.Start);
                var sEnd = day.Date.Date.Add(s.End);

                // Search offsets: 0, +15, -15, +30, -30
                for (int minutes = 0; minutes <= FlexMinutes; minutes += (int)FlexStep.TotalMinutes)
                {
                    foreach (var sign in minutes == 0 ? new[] { 1 } : new[] { +1, -1 })
                    {
                        var offset = TimeSpan.FromMinutes(minutes * sign);
                        var tryStart = task.Start + offset;

                        // Keep within teacher’s shift
                        var tryEnd = tryStart + duration;
                        if (tryStart < teacherShiftStart || tryEnd > teacherShiftEnd)
                            continue;

                        // Do not move lunch outside 11:00–14:00
                        if (isLunch && (tryStart < lunchEarliest || tryStart > lunchLatest)) continue;

                        var effEnd = tryEnd.AddMinutes(task.BufferAfterMinutes);

                        // Must also fit within support’s shift and be free
                        if (tryStart < sStart || effEnd > sEnd) continue;
                        if (!IsFree(supportWindows[s.Name], tryStart, effEnd)) continue;

                        int score = 0;
                        if (prefMap.TryGetValue(task.RoomNumber, out var preferredList) &&
                            preferredList.Any(n => string.Equals(n, s.Name, StringComparison.OrdinalIgnoreCase)))
                        {
                            score -= 3;
                        }
                        if (!string.IsNullOrEmpty(lastRoomBySupport[s.Name]) &&
                            string.Equals(lastRoomBySupport[s.Name], task.RoomNumber, StringComparison.OrdinalIgnoreCase))
                        {
                            score -= 2;
                        }

                        var delta = Math.Abs((tryStart - task.Start).Ticks);
                        var isBetter = score < bestScore ||
                                       (score == bestScore && delta < bestDelta) ||
                                       (score == bestScore && delta == bestDelta && string.CompareOrdinal(s.Name, bestSupport?.Name) < 0);

                        if (isBetter)
                        {
                            bestSupport = s;
                            bestStart = tryStart;
                            bestScore = score;
                            bestDelta = delta;
                        }
                    }
                }
            }

            if (bestSupport != null)
            {
                var newEnd = bestStart + (task.End - task.Start);
                AddAssigned(bySupport, supportWindows, lastRoomBySupport, bestSupport, task, bestStart, newEnd);
                return true;
            }

            return false;
        }

        private void AddAssigned(
            Dictionary<string, List<CoverageTask>> bySupport,
            Dictionary<string, List<Tuple<DateTime, DateTime>>> supportWindows,
            Dictionary<string, string> lastRoomBySupport,
            Support chosen,
            CoverageTask task,
            DateTime start,
            DateTime end)
        {
            var assigned = new CoverageTask
            {
                RoomNumber = task.RoomNumber,
                TeacherName = task.TeacherName,
                SupportName = chosen.Name,
                Kind = CoverageTaskKind.Coverage,
                Start = start,
                End = end,
                BufferAfterMinutes = task.BufferAfterMinutes
            };

            if (!bySupport.TryGetValue(chosen.Name, out var list))
            {
                list = new List<CoverageTask>();
                bySupport[chosen.Name] = list;
            }
            list.Add(assigned);
            bySupport[chosen.Name] = bySupport[chosen.Name].OrderBy(t => t.Start).ToList();

            // Reserve the time including buffer
            var effWindow = Tuple.Create(assigned.Start, assigned.EffectiveEnd);
            supportWindows[chosen.Name].Add(effWindow);
            supportWindows[chosen.Name] = supportWindows[chosen.Name].OrderBy(w => w.Item1).ToList();

            lastRoomBySupport[chosen.Name] = task.RoomNumber;
        }

        public void ScheduleSupportSelfCare(DayContext day, Dictionary<string, List<CoverageTask>> bySupport)
        {
            foreach (var s in day.Supports)
            {
                var name = s.Name;
                var sShiftStart = day.Date.Date.Add(s.Start);
                var sShiftEnd = day.Date.Date.Add(s.End);
                var list = bySupport[name];

                // Compute windows between existing EffectiveEnd ranges
                var reservations = list.Select(t => Tuple.Create(t.Start, t.EffectiveEnd)).OrderBy(w => w.Item1).ToList();
                var freeWindows = BuildFreeWindows(sShiftStart, sShiftEnd, reservations);

                // Required self-care
                var breaksNeeded = (int)Math.Floor(Math.Max(0, (sShiftEnd - sShiftStart).TotalHours) / 3.0);
                var needsLunch = (sShiftEnd - sShiftStart).TotalHours > 5.0;

                // Place lunch first near midpoint (TeacherName = support name)
                if (needsLunch)
                {
                    var midpoint = sShiftStart + TimeSpan.FromTicks((sShiftEnd - sShiftStart).Ticks / 2);
                    var placed = TryPlaceSelfCare(list, ref freeWindows, name, CoverageTaskKind.Lunch, name, "---", midpoint, 30, 0);
                    if (!placed)
                    {
                        // Add unscheduled lunch between 11:00 and 14:00, clamped to shift
                        var earliest = sShiftStart.Date.AddHours(11);
                        var latest = sShiftStart.Date.AddHours(14);
                        var start = TimeHelpers.ClampToQuarterWithin(midpoint, earliest, latest);
                        if (start == DateTime.MinValue) start = TimeHelpers.RoundUpToQuarter(earliest);

                        // keep inside shift
                        if (start < sShiftStart) start = TimeHelpers.RoundUpToQuarter(sShiftStart);
                        var end = start.AddMinutes(30);
                        if (end > sShiftEnd)
                        {
                            start = TimeHelpers.RoundDownToQuarter(sShiftEnd.AddMinutes(-30));
                            end = start.AddMinutes(30);
                        }

                        AddUnscheduledSelfCare(bySupport, name, CoverageTaskKind.Lunch, start, end, 0);
                    }
                }

                // Place breaks across the shift (TeacherName = support name)
                for (int i = 0; i < breaksNeeded; i++)
                {
                    var target = sShiftStart.AddHours((i + 1) * 3.0); // after each 3h block
                    var placed = TryPlaceSelfCare(list, ref freeWindows, name, CoverageTaskKind.Break, name, "---", target, 10, 5);
                    if (!placed)
                    {
                        // Add unscheduled break near target, clamped to shift
                        var start = TimeHelpers.RoundToNearestQuarter(target);
                        if (start < sShiftStart) start = TimeHelpers.RoundUpToQuarter(sShiftStart);
                        var end = start.AddMinutes(10);
                        if (end > sShiftEnd)
                        {
                            start = TimeHelpers.RoundDownToQuarter(sShiftEnd.AddMinutes(-10));
                            end = start.AddMinutes(10);
                        }

                        if (end <= sShiftEnd)
                            AddUnscheduledSelfCare(bySupport, name, CoverageTaskKind.Break, start, end, 5);
                    }
                }

                // Fill idle segments as working time
                reservations = list.Select(t => Tuple.Create(t.Start, t.EffectiveEnd)).OrderBy(w => w.Item1).ToList();
                var idleWindows = BuildFreeWindows(sShiftStart, sShiftEnd, reservations);
                foreach (var w in idleWindows)
                {
                    if (w.Item2 <= w.Item1) continue;
                    list.Add(new CoverageTask
                    {
                        SupportName = name,
                        Kind = CoverageTaskKind.Idle,
                        TeacherName = name, // show owning support in Teacher column
                        RoomNumber = "",
                        Start = w.Item1,
                        End = w.Item2,
                        BufferAfterMinutes = 0
                    });
                }

                // Ensure SupportName is populated for all tasks
                list = list.OrderBy(t => t.Start).ToList();
                foreach (var t in list)
                {
                    if (string.IsNullOrWhiteSpace(t.SupportName))
                        t.SupportName = name;
                }

                bySupport[name] = list;
            }

            // Keep Unscheduled list sorted
            if (bySupport.TryGetValue("Unscheduled", out var unsched))
                bySupport["Unscheduled"] = unsched.OrderBy(t => t.Start).ToList();
        }

        // Helper: add a self-care item to the Unscheduled tab (TeacherName = owner)
        private static void AddUnscheduledSelfCare(
            Dictionary<string, List<CoverageTask>> bySupport,
            string ownerSupportName,
            CoverageTaskKind kind,
            DateTime start,
            DateTime end,
            int bufferAfter)
        {
            if (!bySupport.TryGetValue("Unscheduled", out var list))
            {
                list = new List<CoverageTask>();
                bySupport["Unscheduled"] = list;
            }

            list.Add(new CoverageTask
            {
                SupportName = "Unscheduled",
                Kind = kind,
                TeacherName = ownerSupportName,
                RoomNumber = "---",
                Start = start,
                End = end,
                BufferAfterMinutes = bufferAfter
            });
        }

        // Helpers

        private static bool IsFree(List<Tuple<DateTime, DateTime>> windows, DateTime start, DateTime end)
        {
            for (int i = 0; i < windows.Count; i++)
            {
                var w = windows[i];
                if (start < w.Item2 && end > w.Item1)
                {
                    return false; // overlap
                }
            }
            return true;
        }

        private static DateTime GetLastEffectiveEnd(List<Tuple<DateTime, DateTime>> windows)
        {
            if (windows.Count == 0) return DateTime.MinValue;
            return windows.Max(w => w.Item2);
        }

        private static List<Tuple<DateTime, DateTime>> BuildFreeWindows(DateTime start, DateTime end, List<Tuple<DateTime, DateTime>> reservations)
        {
            var list = new List<Tuple<DateTime, DateTime>>();
            var cursor = start;

            foreach (var r in reservations.OrderBy(r => r.Item1))
            {
                if (r.Item1 > cursor)
                {
                    list.Add(Tuple.Create(cursor, r.Item1));
                }
                if (r.Item2 > cursor) cursor = r.Item2;
                if (cursor >= end) break;
            }

            if (cursor < end) list.Add(Tuple.Create(cursor, end));
            return list;
        }

        private static bool TryPlaceSelfCare(List<CoverageTask> list,
            ref List<Tuple<DateTime, DateTime>> freeWindows,
            string supportName,
            CoverageTaskKind kind, string teacherName, string room,
            DateTime target, int minutes, int bufferAfter)
        {
            var isLunch = kind == CoverageTaskKind.Lunch;

            var sorted = freeWindows.ToList();
            sorted.Sort((a, b) =>
            {
                var da = Math.Abs((a.Item1 - target).Ticks);
                var db = Math.Abs((b.Item1 - target).Ticks);
                return da.CompareTo(db);
            });

            foreach (var w in sorted)
            {
                var windowStart = w.Item1;
                var windowEnd = w.Item2;

                if (isLunch)
                {
                    // Enforce 11:00 ≤ lunch start ≤ 14:00
                    var earliestLunch = w.Item1.Date.AddHours(11);
                    var latestLunch   = w.Item1.Date.AddHours(14);

                    if (windowStart < earliestLunch) windowStart = earliestLunch;

                    // latest allowed start considering duration and window end
                    var latestAllowedStart = latestLunch;
                    var maxByWindow = windowEnd.AddMinutes(-minutes);
                    if (latestAllowedStart > maxByWindow) latestAllowedStart = maxByWindow;

                    // No valid time range within [11:00, 14:00] and this window
                    if (latestAllowedStart <= windowStart) continue;

                    var start = TimeHelpers.ClampToQuarterWithin(target, windowStart, latestAllowedStart);
                    if (start == DateTime.MinValue)
                        start = TimeHelpers.RoundUpToQuarter(windowStart);

                    var end = start.AddMinutes(minutes);
                    if (!(end <= windowEnd && start <= latestLunch)) continue;

                    var task = new CoverageTask
                    {
                        SupportName = supportName,
                        Kind = kind,
                        TeacherName = teacherName,
                        RoomNumber = room,
                        Start = start,
                        End = end,
                        BufferAfterMinutes = bufferAfter
                    };

                    list.Add(task);
                    list.Sort((x, y) => x.Start.CompareTo(y.Start));

                    var effectiveEnd = task.EffectiveEnd;
                    var newWindows = new List<Tuple<DateTime, DateTime>>();
                    foreach (var fw in freeWindows)
                    {
                        if (effectiveEnd <= fw.Item1 || task.Start >= fw.Item2)
                        {
                            newWindows.Add(fw);
                        }
                        else
                        {
                            if (task.Start > fw.Item1)
                                newWindows.Add(Tuple.Create(fw.Item1, task.Start));
                            if (effectiveEnd < fw.Item2)
                                newWindows.Add(Tuple.Create(effectiveEnd, fw.Item2));
                        }
                    }
                    freeWindows = newWindows.OrderBy(w2 => w2.Item1).ToList();
                    return true;
                }
                else
                {
                    // Break/Idle unchanged
                    var start = TimeHelpers.ClampToQuarterWithin(target, windowStart, windowEnd);
                    if (start == DateTime.MinValue)
                        start = TimeHelpers.RoundUpToQuarter(windowStart);

                    var end = start.AddMinutes(minutes);
                    if (end <= windowEnd)
                    {
                        var task = new CoverageTask
                        {
                            SupportName = supportName,
                            Kind = kind,
                            TeacherName = teacherName,
                            RoomNumber = room,
                            Start = start,
                            End = end,
                            BufferAfterMinutes = bufferAfter
                        };

                        list.Add(task);
                        list.Sort((x, y) => x.Start.CompareTo(y.Start));

                        var effectiveEnd = task.EffectiveEnd;
                        var newWindows = new List<Tuple<DateTime, DateTime>>();
                        foreach (var fw in freeWindows)
                        {
                            if (effectiveEnd <= fw.Item1 || task.Start >= fw.Item2)
                            {
                                newWindows.Add(fw);
                            }
                            else
                            {
                                if (task.Start > fw.Item1)
                                    newWindows.Add(Tuple.Create(fw.Item1, task.Start));
                                if (effectiveEnd < fw.Item2)
                                    newWindows.Add(Tuple.Create(effectiveEnd, fw.Item2));
                            }
                        }
                        freeWindows = newWindows.OrderBy(w2 => w2.Item1).ToList();
                        return true;
                    }
                }
            }

            return false;
        }

        private static List<CoverageTask> ResolveOverlapsPerTeacher(List<CoverageTask> tasks)
        {
            var result = new List<CoverageTask>();
            string currentTeacher = null;
            DateTime lastEnd = DateTime.MinValue;

            foreach (var t in tasks.OrderBy(x => x.TeacherName).ThenBy(x => x.Start))
            {
                if (currentTeacher != t.TeacherName)
                {
                    currentTeacher = t.TeacherName;
                    lastEnd = DateTime.MinValue;
                }

                if (t.Start < lastEnd)
                {
                    // Shift current task to start at lastEnd if possible, still aligned
                    var duration = t.End - t.Start;
                    var newStart = TimeHelpers.RoundUpToQuarter(lastEnd);
                    var newEnd = newStart + duration;
                    if (newEnd <= t.End.Date.AddDays(1)) // basic guard
                    {
                        t.Start = newStart;
                        t.End = newEnd;
                    }
                }

                result.Add(t);
                lastEnd = t.End;
            }

            return result;
        }
    }
}