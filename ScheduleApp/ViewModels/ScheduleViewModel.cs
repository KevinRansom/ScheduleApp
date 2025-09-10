using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ScheduleApp.Models;

namespace ScheduleApp.ViewModels
{
    public class ScheduleViewModel : BaseViewModel
    {
        public ObservableCollection<SupportTabViewModel> SupportTabs { get; } = new ObservableCollection<SupportTabViewModel>();

        // NEW: flattened support entries used by the By Support left-hand list
        public ObservableCollection<SupportStaffEntry> SupportEntries { get; } = new ObservableCollection<SupportStaffEntry>();

        // View mode: "Text", "Visual", "Grid"
        private string _viewMode = "Visual";
        public string ViewMode
        {
            get { return _viewMode; }
            set
            {
                if (_viewMode == value) return;
                _viewMode = value;
                Raise();
                Raise(nameof(ShowText));
                Raise(nameof(ShowVisual));
                Raise(nameof(ShowGrid));
            }
        }

        public bool ShowText  { get { return string.Equals(ViewMode, "Text",   StringComparison.OrdinalIgnoreCase); } }
        public bool ShowVisual{ get { return string.Equals(ViewMode, "Visual", StringComparison.OrdinalIgnoreCase); } }
        public bool ShowGrid  { get { return string.Equals(ViewMode, "Grid",   StringComparison.OrdinalIgnoreCase); } }

        public void LoadTabs(SupportTabViewModel[] tabs)
        {
            SupportTabs.Clear();
            for (int i = 0; i < tabs.Length; i++)
            {
                SupportTabs.Add(tabs[i]);
            }
            Raise(nameof(SupportTabs));

            // Build support lookup for quick access by support name
            _supportRowsByName.Clear();
            foreach (var tab in tabs ?? Array.Empty<SupportTabViewModel>())
            {
                var key = tab?.SupportName ?? string.Empty;
                // store a copy (defensive) to avoid accidental external mutation
                _supportRowsByName[key] = (tab?.Tasks ?? new List<CoverageTask>()).ToList();
            }
        }

        // NEW: Update the SupportEntries collection from the set of tabs.
        // Ensure any "Unscheduled Breaks" entry is placed first, then the remaining tabs in order.
        public void UpdateSupportEntries(SupportTabViewModel[] tabs)
        {
            SupportEntries.Clear();
            if (tabs == null || tabs.Length == 0) return;

            // Find any unscheduled entries (match either display name or "Unscheduled")
            var unscheduledKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Unscheduled Breaks", "Unscheduled" };

            // Add Unscheduled entries first (preserve original order among unscheduled if multiple)
            foreach (var t in tabs.Where(tt => tt != null && unscheduledKeys.Contains(tt.SupportName)))
            {
                var name = t.SupportName;
                if (string.IsNullOrWhiteSpace(name)) continue;
                SupportEntries.Add(new SupportStaffEntry(name, isUnscheduled: true));
            }

            // Then add all other tab names in the incoming order, skipping any unscheduled already added
            foreach (var t in tabs)
            {
                if (t == null) continue;
                var name = t.SupportName;
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (unscheduledKeys.Contains(name)) continue; // already added
                SupportEntries.Add(new SupportStaffEntry(name, isUnscheduled: false));
            }

            Raise(nameof(SupportEntries));
        }

        // --- NEW: Teacher view state ---
        private readonly Dictionary<string, List<TeacherScheduleRow>> _teacherRowsByName
            = new Dictionary<string, List<TeacherScheduleRow>>(StringComparer.OrdinalIgnoreCase);

        public ObservableCollection<TeacherScheduleRow> SelectedTeacherRows { get; }
            = new ObservableCollection<TeacherScheduleRow>();

        private Teacher _selectedTeacher;
        public Teacher SelectedTeacher
        {
            get { return _selectedTeacher; }
            set
            {
                if (_selectedTeacher == value) return;
                _selectedTeacher = value;
                Raise();
                RefreshSelectedTeacherRows();
            }
        }

        private void RefreshSelectedTeacherRows()
        {
            SelectedTeacherRows.Clear();
            var key = _selectedTeacher?.Name;
            if (string.IsNullOrWhiteSpace(key)) return;

            if (_teacherRowsByName.TryGetValue(key, out var rows))
            {
                foreach (var r in rows.OrderBy(r => r.SortKey))
                    SelectedTeacherRows.Add(r);
            }
        }

