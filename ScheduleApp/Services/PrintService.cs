using System;
using System.IO;
using System.Linq;
using System.Printing;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Win32;
// PDFsharp
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using ScheduleApp.Models;
using ScheduleApp.ViewModels;

namespace ScheduleApp.Services
{
    public class PrintService
    {
        // Existing method kept for compatibility (prints only support schedules)
        public string SaveScheduleAsPdf(SupportTabViewModel[] tabs, string outputDirectory, string staffNames, string appVersion)
        {
            // Forward to the new overload that accepts teachers.
            // Passing null for teachers will generate the PDF with support pages only (no teacher pages).
            return SaveScheduleAsPdf(tabs, null, outputDirectory, staffNames, appVersion);
        }

        // NEW: Also prints each teacher’s schedule after support schedules
        public string SaveScheduleAsPdf(
            SupportTabViewModel[] tabs,
            System.Collections.Generic.IList<Teacher> teachers,
            string outputDirectory,
            string staffNames,
            string appVersion)
        {
            if (tabs == null) throw new ArgumentNullException(nameof(tabs));
            if (string.IsNullOrWhiteSpace(outputDirectory)) throw new ArgumentException("Output directory required.", nameof(outputDirectory));

            Directory.CreateDirectory(outputDirectory);

            var fileName = $"BreakSchedule_{DateTime.Today:yyyy-MM-dd}.pdf";
            var fullPath = Path.Combine(outputDirectory, fileName);

            var orderedSupports = tabs.OrderBy(t => t.SupportName).ToArray();
            var allTasks = orderedSupports.SelectMany(t => t.Tasks ?? Enumerable.Empty<CoverageTask>()).OrderBy(t => t.Start).ToArray();
            var orderedTeachers = (teachers ?? Array.Empty<Teacher>()).OrderBy(t => t.Name).ToArray();

            using (var pdf = new PdfDocument())
            {
                // Metadata
                pdf.Info.Title = "Break Schedule";
                pdf.Info.Subject = "Daily break/support coverage schedule";
                pdf.Info.Author = string.IsNullOrWhiteSpace(staffNames) ? "ScheduleApp" : staffNames;
                pdf.Info.Keywords = "Schedule;Break;Coverage;Staff;Teacher";
                pdf.Info.CreationDate = DateTime.Now;
                pdf.Info.Creator = $"ScheduleApp {appVersion ?? "1.0"}";

                // Page config
                const double margin = 48; // pt
                var page = pdf.AddPage();
                page.Size = PdfSharp.PageSize.Letter;
                var gfx = XGraphics.FromPdfPage(page);

                // Fonts
                var fontOpts   = new XPdfFontOptions(PdfFontEncoding.Unicode, PdfFontEmbedding.TryComputeSubset);
                var headerFont = new XFont("Courier", 16, XFontStyleEx.Bold, fontOpts);
                var bodyFont   = new XFont("Courier", 12, XFontStyleEx.Regular, fontOpts);

                // Dimensions in points
                double pageWidthPt = page.Width.Point;
                double pageHeightPt = page.Height.Point;

                // Layout
                double contentWidth   = pageWidthPt - (margin * 2);
                double bodyLineHeight = gfx.MeasureString("Mg", bodyFont).Height * 1.25;
                double y = margin;

                void NewPage()
                {
                    gfx.Dispose();
                    page = pdf.AddPage();
                    page.Size = PdfSharp.PageSize.Letter;
                    gfx = XGraphics.FromPdfPage(page);
                    pageWidthPt = page.Width.Point;
                    pageHeightPt = page.Height.Point;
                    y = margin;
                }

                void DrawHeader(string headerText, bool isContinuation)
                {
                    var text = isContinuation ? headerText + " (cont.)" : headerText;
                    var h = gfx.MeasureString(text, headerFont).Height;
                    gfx.DrawString(text, headerFont, XBrushes.Black, new XRect(margin, y, contentWidth, h), XStringFormats.TopLeft);
                    y += h + (bodyLineHeight * 0.5);
                }

                // Max characters per line for monospace wrapping
                int maxCharsPerLine;
                {
                    var glyphW = Math.Max(1.0, gfx.MeasureString("M", bodyFont).Width);
                    maxCharsPerLine = Math.Max(1, (int)Math.Floor(contentWidth / glyphW));
                }

                // 1) Support schedules (existing pages)
                for (int i = 0; i < orderedSupports.Length; i++)
                {
                    var tab = orderedSupports[i];
                    var header = "Support: " + tab.SupportName;

                    if (i > 0) NewPage();
                    DrawHeader(header, isContinuation: false);

                    var lines = BuildAlignedLines(tab.Tasks.OrderBy(t => t.Start).ToArray());
                    foreach (var line in lines)
                    {
                        foreach (var wrapped in WrapMonospace(line, maxCharsPerLine))
                        {
                            if (y + bodyLineHeight > pageHeightPt - margin)
                            {
                                NewPage();
                                DrawHeader(header, isContinuation: true);
                            }

                            gfx.DrawString(wrapped, bodyFont, XBrushes.Black,
                                new XRect(margin, y, contentWidth, bodyLineHeight), XStringFormats.TopLeft);
                            y += bodyLineHeight;
                        }
                    }

                    y += bodyLineHeight;
                }

                // 2) Teacher schedules (one page per teacher)
                for (int i = 0; i < orderedTeachers.Length; i++)
                {
                    var teacher = orderedTeachers[i];
                    NewPage();
                    var header = "Teacher: " + (teacher?.Name ?? "(unknown)");
                    DrawHeader(header, isContinuation: false);

                    var lines = BuildTeacherLines(teacher, allTasks);
                    foreach (var line in lines)
                    {
                        foreach (var wrapped in WrapMonospace(line, maxCharsPerLine))
                        {
                            if (y + bodyLineHeight > pageHeightPt - margin)
                            {
                                NewPage();
                                DrawHeader(header, isContinuation: true);
                            }

                            gfx.DrawString(wrapped, bodyFont, XBrushes.Black,
                                new XRect(margin, y, contentWidth, bodyLineHeight), XStringFormats.TopLeft);
                            y += bodyLineHeight;
                        }
                    }

                    y += bodyLineHeight;
                }

                pdf.Save(fullPath);
            }

            return fullPath;
        }

