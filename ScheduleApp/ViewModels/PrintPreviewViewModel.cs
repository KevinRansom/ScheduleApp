using System.Linq;
using System.Windows.Documents;
using ScheduleApp.Infrastructure;
using ScheduleApp.Services;

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

        public void RefreshDocument(SupportTabViewModel[] tabs)
        {
            Document = _printService.BuildFlowDocument(tabs);
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
            var doc = _printService.BuildFlowDocument(new[] { SelectedTab });
            _printService.PrintToPdf(doc, SelectedTab.SupportName + ".pdf");
        }
    }
}