        // Build teacher rows from teachers and all assigned coverage tasks
        public void LoadTeacherSchedules(DateTime date, IList<Teacher> teachers, IList<CoverageTask> allAssigned)
        {
            _teacherRowsByName.Clear();

            foreach (var t in teachers ?? Array.Empty<Teacher>())
            {
                var rows = new List<TeacherScheduleRow>();

                // Start of Day
                var start = date.Date + t.Start;
                rows.Add(new TeacherScheduleRow
                {
                    TeacherName = t.Name,
                    SupportStaff = string.Empty,        // per requirement
                    Activity = "Start of Day",
                    Duration = string.Empty,
                    Start = start.ToString("HH:mm"),
                    SortKey = start
                });

                // Teacher coverage needs (breaks/lunch) that were assigned (or marked Unscheduled)
                var myCoverage = (allAssigned ?? Array.Empty<CoverageTask>())
                    .Where(ct => ct.Kind == CoverageTaskKind.Coverage &&
                                  string.Equals(ct.TeacherName, t.Name, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(ct => ct.Start)
                    .ToList();

                foreach (var ct in myCoverage)
                {
                    var isLunch = (ct.End - ct.Start).TotalMinutes >= 25.0;
                    rows.Add(new TeacherScheduleRow
                    {
                        TeacherName = t.Name,
                        SupportStaff = ct.SupportName ?? "", // "Unscheduled" already present for unassigned
                        Activity = isLunch ? "Lunch" : "Break",
                        Duration = ct.DurationText,
                        Start = ct.Start.ToString("HH:mm"),
                        SortKey = ct.Start
                    });
                }

                // End of Day
                var end = date.Date + t.End;
                rows.Add(new TeacherScheduleRow
                {
                    TeacherName = t.Name,
                    SupportStaff = string.Empty,        // per requirement (not break or lunch)
                    Activity = "End of Day",
                    Duration = string.Empty,
                    Start = end.ToString("HH:mm"),
                    SortKey = end
                });

                _teacherRowsByName[t.Name ?? ""] = rows;
            }

            // Refresh current selection (keeps the same SelectedTeacher if set)
            RefreshSelectedTeacherRows();
        }

        // NEW: show schedules for multiple selected teachers
        public void ShowTeachers(IEnumerable<Teacher> teachers)
        {
            SelectedTeacherRows.Clear();
            if (teachers == null) return;

            // Collect rows for each selected teacher (preserve each teacher's TeacherName and SortKey)
            var rows = new List<TeacherScheduleRow>();
            foreach (var t in teachers)
            {
                if (t == null) continue;
                var key = t.Name ?? "";
                if (_teacherRowsByName.TryGetValue(key, out var rlist))
                {
                    rows.AddRange(rlist);
                }
            }

            // Order rows by TeacherName then by time so grid groups cleanly
            foreach (var r in rows.OrderBy(r => r.TeacherName).ThenBy(r => r.SortKey))
                SelectedTeacherRows.Add(r);
        }

        // --- NEW: Support view state ---
        private readonly Dictionary<string, List<CoverageTask>> _supportRowsByName
            = new Dictionary<string, List<CoverageTask>>(StringComparer.OrdinalIgnoreCase);

        public ObservableCollection<CoverageTask> SelectedSupportRows { get; }
            = new ObservableCollection<CoverageTask>();

        // Show schedules for the selected support staff (supports may be multi-selected)
        public void ShowSupports(IEnumerable<Support> supports)
        {
            SelectedSupportRows.Clear();
            if (supports == null) return;

            var rows = new List<CoverageTask>();
            foreach (var s in supports)
            {
                if (s == null) continue;
                var key = s.Name ?? string.Empty;

                // Try direct match first
                if (_supportRowsByName.TryGetValue(key, out var list))
                {
                    rows.AddRange(list);
                    continue;
                }

                // Fallback: handle the special "Unscheduled" display name used in tabs
                if (string.Equals(key, "Unscheduled", StringComparison.OrdinalIgnoreCase))
                {
                    // tab display uses "Unscheduled Breaks" — try that too
                    if (_supportRowsByName.TryGetValue("Unscheduled Breaks", out var ulist))
                        rows.AddRange(ulist);
                }
            }

            // Order by support name then by start time for a grouped, chronological display
            foreach (var r in rows.OrderBy(r => r.SupportName).ThenBy(r => r.Start))
                SelectedSupportRows.Add(r);
        }
    }
}