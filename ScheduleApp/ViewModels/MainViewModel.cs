using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using ScheduleApp.Infrastructure;
using ScheduleApp.Models;
using ScheduleApp.Services;
using System.Xml.Serialization;

namespace ScheduleApp.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        public ObservableCollection<TimeSpan> QuarterHours { get; } = new ObservableCollection<TimeSpan>();

        public SetupViewModel Setup { get; } = new SetupViewModel();
        public ScheduleViewModel Schedule { get; } = new ScheduleViewModel();
        public PrintPreviewViewModel PrintPreview { get; } = new PrintPreviewViewModel();

        public ObservableCollection<string> Tabs { get; } = new ObservableCollection<string> { "Setup", "Schedule View", "Print Preview" };

        private int _selectedTabIndex;
        public int SelectedTabIndex { get { return _selectedTabIndex; } set { _selectedTabIndex = value; Raise(); } }

        private readonly SchedulerService _scheduler = new SchedulerService();
        private readonly PrintService _printService = new PrintService();

        public RelayCommand GenerateScheduleCommand { get; }
        public RelayCommand SaveScheduleCommand { get; }
        public RelayCommand SaveSetupCommand { get; }
        public RelayCommand LoadSetupCommand { get; }

        private readonly string _defaultSetupPath;

        public MainViewModel()
        {
            for (int h = 0; h < 24; h++)
            {
                QuarterHours.Add(new TimeSpan(h, 0, 0));
                QuarterHours.Add(new TimeSpan(h, 15, 0));
                QuarterHours.Add(new TimeSpan(h, 30, 0));
                QuarterHours.Add(new TimeSpan(h, 45, 0));
            }

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, "ScheduleApp");
            Directory.CreateDirectory(dir);
            _defaultSetupPath = Path.Combine(dir, "ScheduleApp.settings");

            GenerateScheduleCommand = new RelayCommand(GenerateSchedule);
            SaveScheduleCommand = new RelayCommand(SaveSchedule, ScheduleHasData);

            SaveSetupCommand = new RelayCommand(SaveSetupDefault);
            LoadSetupCommand = new RelayCommand(LoadSetupDefault);

            LoadSetupDefault();
        }

        private void GenerateSchedule()
        {
            var day = new DayContext
            {
                Date = DateTime.Today,
                Teachers = Setup.Teachers.ToList(),
                Supports = Setup.Supports.ToList(),
                Preferences = Setup.Preferences.ToList()
            };

            var teacherTasks = _scheduler.GenerateTeacherCoverageTasks(day);
            var assigned = _scheduler.AssignSupportToTeacherTasks(day, teacherTasks);

            foreach (var kvp in assigned.ToList())
            {
                foreach (var t in kvp.Value)
                {
                    t.SupportName = kvp.Key;
                }
            }

            _scheduler.ScheduleSupportSelfCare(day, assigned);

            var tabs = assigned.Keys.OrderBy(k => k).Select(name =>
            {
                var vm = new SupportTabViewModel { SupportName = name, Tasks = assigned[name].OrderBy(t => t.Start).ToList() };
                return vm;
            }).ToArray();

            Schedule.LoadTabs(tabs);
            PrintPreview.RefreshDocument(tabs);

            SelectedTabIndex = 1;

            SaveScheduleCommand.RaiseCanExecuteChanged();
        }

        private bool ScheduleHasData()
        {
            try
            {
                var tabsProp = Schedule?.GetType().GetProperty("SupportTabs");
                var tabs = tabsProp?.GetValue(Schedule) as IEnumerable;
                if (tabs == null) return false;
                foreach (var _ in tabs) return true;
                return false;
            }
            catch { return false; }
        }

        // Save as PDF (PDFsharp): choose folder via SaveFileDialog, then generate file with timestamped name.
        private void SaveSchedule()
        {
            try
            {
                var tabsList = Schedule.SupportTabs?.ToArray();
                if (tabsList == null || tabsList.Length == 0)
                {
                    MessageBox.Show("No schedules to print.", "Save", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var defaultName = $"BreakSchedule_{DateTime.Today:yyyy-MM-dd}.pdf";
                var dlg = new SaveFileDialog
                {
                    Title = "Save Schedule as PDF",
                    FileName = defaultName,
                    Filter = "PDF Document (*.pdf)|*.pdf",
                    AddExtension = true,
                    OverwritePrompt = true
                };
                if (dlg.ShowDialog() != true) return;

                var outputDir = Path.GetDirectoryName(dlg.FileName)
                                 ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                // Optional: pass staff names if available
                var staffNames = string.Join(", ", Setup.Teachers.Select(t => t.Name));
                var savedPath = _printService.SaveScheduleAsPdf(tabsList, outputDir, staffNames, appVersion: "1.0");

                MessageBox.Show($"Saved to:\n{savedPath}", "Save PDF", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to save to PDF:\n" + ex.Message, "Save", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // NEW: Save entire Setup (Teachers, Supports, Preferences) to default file
        private void SaveSetupDefault()
        {
            try
            {
                var data = new SetupData
                {
                    Teachers = Setup.Teachers?.ToList() ?? new List<Teacher>(),
                    Supports = Setup.Supports?.ToList() ?? new List<Support>(),
                    Preferences = Setup.Preferences?.ToList() ?? new List<RoomPreference>()
                };

                var serializer = new XmlSerializer(typeof(SetupData));
                using (var fs = File.Create(_defaultSetupPath))
                {
                    serializer.Serialize(fs, data);
                }

                MessageBox.Show("Setup saved.", "Save Setup", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to save setup:\n" + ex.Message, "Save Setup", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // NEW: Load entire Setup from default file (if it exists)
        private void LoadSetupDefault()
        {
            try
            {
                if (!File.Exists(_defaultSetupPath)) return;

                var serializer = new XmlSerializer(typeof(SetupData));
                using (var fs = File.OpenRead(_defaultSetupPath))
                {
                    var data = (SetupData)serializer.Deserialize(fs);

                    Setup.Teachers.Clear();
                    Setup.Supports.Clear();
                    Setup.Preferences.Clear();

                    if (data.Teachers != null) foreach (var t in data.Teachers) Setup.Teachers.Add(t);
                    if (data.Supports != null) foreach (var s in data.Supports) Setup.Supports.Add(s);
                    if (data.Preferences != null) foreach (var p in data.Preferences) Setup.Preferences.Add(p);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load setup:\n" + ex.Message, "Load Setup", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static object GetProp(object obj, string name) =>
            obj?.GetType().GetProperty(name)?.GetValue(obj);

        private static string ToStringSafe(object value) =>
            value == null ? string.Empty : value.ToString();

        private static string Csv(string s)
        {
            if (s == null) return "\"\"";
            var escaped = s.Replace("\"", "\"\"");
            return $"\"{escaped}\"";
        }
    }
}