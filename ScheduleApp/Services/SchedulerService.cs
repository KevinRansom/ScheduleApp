using System;
using System.Collections.Generic;
using System.Linq;
using ScheduleApp.Models;
using ScheduleApp.Infrastructure;

namespace ScheduleApp.Services
{
    public class SchedulerService
    {
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

            // Track reservations per support
            var supportWindows = day.Supports.ToDictionary(s => s.Name, s =>
                new List<Tuple<DateTime, DateTime>>());

            // Track last room per support to encourage consistency
            var lastRoomBySupport = day.Supports.ToDictionary(s => s.Name, s => (string)null);

            // Build preference map: room -> support name
            var prefMap = day.Preferences.Where(p => !string.IsNullOrWhiteSpace(p.RoomNumber) && !string.IsNullOrWhiteSpace(p.PreferredSupportName))
                                         .ToDictionary(p => p.RoomNumber, p => p.PreferredSupportName);

            foreach (var task in teacherTasks.OrderBy(t => t.Start))
            {
                // Find candidates available for the entire block (including buffer)
                var candidates = new List<Tuple<Support, int>>(); // support, score

                foreach (var s in day.Supports)
                {
                    var sStart = day.Date.Date.Add(s.Start);
                    var sEnd = day.Date.Date.Add(s.End);
                    if (task.Start < sStart || task.EffectiveEnd > sEnd) continue;

                    if (IsFree(supportWindows[s.Name], task.Start, task.EffectiveEnd))
                    {
                        int score = 0;

                        // Prefer preferred support per room
                        if (prefMap.ContainsKey(task.RoomNumber) && string.Equals(prefMap[task.RoomNumber], s.Name, StringComparison.OrdinalIgnoreCase))
                            score -= 3;

                        // Prefer support who last covered this room
                        if (!string.IsNullOrEmpty(lastRoomBySupport[s.Name]) &&
                            string.Equals(lastRoomBySupport[s.Name], task.RoomNumber, StringComparison.OrdinalIgnoreCase))
                            score -= 2;

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
                    var assigned = new CoverageTask
                    {
                        RoomNumber = task.RoomNumber,
                        TeacherName = task.TeacherName,
                        SupportName = chosen.Name,
                        Kind = CoverageTaskKind.Coverage,
                        Start = task.Start,
                        End = task.End,
                        BufferAfterMinutes = task.BufferAfterMinutes
                    };
                    bySupport[chosen.Name].Add(assigned);
                    supportWindows[chosen.Name].Add(Tuple.Create(assigned.Start, assigned.EffectiveEnd));
                    supportWindows[chosen.Name] = supportWindows[chosen.Name].OrderBy(w => w.Item1).ToList();
                    lastRoomBySupport[chosen.Name] = task.RoomNumber;
                }
                // If no candidate, task remains unassigned (could be reported/colored differently if desired)
            }

            // Sort tasks per support
            foreach (var key in bySupport.Keys.ToList())
            {
                bySupport[key] = bySupport[key].OrderBy(t => t.Start).ToList();
            }

            return bySupport;
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

                // Place lunch first near midpoint
                if (needsLunch)
                {
                    var midpoint = sShiftStart + TimeSpan.FromTicks((sShiftEnd - sShiftStart).Ticks / 2);
                    TryPlaceSelfCare(list, ref freeWindows, CoverageTaskKind.Lunch, "Self", "---", midpoint, 30, 0);
                }

                // Place breaks across the shift
                for (int i = 0; i < breaksNeeded; i++)
                {
                    var target = sShiftStart.AddHours((i + 1) * 3.0); // after each 3h block
                    TryPlaceSelfCare(list, ref freeWindows, CoverageTaskKind.Break, "Self", "---", target, 10, 5);
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
                        TeacherName = "",
                        RoomNumber = "",
                        Start = w.Item1,
                        End = w.Item2,
                        BufferAfterMinutes = 0
                    });
                }

                bySupport[name] = list.OrderBy(t => t.Start).ToList();
            }
        }

        // Helpers

        private static List<CoverageTask> ResolveOverlapsPerTeacher(List<CoverageTask> tasks)
        {
            var result = new List<CoverageTask>();
            string currentTeacher = null;
            DateTime lastEnd = DateTime.MinValue;

            foreach (var t in tasks)
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
            CoverageTaskKind kind, string teacherName, string room,
            DateTime target, int minutes, int bufferAfter)
        {
            // choose window closest to target that fits aligned start
            var sorted = freeWindows.ToList();
            sorted.Sort((a, b) =>
            {
                var da = Math.Abs((a.Item1 - target).Ticks);
                var db = Math.Abs((b.Item1 - target).Ticks);
                return da.CompareTo(db);
            });

            foreach (var w in sorted)
            {
                // Align within window
                var start = TimeHelpers.ClampToQuarterWithin(target, w.Item1, w.Item2);
                if (start == DateTime.MinValue)
                {
                    // Try at window start
                    start = TimeHelpers.RoundUpToQuarter(w.Item1);
                }

                var end = start.AddMinutes(minutes);
                if (end <= w.Item2)
                {
                    var task = new CoverageTask
                    {
                        SupportName = list.Count > 0 ? list[0].SupportName : "", // will be filled by caller per support loop
                        Kind = kind,
                        TeacherName = teacherName,
                        RoomNumber = room,
                        Start = start,
                        End = end,
                        BufferAfterMinutes = bufferAfter
                    };

                    // Insert with effective reservation
                    list.Add(task);
                    list.Sort((x, y) => x.Start.CompareTo(y.Start));

                    // Update windows: remove used portion including buffer for conflict purposes
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

            return false;
        }
    }
}