using System.Linq;
using System.Windows;
using System.Windows.Documents;
using Microsoft.Win32;
using ScheduleApp.Infrastructure;
using ScheduleApp.Services;
using ScheduleApp.Models; // NEW

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

            var outputDir = System.IO.Path.GetDirectoryName(dlg.FileName)
                             ?? System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);

            _printService.SaveScheduleAsPdf(new[] { SelectedTab }, outputDir, staffNames: "", appVersion: "1.0");

            MessageBox.Show("PDF saved.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}