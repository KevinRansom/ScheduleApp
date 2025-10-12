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
                var total = shiftEnd - shiftStart;
                var totalHours = total.TotalHours;

                // 1) Lunch (30m) if shift > 5h, near midpoint, clamped to 11:00–14:00 and inside shift
                DateTime? lunchStartOpt = null, lunchEndOpt = null;
                if (totalHours > 5.0 || t.LunchRequired)
                {
                    var midpoint = shiftStart + TimeSpan.FromTicks((shiftEnd - shiftStart).Ticks / 2);
                    var lunchStart = TimeHelpers.RoundToNearestQuarter(midpoint);
                    var latestLunchStart = date.AddHours(14);

                    if (lunchStart > latestLunchStart)
                        lunchStart = TimeHelpers.RoundDownToQuarter(latestLunchStart);

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
                        lunchStartOpt = lunchStart;
                        lunchEndOpt = lunchEnd;

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

                // 2) Build contiguous working segments (lunch resets the “rest” timer)
                var segments = new List<Tuple<DateTime, DateTime>>();
                if (lunchStartOpt.HasValue)
                {
                    if (lunchStartOpt.Value > shiftStart)
                        segments.Add(Tuple.Create(shiftStart, lunchStartOpt.Value));
                    if (lunchEndOpt.Value < shiftEnd)
                        segments.Add(Tuple.Create(lunchEndOpt.Value, shiftEnd));
                }
                else
                {
                    segments.Add(Tuple.Create(shiftStart, shiftEnd));
                }

                // 3) Place breaks
                //    - Enforce: no one works ≥ 3h continuously.
                //    - Prefer: place each break near the middle of the 4h block (~2h since last rest).
                //    - Ensure: at least floor(totalWork/4) breaks overall.
                var totalWork = segments.Aggregate(TimeSpan.Zero, (acc, s) => acc + (s.Item2 - s.Item1));
                var minBreaks = (int)Math.Floor(totalWork.TotalHours / 4.0);

                var threeHours = TimeSpan.FromHours(3);
                var midOfFour = TimeSpan.FromHours(2);
                var breakDur = TimeSpan.FromMinutes(10);

                int placedBreaks = 0;

                // Helper to add one break at target within [windowStart, windowEnd]
                Action<DateTime, DateTime, DateTime> addBreakNear = (lastRest, segEnd, target) =>
                {
                    var latest = lastRest + threeHours;                            // cannot be later than 3h mark
                    var windowEnd = segEnd.AddMinutes(-10);                        // must fit 10m
                    var upperBound = latest < windowEnd ? latest : windowEnd;

                    var start = TimeHelpers.ClampToQuarterWithin(target, lastRest, upperBound);
                    if (start == DateTime.MinValue)
                        start = TimeHelpers.RoundDownToQuarter(upperBound);

                    if (start < lastRest) start = TimeHelpers.RoundUpToQuarter(lastRest);
                    var end = start + breakDur;
                    if (end > segEnd || end <= lastRest) return;

                    tasks.Add(new CoverageTask
                    {
                        RoomNumber = t.RoomNumber,
                        TeacherName = t.Name,
                        Kind = CoverageTaskKind.Coverage,
                        Start = start,
                        End = end,
                        BufferAfterMinutes = 5
                    });

                    placedBreaks++;
                };

                // First pass: enforce the 3h rule in each segment
                for (int si = 0; si < segments.Count; si++)
                {
                    var segStart = segments[si].Item1;
                    var segEnd = segments[si].Item2;

                    var lastRest = segStart; // reset at start of segment or post-lunch
                    while (segEnd - lastRest > threeHours)
                    {
                        var target = lastRest + midOfFour; // aim at ~2h after last rest
                        var preCount = placedBreaks;
                        addBreakNear(lastRest, segEnd, target);

                        // move lastRest if we actually added a break; otherwise bail to avoid infinite loop
                        if (placedBreaks > preCount)
                        {
                            lastRest = tasks.Last().End;
                        }
                        else
                        {
                            // Could not place due to tight window; push to the latest allowed (just under 3h)
                            var forcedStart = TimeHelpers.RoundDownToQuarter((lastRest + threeHours).AddMinutes(-10));
                            if (forcedStart <= lastRest) break;
                            tasks.Add(new CoverageTask
                            {
                                RoomNumber = t.RoomNumber,
                                TeacherName = t.Name,
                                Kind = CoverageTaskKind.Coverage,
                                Start = forcedStart,
                                End = forcedStart.AddMinutes(10),
                                BufferAfterMinutes = 5
                            });
                            placedBreaks++;
                            lastRest = forcedStart.AddMinutes(10);
                        }
                    }
                }

                // Second pass: top up to meet the 4h minimum (if needed)
                if (placedBreaks < minBreaks)
                {
                    foreach (var seg in segments)
                    {
                        if (placedBreaks >= minBreaks) break;

                        var segStart = seg.Item1;
                        var segEnd = seg.Item2;
                        if (segEnd - segStart < TimeSpan.FromHours(2) + breakDur) continue; // too short to place a mid-of-four break

                        var target = segStart + midOfFour;
                        addBreakNear(segStart, segEnd, target);
                    }
                }
            }

            // Resolve rounding collisions
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

            // NEW: hard-place each support's lunch as a real task before assigning coverage.
            PlaceSupportLunchHolds(day, bySupport, supportWindows);

            // Two-pass cutoff (AM before PM)
            var twoPassCutoff = TimeSpan.FromHours(12);
            var cutoff = day.Date.Date.Add(twoPassCutoff);

            // Order: AM tasks first, then lunch-length coverage before breaks, then by time
            var orderedTasks = teacherTasks
                .OrderBy(t => t.Start >= cutoff ? 1 : 0)
                .ThenBy(t => ((t.End - t.Start).TotalMinutes >= 25) ? 0 : 1)
                .ThenBy(t => t.Start)
                .ToList();

            foreach (var task in orderedTasks)
            {
                var candidates = new List<Tuple<Support, int>>(); // support, score
                var isLunchTask = (task.End - task.Start).TotalMinutes >= 25;

                foreach (var s in day.Supports)
                {
                    var sStart = day.Date.Date.Add(s.Start);
                    var sEnd = day.Date.Date.Add(s.End);
                    if (task.Start < sStart || task.EffectiveEnd > sEnd) continue;

                    if (IsFree(supportWindows[s.Name], task.Start, task.EffectiveEnd))
                    {
                        int score = 0;

                        if (isLunchTask) score -= 5;

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

                        var lastEnd = GetLastEffectiveEnd(supportWindows[s.Name]);
                        var idle = (task.Start - lastEnd).TotalMinutes;
                        if (idle > 45) score += 1;

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
                    // 1) local flex (±30m)
                    var flex = TryAssignWithFlex(day, task, bySupport, supportWindows, prefMap, lastRoomBySupport);

                    // 2) push pre-noon breaks to first PM slot
                    var pushed = false;
                    if (!flex)
                    {
                        var isBreak = (task.End - task.Start).TotalMinutes < 25;
                        if (isBreak && task.Start < cutoff)
                            pushed = TryAssignBreakPushRight(day, cutoff, task, bySupport, supportWindows, prefMap, lastRoomBySupport);
                    }

                    if (!flex && !pushed)
                    {
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

            foreach (var key in bySupport.Keys.ToList())
                bySupport[key] = bySupport[key].OrderBy(t => t.Start).ToList();

            return bySupport;
        }

        // Push a pre-noon BREAK to the first feasible slot after the cutoff, within teacher & support shifts.
        private bool TryAssignBreakPushRight(
            DayContext day,
            DateTime cutoff,
            CoverageTask task,
            Dictionary<string, List<CoverageTask>> bySupport,
            Dictionary<string, List<Tuple<DateTime, DateTime>>> supportWindows,
            Dictionary<string, List<string>> prefMap,
            Dictionary<string, string> lastRoomBySupport)
        {
            var duration = task.End - task.Start;

            // Keep within teacher’s shift
            var teacher = day.Teachers.FirstOrDefault(t =>
                string.Equals(t.Name, task.TeacherName, StringComparison.OrdinalIgnoreCase));
            var teacherShiftStart = teacher != null ? day.Date.Date.Add(teacher.Start) : task.Start;
            var teacherShiftEnd   = teacher != null ? day.Date.Date.Add(teacher.End)   : task.End;

            // Start scanning at or after cutoff
            var searchStart = TimeHelpers.RoundUpToQuarter(cutoff > task.Start ? cutoff : task.Start);

            var bestSupport = (Support)null;
            DateTime bestStart = DateTime.MinValue;
            int bestScore = int.MaxValue;
            long bestDelta = long.MaxValue;

            foreach (var s in day.Supports)
            {
                var sStart = day.Date.Date.Add(s.Start);
                var sEnd = day.Date.Date.Add(s.End);

                // Walk forward in 15-min steps
                for (var tryStart = searchStart; tryStart + duration <= teacherShiftEnd; tryStart = tryStart.Add(FlexStep))
                {
                    var tryEnd = tryStart + duration;
                    var effEnd = tryEnd.AddMinutes(task.BufferAfterMinutes);

                    if (tryStart < teacherShiftStart || effEnd > teacherShiftEnd) continue;
                    if (tryStart < sStart || effEnd > sEnd) continue;
                    if (!IsFree(supportWindows[s.Name], tryStart, effEnd)) continue;

                    int score = 0;

                    // Prefer listed preferred support for the room
                    if (prefMap.TryGetValue(task.RoomNumber, out var preferredList) &&
                        preferredList.Any(n => string.Equals(n, s.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        score -= 3;
                    }

                    // Prefer staying in same room as previous
                    if (!string.IsNullOrEmpty(lastRoomBySupport[s.Name]) &&
                        string.Equals(lastRoomBySupport[s.Name], task.RoomNumber, StringComparison.OrdinalIgnoreCase))
                    {
                        score -= 2;
                    }

                    // Mild penalty for long idle before this task
                    var lastEnd = GetLastEffectiveEnd(supportWindows[s.Name]);
                    var idle = (tryStart - lastEnd).TotalMinutes;
                    if (idle > 45) score += 1;

                    // prefer smaller displacement from original time
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

                    // Early exit: found a perfect fit with strong preference
                    if (bestScore <= -3 && delta == (bestStart - task.Start).Ticks) break;
                }
            }

            if (bestSupport != null)
            {
                var newEnd = bestStart + duration;
                AddAssigned(bySupport, supportWindows, lastRoomBySupport, bestSupport, task, bestStart, newEnd);
                return true;
            }

            return false;
        }

        // NEW: Hard-reserve a lunch window for each support so coverage cannot occupy it.
        // Lunch is required when shift > 5h or LunchRequired is true.
        private static void PreReserveSupportLunchWindows(
            DayContext day,
            Dictionary<string, List<Tuple<DateTime, DateTime>>> supportWindows)
        {
            var date = day.Date.Date;
            var earliest = date.AddHours(11); // 11:00 AM
            var latest   = date.AddHours(14); // 2:00 PM

            foreach (var s in day.Supports)
            {
                var sStart = date.Add(s.Start);
                var sEnd   = date.Add(s.End);
                var shiftHours = Math.Max(0, (sEnd - sStart).TotalHours);

                if (!(s.LunchRequired || shiftHours > 5.0)) continue;

                // Target near midpoint, but clamp to 11:00–14:00 and inside shift
                var midpoint = sStart + TimeSpan.FromTicks((sEnd - sStart).Ticks / 2);
                var start = TimeHelpers.ClampToQuarterWithin(midpoint, earliest, latest);
                if (start == DateTime.MinValue) start = TimeHelpers.RoundToNearestQuarter(midpoint);

                if (start < sStart) start = TimeHelpers.RoundUpToQuarter(sStart);
                var end = start.AddMinutes(30);
                if (end > sEnd)
                {
                    start = TimeHelpers.RoundDownToQuarter(sEnd.AddMinutes(-30));
                    end = start.AddMinutes(30);
                }

                // Only reserve if a 30-minute slot fits
                if (end <= sEnd && end > start)
                {
                    // Reserving raw lunch time (no buffer)
                    supportWindows[s.Name].Add(Tuple.Create(start, end));
                    supportWindows[s.Name] = supportWindows[s.Name].OrderBy(w => w.Item1).ToList();
                }
            }
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
            var lunchLatest   = day.Date.Date.AddHours(14); // 2:00 PM latest lunch start

            // Keep within teacher’s shift
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

                        var tryEnd = tryStart + duration;
                        if (tryStart < teacherShiftStart || tryEnd > teacherShiftEnd)
                            continue;

                        // Keep lunches within 11:00–14:00 window
                        if (isLunch && (tryStart < lunchEarliest || tryStart > lunchLatest)) continue;

                        var effEnd = tryEnd.AddMinutes(task.BufferAfterMinutes);

                        // Must fit within support’s shift and be free
                        if (tryStart < sStart || effEnd > sEnd) continue;
                        if (!IsFree(supportWindows[s.Name], tryStart, effEnd)) continue;

                        int score = 0;

                        // STRONG preference for lunches over breaks
                        if (isLunch) score -= 5;

                        // Prefer listed preferred support for the room
                        if (prefMap.TryGetValue(task.RoomNumber, out var preferredList) &&
                            preferredList.Any(n => string.Equals(n, s.Name, StringComparison.OrdinalIgnoreCase)))
                        {
                            score -= 3;
                        }

                        // Prefer staying in same room as previous
                        if (!string.IsNullOrEmpty(lastRoomBySupport[s.Name]) &&
                            string.Equals(lastRoomBySupport[s.Name], task.RoomNumber, StringComparison.OrdinalIgnoreCase))
                        {
                            score -= 2;
                        }

                        // Mild penalty for long idle before this task
                        var lastEnd = GetLastEffectiveEnd(supportWindows[s.Name]);
                        var idle = (tryStart - lastEnd).TotalMinutes;
                        if (idle > 45) score += 1;

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
                var alreadyHasLunch = list.Any(t => t.Kind == CoverageTaskKind.Lunch);
                var needsLunch = !alreadyHasLunch && (sShiftEnd - sShiftStart).TotalHours > 5.0;

                // Place lunch first near midpoint only if not already placed
                if (needsLunch)
                {
                    var midpoint = sShiftStart + TimeSpan.FromTicks((sShiftEnd - sShiftStart).Ticks / 2);
                    var placed = TryPlaceSelfCare(list, ref freeWindows, name, CoverageTaskKind.Lunch, name, "---", midpoint, 30, 0);
                    if (!placed)
                    {
                        var earliest = sShiftStart.Date.AddHours(11);
                        var latest = sShiftStart.Date.AddHours(14);
                        var start = TimeHelpers.ClampToQuarterWithin(midpoint, earliest, latest);
                        if (start == DateTime.MinValue) start = TimeHelpers.RoundUpToQuarter(earliest);

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
                    var target = sShiftStart.AddHours((i + 1) * 3.0);
                    var placed = TryPlaceSelfCare(list, ref freeWindows, name, CoverageTaskKind.Break, name, "---", target, 10, 5);
                    if (!placed)
                    {
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
                        TeacherName = name,
                        RoomNumber = "",
                        Start = w.Item1,
                        End = w.Item2,
                        BufferAfterMinutes = 0
                    });
                }

                list = list.OrderBy(t => t.Start).ToList();
                foreach (var t in list)
                {
                    if (string.IsNullOrWhiteSpace(t.SupportName))
                        t.SupportName = name;
                }

                bySupport[name] = list;
            }

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

        private static bool TryPlaceSelfCare(
            List<CoverageTask> list,
            ref List<Tuple<DateTime, DateTime>> freeWindows,
            string supportName,
            CoverageTaskKind kind,
            string teacherName,
            string room,
            DateTime target,
            int minutes,
            int bufferAfter)
        {
            var isLunch = kind == CoverageTaskKind.Lunch;

            // Sort free windows by closeness to the target time
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
                    var latestLunch = w.Item1.Date.AddHours(14);

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

                    // Update free windows by removing [Start, EffectiveEnd]
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
                    // Break/Idle placement
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

        // NEW: create an actual Lunch task per support and reserve that window.
        private static void PlaceSupportLunchHolds(
            DayContext day,
            Dictionary<string, List<CoverageTask>> bySupport,
            Dictionary<string, List<Tuple<DateTime, DateTime>>> supportWindows)
        {
            var date = day.Date.Date;
            var earliest = date.AddHours(11); // 11:00
            var latest   = date.AddHours(14); // 14:00

            foreach (var s in day.Supports)
            {
                var sStart = date.Add(s.Start);
                var sEnd   = date.Add(s.End);
                var shiftHours = Math.Max(0, (sEnd - sStart).TotalHours);

                if (!s.LunchRequired) continue;

                var midpoint = sStart + TimeSpan.FromTicks((sEnd - sStart).Ticks / 2);
                var start = TimeHelpers.ClampToQuarterWithin(midpoint, earliest, latest);
                if (start == DateTime.MinValue) start = TimeHelpers.RoundToNearestQuarter(midpoint);

                if (start < sStart) start = TimeHelpers.RoundUpToQuarter(sStart);
                var end = start.AddMinutes(30);
                if (end > sEnd)
                {
                    start = TimeHelpers.RoundDownToQuarter(sEnd.AddMinutes(-30));
                    end = start.AddMinutes(30);
                }

                if (end <= sEnd && end > start)
                {
                    // Add the lunch task to the visible schedule
                    var lunch = new CoverageTask
                    {
                        SupportName = s.Name,
                        Kind = CoverageTaskKind.Lunch,
                        TeacherName = s.Name,   // show owning support in the Teacher column
                        RoomNumber = "---",
                        Start = start,
                        End = end,
                        BufferAfterMinutes = 0
                    };

                    bySupport[s.Name].Add(lunch);
                    bySupport[s.Name] = bySupport[s.Name].OrderBy(t => t.Start).ToList();

                    // Also reserve the time so coverage assignment cannot overlap it
                    supportWindows[s.Name].Add(Tuple.Create(start, end));
                    supportWindows[s.Name] = supportWindows[s.Name].OrderBy(w => w.Item1).ToList();
                }
            }
        }
    }
}