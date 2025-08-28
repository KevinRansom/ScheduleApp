using System;
using System.Linq;
using System.Printing;
using System.Windows.Documents;
using ScheduleApp.Models;
using ScheduleApp.ViewModels;

namespace ScheduleApp.Services
{
    public class PrintService
    {
        public FlowDocument BuildFlowDocument(SupportTabViewModel[] tabs)
        {
            var doc = new FlowDocument
            {
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 12
            };

            foreach (var tab in tabs.OrderBy(t => t.SupportName))
            {
                var section = new Section();
                section.Blocks.Add(new Paragraph(new Run("Support: " + tab.SupportName))
                {
                    FontSize = 16,
                    FontWeight = System.Windows.FontWeights.Bold
                });

                var lines = BuildAlignedLines(tab.Tasks.OrderBy(t => t.Start).ToArray());
                section.Blocks.Add(new Paragraph(new Run(string.Join(Environment.NewLine, lines))));
                section.Blocks.Add(new Paragraph(new Run(" "))); // spacer
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

            if (rows.Length == 0)
                return new[] { string.Join(" | ", headers) };

            var colWidths = new int[headers.Length];
            for (int c = 0; c < colWidths.Length; c++)
            {
                var maxRow = rows.Max(r => r[c].Length);
                colWidths[c] = Math.Max(headers[c].Length, maxRow);
            }

            string Pad(string s, int w) { return (s ?? string.Empty).PadRight(w); }

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
            return "Free"; // previously Idle
        }

        public void PrintFlowDocument(FlowDocument doc)
        {
            var pd = new System.Windows.Controls.PrintDialog();
            if (pd.ShowDialog() == true)
            {
                IDocumentPaginatorSource dps = doc;
                pd.PrintDocument(dps.DocumentPaginator, "Support Schedules");
            }
        }

        public void PrintToPdf(FlowDocument doc, string filename)
        {
            var server = new LocalPrintServer();
            PrintQueue pdfQueue = null;

            try
            {
                var queues = server.GetPrintQueues(new[]
                {
                    System.Printing.EnumeratedPrintQueueTypes.Local,
                    System.Printing.EnumeratedPrintQueueTypes.Connections
                });

                pdfQueue = queues.Cast<PrintQueue>()
                    .FirstOrDefault(q => q.Name.IndexOf("Microsoft Print to PDF", StringComparison.OrdinalIgnoreCase) >= 0);
            }
            catch
            {
                var queues = server.GetPrintQueues();
                pdfQueue = queues.Cast<PrintQueue>()
                    .FirstOrDefault(q => q.Name.IndexOf("Microsoft Print to PDF", StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (pdfQueue == null)
                throw new InvalidOperationException("Microsoft Print to PDF printer not found.");

            var writer = PrintQueue.CreateXpsDocumentWriter(pdfQueue);
            IDocumentPaginatorSource dps = doc;
            writer.Write(dps.DocumentPaginator);
        }
    }
}