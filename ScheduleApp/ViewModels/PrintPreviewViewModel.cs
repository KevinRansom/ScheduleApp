using System.Linq;
using System.Windows;
using System.Windows.Documents;
using Microsoft.Win32;
using ScheduleApp.Infrastructure;
using ScheduleApp.Services;
using ScheduleApp.Models; // NEW
using System.IO; // <- added

namespace ScheduleApp.ViewModels
{
    public class PrintPreviewViewModel : BaseViewModel
    {
        private readonly PrintService _printService = new PrintService();

        private FlowDocument _document;
        public FlowDocument Document { get { return _document; } set { _document = value; Raise(); } }

        private SupportTabViewModel _selectedTab;
        public SupportTabViewModel SelectedTab { get { return _selectedTab; } set { _selectedTab = value; Raise(); } }

        public RelayCommand PrintAllCommand { get; }
        public RelayCommand PrintSelectedCommand { get; }
        public RelayCommand ExportPdfCommand { get; }

        public PrintPreviewViewModel()
        {
            PrintAllCommand = new RelayCommand(PrintAll, () => Document != null);
            PrintSelectedCommand = new RelayCommand(PrintSelected, () => SelectedTab != null);
            ExportPdfCommand = new RelayCommand(ExportPdf, () => SelectedTab != null);
        }

        // OLD signature kept for compatibility (supports-only)
        public void RefreshDocument(SupportTabViewModel[] tabs)
        {
            Document = _printService.BuildFlowDocument(tabs);
        }

        // NEW: supports + teachers
        public void RefreshDocument(SupportTabViewModel[] tabs, System.Collections.Generic.IList<Teacher> teachers)
        {
            Document = _printService.BuildFlowDocument(tabs, teachers);
        }

        private void PrintAll()
        {
            if (Document != null) _printService.PrintFlowDocument(Document);
        }

        private void PrintSelected()
        {
            if (SelectedTab == null) return;
            var doc = _printService.BuildFlowDocument(new[] { SelectedTab });
            _printService.PrintFlowDocument(doc);
        }

        private void ExportPdf()
        {
            if (SelectedTab == null) return;

            try
            {
                // Try to get configured save folder and teacher list from the main view model (preferences)
                var mainVm = Application.Current?.MainWindow?.DataContext as MainViewModel;
                string outputDir = null;
                System.Collections.Generic.IList<Teacher> teachers = null;

                if (mainVm?.Setup != null)
                {
                    if (!string.IsNullOrWhiteSpace(mainVm.Setup.SaveFolder))
                    {
                        try
                        {
                            Directory.CreateDirectory(mainVm.Setup.SaveFolder);
                            outputDir = mainVm.Setup.SaveFolder;
                        }
                        catch
                        {
                            outputDir = null; // fall back to prompting
                        }
                    }

                    // capture teacher list for teacher pages (may be empty)
                    teachers = mainVm.Setup.Teachers?.ToList();
                }

                // If no configured folder, ask the user for a file location (backwards compatible)
                if (string.IsNullOrWhiteSpace(outputDir))
                {
                    var defaultName = $"BreakSchedule_{System.DateTime.Today:yyyy-MM-dd}.pdf";
                    var dlg = new SaveFileDialog
                    {
                        Title = "Save Schedule as PDF",
                        FileName = defaultName,
                        Filter = "PDF Document (*.pdf)|*.pdf",
                        AddExtension = true,
                        OverwritePrompt = true
                    };
                    if (dlg.ShowDialog() != true) return;
                    outputDir = Path.GetDirectoryName(dlg.FileName)
                                ?? System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
                }

                var staffNames = (teachers != null && teachers.Count > 0)
                                 ? string.Join(", ", teachers.Select(t => t.Name))
                                 : "";

                // Use the PrintService overload that can include teacher pages
                var savedPath = _printService.SaveScheduleAsPdf(
                    new[] { SelectedTab },
                    teachers,
                    outputDir,
                    staffNames,
                    appVersion: "1.0");

                MessageBox.Show($"Saved to:\n{savedPath}", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("Failed to export PDF:\n" + ex.Message, "Export", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}