        // Print any WPF Visual (e.g., Grid/UserControl) to paper via Windows PrintDialog.
        public void PrintVisualToPaper(Visual visual, string jobName)
        {
            if (visual == null) throw new ArgumentNullException(nameof(visual));
            var dlg = new System.Windows.Controls.PrintDialog();
            if (dlg.ShowDialog() == true)
            {
                var name = string.IsNullOrWhiteSpace(jobName) ? "Schedule" : jobName;
                dlg.PrintVisual(visual, name);
            }
        }

        // Print a FlowDocument to paper via Windows PrintDialog.
        public void PrintFlowDocument(FlowDocument doc, string jobName = "Support Schedules")
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            var dlg = new System.Windows.Controls.PrintDialog();
            if (dlg.ShowDialog() == true)
            {
                IDocumentPaginatorSource dps = doc;
                dlg.PrintDocument(dps.DocumentPaginator, string.IsNullOrWhiteSpace(jobName) ? "Support Schedules" : jobName);
            }
        }

        // Overload kept for existing callers.
        public void PrintFlowDocument(FlowDocument doc)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            var pd = new System.Windows.Controls.PrintDialog();
            if (pd.ShowDialog() == true)
            {
                IDocumentPaginatorSource dps = doc;
                pd.PrintDocument(dps.DocumentPaginator, "Support Schedules");
            }
        }

        public FlowDocument BuildFlowDocument(SupportTabViewModel[] tabs)
        {
            var ordered = tabs.OrderBy(t => t.SupportName).ToArray();

            var doc = new FlowDocument
            {
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 12,
                PagePadding = new Thickness(48),
                ColumnWidth = double.PositiveInfinity
            };

            for (int i = 0; i < ordered.Length; i++)
            {
                var tab = ordered[i];
                var section = new Section { BreakPageBefore = i > 0 };

                section.Blocks.Add(new Paragraph(new Run("Support: " + tab.SupportName))
                {
                    FontSize = 16,
                    FontWeight = FontWeights.Bold
                });

                var lines = BuildAlignedLines(tab.Tasks.OrderBy(t => t.Start).ToArray());
                section.Blocks.Add(new Paragraph(new Run(string.Join(Environment.NewLine, lines))));
                section.Blocks.Add(new Paragraph(new Run(" ")));
                section.Blocks.Add(new Paragraph(new Run(" "))); // extra space

                doc.Blocks.Add(section);
            }

            return doc;
        }

        // NEW: FlowDocument that includes both support schedules and one section per teacher
        public FlowDocument BuildFlowDocument(SupportTabViewModel[] tabs, System.Collections.Generic.IList<Teacher> teachers)
        {
            var orderedSupports = (tabs ?? Array.Empty<SupportTabViewModel>()).OrderBy(t => t.SupportName).ToArray();
            var allTasks = orderedSupports.SelectMany(t => t.Tasks ?? Enumerable.Empty<CoverageTask>()).OrderBy(t => t.Start).ToArray();
            var orderedTeachers = (teachers ?? Array.Empty<Teacher>()).OrderBy(t => t.Name).ToArray();

            var doc = new FlowDocument
            {
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 12,
                PagePadding = new Thickness(48),
                ColumnWidth = double.PositiveInfinity
            };

            // 1) Support sections
            for (int i = 0; i < orderedSupports.Length; i++)
            {
                var tab = orderedSupports[i];
                var section = new Section { BreakPageBefore = i > 0 };

                section.Blocks.Add(new Paragraph(new Run("Support: " + tab.SupportName))
                {
                    FontSize = 16,
                    FontWeight = FontWeights.Bold
                });

                var lines = BuildAlignedLines(tab.Tasks.OrderBy(t => t.Start).ToArray());
                section.Blocks.Add(new Paragraph(new Run(string.Join(Environment.NewLine, lines))));
                section.Blocks.Add(new Paragraph(new Run(" ")));
                section.Blocks.Add(new Paragraph(new Run(" "))); // extra space

                doc.Blocks.Add(section);
            }

            // 2) Teacher sections
            for (int i = 0; i < orderedTeachers.Length; i++)
            {
                var teacher = orderedTeachers[i];
                var section = new Section { BreakPageBefore = true };

                section.Blocks.Add(new Paragraph(new Run("Teacher: " + (teacher?.Name ?? "(unknown)")))
                {
                    FontSize = 16,
                    FontWeight = FontWeights.Bold
                });

                var lines = BuildTeacherLines(teacher, allTasks);
                section.Blocks.Add(new Paragraph(new Run(string.Join(Environment.NewLine, lines))));
                section.Blocks.Add(new Paragraph(new Run(" ")));
                section.Blocks.Add(new Paragraph(new Run(" "))); // extra space

                doc.Blocks.Add(section);
            }

            return doc;
        }

        private static string[] BuildAlignedLines(CoverageTask[] tasks)
        {
            var headers = new[] { "Support", "Task", "Duration", "Teacher", "Room", "Start" };

            var rows = tasks.Select(t =>
            {
                var task = GetTaskName(t);
                var duration = (task == "Break" || task == "Lunch" || task == "Free") ? (t.Minutes.ToString() + "min") : "";
                var teacher = string.IsNullOrWhiteSpace(t.TeacherName) ? "Self" : t.TeacherName;
                var room = string.IsNullOrWhiteSpace(t.RoomNumber) ? "---" : t.RoomNumber;

                return new[] { t.SupportName ?? "", task, duration, teacher, room, t.Start.ToString("HH:mm") };
            }).ToArray();

            if (rows.Length == 0) return new[] { string.Join(" | ", headers) };

            var colWidths = new int[headers.Length];
            for (int c = 0; c < colWidths.Length; c++)
            {
                var maxRow = rows.Max(r => r[c].Length);
                colWidths[c] = Math.Max(headers[c].Length, maxRow);
            }

            string Pad(string s, int w) => (s ?? string.Empty).PadRight(w);

            var headerLine = string.Join(" | ", headers.Select((h, i) => Pad(h, colWidths[i])));
            var sepLine = string.Join("-+-", colWidths.Select(w => new string('-', w)));
            var bodyLines = rows.Select(r => string.Join(" | ", r.Select((col, i) => Pad(col, colWidths[i]))));

            return new[] { headerLine, sepLine }.Concat(bodyLines).ToArray();
        }

        // NEW: Teacher schedule lines
        private static string[] BuildTeacherLines(Teacher teacher, CoverageTask[] allTasks)
        {
            var headers = new[] { "Name", "Support Staff", "Activity", "Duration", "Start" };

            var rows = new System.Collections.Generic.List<string[]>();

            var name = teacher?.Name ?? "";
            var start = DateTime.Today.Add(teacher?.Start ?? TimeSpan.Zero).ToString("HH:mm");
            var end = DateTime.Today.Add(teacher?.End ?? TimeSpan.Zero).ToString("HH:mm");

            // Start of Day
            rows.Add(new[] { name, "", "Start of Day", "", start });

            // Assigned coverage (breaks/lunch)
            var mine = (allTasks ?? Array.Empty<CoverageTask>())
                .Where(ct => ct.Kind == CoverageTaskKind.Coverage &&
                             string.Equals(ct.TeacherName, name, StringComparison.OrdinalIgnoreCase))
                .OrderBy(ct => ct.Start)
                .ToArray();

            foreach (var ct in mine)
            {
                var isLunch = (ct.End - ct.Start).TotalMinutes >= 25.0;
                rows.Add(new[]
                {
                    name,
                    ct.SupportName ?? "",
                    isLunch ? "Lunch" : "Break",
                    ct.DurationText,
                    ct.Start.ToString("HH:mm")
                });
            }

            // End of Day
            rows.Add(new[] { name, "", "End of Day", "", end });

            // Column widths
            var colWidths = new int[headers.Length];
            for (int c = 0; c < colWidths.Length; c++)
            {
                var maxRow = rows.Count > 0 ? rows.Max(r => r[c].Length) : 0;
                colWidths[c] = Math.Max(headers[c].Length, maxRow);
            }

            string Pad(string s, int w) => (s ?? string.Empty).PadRight(w);

            var headerLine = string.Join(" | ", headers.Select((h, i) => Pad(h, colWidths[i])));
            var sepLine = string.Join("-+-", colWidths.Select(w => new string('-', w)));
            var bodyLines = rows.Select(r => string.Join(" | ", r.Select((col, i) => Pad(col, colWidths[i]))));

            return new[] { headerLine, sepLine }.Concat(bodyLines).ToArray();
        }

        private static string GetTaskName(CoverageTask t)
        {
            if (t.Kind == CoverageTaskKind.Coverage)
                return t.Minutes >= 25 ? "Lunch" : "Break";
            if (t.Kind == CoverageTaskKind.Lunch) return "Lunch";
            if (t.Kind == CoverageTaskKind.Break) return "Break";
            return "Free";
        }

        private static string[] WrapMonospace(string line, int maxChars)
        {
            if (string.IsNullOrEmpty(line) || maxChars <= 0) return new[] { line ?? string.Empty };
            if (line.Length <= maxChars) return new[] { line };

            var parts = new System.Collections.Generic.List<string>();
            int start = 0;
            while (start < line.Length)
            {
                int len = Math.Min(maxChars, line.Length - start);

                int breakAt = -1;
                if (start + len < line.Length)
                    breakAt = line.LastIndexOf(' ', start + len - 1, len);
                if (breakAt <= start) breakAt = start + len;

                parts.Add(line.Substring(start, breakAt - start).TrimEnd());
                start = breakAt;
                while (start < line.Length && line[start] == ' ') start++;
            }
            return parts.ToArray();
        }
    }
}