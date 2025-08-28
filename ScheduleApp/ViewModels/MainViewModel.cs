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

        public RelayCommand GenerateScheduleCommand { get; }
        public RelayCommand SaveScheduleCommand { get; }
        public RelayCommand SaveSetupCommand { get; }   // NEW
        public RelayCommand LoadSetupCommand { get; }   // NEW

        private readonly string _defaultSetupPath;       // NEW

        public MainViewModel()
        {
            // Populate 24-hour quarter increments
            for (int h = 0; h < 24; h++)
            {
                QuarterHours.Add(new TimeSpan(h, 0, 0));
                QuarterHours.Add(new TimeSpan(h, 15, 0));
                QuarterHours.Add(new TimeSpan(h, 30, 0));
                QuarterHours.Add(new TimeSpan(h, 45, 0));
            }

            // Compute default setup path and ensure directory
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, "ScheduleApp");
            Directory.CreateDirectory(dir);
            _defaultSetupPath = Path.Combine(dir, "ScheduleApp.settings");

            GenerateScheduleCommand = new RelayCommand(GenerateSchedule);
            SaveScheduleCommand = new RelayCommand(SaveSchedule, ScheduleHasData);

            SaveSetupCommand = new RelayCommand(SaveSetupDefault); // NEW
            LoadSetupCommand = new RelayCommand(LoadSetupDefault); // NEW

            // Auto-load default file on startup (if present)
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

            // Inject support names for self-care and idle insertion
            foreach (var kvp in assigned.ToList())
            {
                foreach (var t in kvp.Value)
                {
                    t.SupportName = kvp.Key;
                }
            }

            _scheduler.ScheduleSupportSelfCare(day, assigned);

            // Build tabs
            var tabs = assigned.Keys.OrderBy(k => k).Select(name =>
            {
                var vm = new SupportTabViewModel { SupportName = name, Tasks = assigned[name].OrderBy(t => t.Start).ToList() };
                return vm;
            }).ToArray();

            Schedule.LoadTabs(tabs);
            PrintPreview.RefreshDocument(tabs);

            SelectedTabIndex = 1; // switch to Schedule View

            // Enable Save when data exists
            SaveScheduleCommand.RaiseCanExecuteChanged();
        }

        private bool ScheduleHasData()
        {
            try
            {
                var tabsProp = Schedule?.GetType().GetProperty("SupportTabs");
                var tabs = tabsProp?.GetValue(Schedule) as IEnumerable;
                if (tabs == null) return false;
                foreach (var _ in tabs) return true; // has at least one
                return false;
            }
            catch { return false; }
        }

        private void SaveSchedule()
        {
            var dlg = new SaveFileDialog
            {
                Title = "Save Schedule",
                FileName = $"Schedule_{DateTime.Today:yyyyMMdd}.csv",
                Filter = "CSV files (*.csv)|*.csv|Text files (*.txt)|*.txt",
                AddExtension = true,
                OverwritePrompt = true
            };

            if (dlg.ShowDialog() != true) return;

            var sb = new StringBuilder();
            sb.AppendLine("Support,Task,Duration,Teacher,Room,Start,Kind");

            var tabsProp = Schedule.GetType().GetProperty("SupportTabs");
            var tabs = (IEnumerable)tabsProp.GetValue(Schedule);

            foreach (var tab in tabs)
            {
                var supportName = ToStringSafe(GetProp(tab, "SupportName"));

                var tasksEnum = (IEnumerable)GetProp(tab, "Tasks");
                if (tasksEnum == null) continue;

                foreach (var task in tasksEnum)
                {
                    var taskName   = ToStringSafe(GetProp(task, "TaskName"));
                    var duration   = ToStringSafe(GetProp(task, "DurationText"));
                    var teacher    = ToStringSafe(GetProp(task, "TeacherDisplay"));
                    var room       = ToStringSafe(GetProp(task, "RoomDisplay"));
                    var start      = ToStringSafe(GetProp(task, "StartText"));
                    var kind       = ToStringSafe(GetProp(task, "Kind"));

                    sb.AppendLine(string.Join(",",
                        Csv(supportName),
                        Csv(taskName),
                        Csv(duration),
                        Csv(teacher),
                        Csv(room),
                        Csv(start),
                        Csv(kind)));
                }
            }

            File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
            MessageBox.Show("Schedule saved successfully.", "Save Schedule", MessageBoxButton.OK, MessageBoxImage.Information);
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