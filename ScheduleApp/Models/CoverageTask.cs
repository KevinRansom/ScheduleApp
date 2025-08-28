using System;

namespace ScheduleApp.Models
{
    public enum CoverageTaskKind
    {
        Coverage, // support covering a teacher task
        Break,    // self-care break
        Lunch,    // self-care lunch
        Idle      // waiting (displayed as "Free")
    }

    public class CoverageTask
    {
        public string RoomNumber { get; set; } // --- for self-care/idle
        public string TeacherName { get; set; } // "Self" for self-care, "" for idle
        public string SupportName { get; set; } // filled for support timelines
        public CoverageTaskKind Kind { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public int BufferAfterMinutes { get; set; } // 5 for 10-min breaks, 0 otherwise

        public DateTime EffectiveEnd => End.AddMinutes(BufferAfterMinutes);

        public int Minutes => (int)Math.Round((End - Start).TotalMinutes);

        // Display helpers for aligned columns in visual/text views
        public string TaskName
        {
            get
            {
                if (Kind == CoverageTaskKind.Coverage) return Minutes >= 25 ? "Lunch" : "Break";
                if (Kind == CoverageTaskKind.Lunch) return "Lunch";
                if (Kind == CoverageTaskKind.Break) return "Break";
                return "Free"; // Idle shown as Free
            }
        }

        public string DurationText
        {
            get
            {
                // Show minutes for Break/Lunch/Free; empty for other kinds
                if (TaskName == "Break" || TaskName == "Lunch" || TaskName == "Free")
                    return Minutes + "min";
                return "";
            }
        }

        public string TeacherDisplay
        {
            get
            {
                // For self-care/free rows show Self; otherwise actual teacher
                if (Kind == CoverageTaskKind.Coverage) return string.IsNullOrWhiteSpace(TeacherName) ? "Self" : TeacherName;
                return "Self";
            }
        }

        public string RoomDisplay
        {
            get
            {
                // Coverage shows actual room, others as ---
                if (Kind == CoverageTaskKind.Coverage)
                    return string.IsNullOrWhiteSpace(RoomNumber) ? "---" : RoomNumber;
                return "---";
            }
        }

        public string StartText => Start.ToString("HH:mm");

        public string DisplayTitle
        {
            get
            {
                switch (Kind)
                {
                    case CoverageTaskKind.Coverage:
                        return string.Format("Coverage | {0} {1}", TeacherName, string.IsNullOrEmpty(RoomNumber) ? "" : "(" + RoomNumber + ")");
                    case CoverageTaskKind.Break:
                        return "Break (10m)";
                    case CoverageTaskKind.Lunch:
                        return "Lunch (30m)";
                    default:
                        return string.Format("Free ({0}m)", Minutes);
                }
            }
        }

        public string DetailsLine
        {
            get
            {
                var start = Start.ToString("HH:mm");
                string who = Kind == CoverageTaskKind.Coverage
                    ? string.Format("Teacher: {0} | Room: {1}", TeacherDisplay, RoomDisplay)
                    : "Teacher: Self | Room: ---";

                string kindPart;
                if (Kind == CoverageTaskKind.Coverage)
                    kindPart = Minutes >= 25 ? "Lunch: 30min" : "Break: 10min";
                else if (Kind == CoverageTaskKind.Lunch) kindPart = "Lunch: 30min";
                else if (Kind == CoverageTaskKind.Break) kindPart = "Break: 10min";
                else kindPart = string.Format("Free: {0}min", Minutes);

                return string.Format("Support: {0} | {1} | {2} | Start: {3}", SupportName, kindPart, who, start);
            }
        }
    }
}