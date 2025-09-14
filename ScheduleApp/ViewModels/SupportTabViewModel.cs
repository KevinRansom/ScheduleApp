using System;
using System.Collections.Generic;
using System.Linq;
using ScheduleApp.Models;

namespace ScheduleApp.ViewModels
{
    public class SupportTabViewModel : BaseViewModel
    {
        public string SupportName { get; set; }
        public List<CoverageTask> Tasks { get; set; } = new List<CoverageTask>();

        public string ScheduleText
        {
            get
            {
                var ordered = Tasks.OrderBy(t => t.Start).ToArray();
                if (ordered.Length == 0) return string.Empty;

                var headers = new[] { "Support", "Task", "Duration", "Teacher", "Room", "Start" };

                var rows = ordered.Select(t =>
                {
                    var task = GetTaskName(t);
                    var duration = t.DurationText; // always show duration

                    // Show "Self" when this is a self-care/free/idle slot for the support staff
                    string teacher;
                    if (string.IsNullOrWhiteSpace(t.TeacherName))
                    {
                        teacher = "Self";
                    }
                    else
                    {
                        // If the teacher name equals the support name and the task is a self-care kind,
                        // display "Self" instead of repeating the support's name.
                        if (string.Equals(t.TeacherName, t.SupportName, StringComparison.OrdinalIgnoreCase)
                            && (t.Kind == CoverageTaskKind.Break || t.Kind == CoverageTaskKind.Lunch || t.Kind == CoverageTaskKind.Idle))
                        {
                            teacher = "Self";
                        }
                        else
                        {
                            teacher = t.TeacherName;
                        }
                    }

                    var room = string.IsNullOrWhiteSpace(t.RoomNumber) ? "---" : t.RoomNumber;

                    return new[]
                    {
                        t.SupportName ?? "",
                        task,
                        duration,
                        teacher,
                        room,
                        t.Start.ToString("HH:mm")
                    };
                }).ToArray();

                var colWidths = new int[headers.Length];
                for (int c = 0; c < colWidths.Length; c++)
                {
                    var maxRow = rows.Length > 0 ? rows.Max(r => r[c].Length) : 0;
                    colWidths[c] = Math.Max(headers[c].Length, maxRow);
                }

                string Pad(string s, int w) { return (s ?? string.Empty).PadRight(w); }

                var headerLine = string.Join(" | ", headers.Select((h, i) => Pad(h, colWidths[i])));
                var sepLine = string.Join("-+-", colWidths.Select(w => new string('-', w)));
                var bodyLines = rows.Select(r => string.Join(" | ", r.Select((col, i) => Pad(col, colWidths[i]))));

                return string.Join(Environment.NewLine, new[] { headerLine, sepLine }.Concat(bodyLines));
            }
        }

        private static string GetTaskName(CoverageTask t)
        {
            if (t.Kind == CoverageTaskKind.Coverage) return "Coverage";
            if (t.Kind == CoverageTaskKind.Lunch)    return "Lunch";
            if (t.Kind == CoverageTaskKind.Break)    return "Break";
            return "Free";
        }
    }
}