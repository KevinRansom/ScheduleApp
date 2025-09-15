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

        // New: display-only teacher list (contains synthetic "Unscheduled Breaks" entry at index 0)
        public ObservableCollection<Teacher> DisplayTeachers { get; } = new ObservableCollection<Teacher>();

        public ObservableCollection<string> Tabs { get; } = new ObservableCollection<string> { "Team Lineup", "Schedule View", "Print Preview" };

        private int _selectedTabIndex;
        public int SelectedTabIndex { get { return _selectedTabIndex; } set { _selectedTabIndex = value; Raise(); } }

        private readonly SchedulerService _scheduler = new SchedulerService();
        private readonly PrintService _printService = new PrintService();

        public RelayCommand GenerateScheduleCommand { get; }
        public RelayCommand SaveScheduleCommand { get; }
        public RelayCommand SaveSetupCommand { get; }
        public RelayCommand LoadSetupCommand { get; }

        private readonly string _defaultSetupPath;

        // Guard to prevent re-entrant GenerateSchedule calls
        private bool _isGenerating;

        public MainViewModel()
        {
            // existing initialization...
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

            // keep a user-invoked save that shows feedback
            SaveSetupCommand = new RelayCommand(() => SaveSetupDefault());
            LoadSetupCommand = new RelayCommand(LoadSetupDefault);

            LoadSetupDefault();

            // Populate DisplayTeachers initially so UI shows Unscheduled immediately
            UpdateDisplayTeachers();

            // Run schedule generation on app start: after loading setup and before any save action.
            GenerateSchedule();
        }

        private void GenerateSchedule()
        {
            // Prevent reentry (e.g. tab selection handlers triggering GenerateSchedule while it is already running).
            if (_isGenerating) return;
            _isGenerating = true;

            try
            {
                try
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

                    // Build tabs: preserve user support ordering first, then any extra supports,
                    // and append Unscheduled (displayed as "Unscheduled Breaks") at the end.
                    var tabsList = new List<SupportTabViewModel>();

                    // preserve Setup.Supports order (use names as provided)
                    var supportOrder = Setup.Supports.Select(s => s.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
                    foreach (var sname in supportOrder)
                    {
                        if (assigned.TryGetValue(sname, out var list))
                        {
                            tabsList.Add(new SupportTabViewModel
                            {
                                SupportName = sname,
                                Tasks = list.OrderBy(t => t.Start).ToList()
                            });
                        }
                        else
                        {
                            tabsList.Add(new SupportTabViewModel
                            {
                                SupportName = sname,
                                Tasks = new List<CoverageTask>()
                            });
                        }
                    }

                    // add any remaining assigned keys not in setup (except "Unscheduled" which we handle last)
                    var extras = assigned.Keys
                        .Where(k => !supportOrder.Contains(k, StringComparer.OrdinalIgnoreCase))
                        .Where(k => !string.Equals(k, "Unscheduled", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(k => k);
                    foreach (var key in extras)
                    {
                        tabsList.Add(new SupportTabViewModel
                        {
                            SupportName = key,
                            Tasks = assigned[key].OrderBy(t => t.Start).ToList()
                        });
                    }

                    // Finally append Unscheduled as "Unscheduled Breaks" if present
                    if (assigned.TryGetValue("Unscheduled", out var unschedList) && unschedList != null)
                    {
                        var displayName = "Unscheduled Breaks";
                        // Update tasks to use the display name so schedule rows match the tab label
                        foreach (var t in unschedList)
                            t.SupportName = displayName;

                        tabsList.Add(new SupportTabViewModel
                        {
                            SupportName = displayName,
                            Tasks = unschedList.OrderBy(t => t.Start).ToList()
                        });
                    }

                    var tabs = tabsList.ToArray();

                    Schedule.LoadTabs(tabs);

                    // NEW: update the left-hand support entries list used by the By Support list
                    Schedule.UpdateSupportEntries(tabs);

                    // NEW: include teacher pages in the preview document
                    PrintPreview.RefreshDocument(tabs, Setup.Teachers.ToList());

                    var allAssignedTasks = assigned.Values.SelectMany(x => x).ToList();
                    Schedule.LoadTeacherSchedules(DateTime.Today, Setup.Teachers.ToList(), allAssignedTasks);

                    // Update the display-only teacher list so UI shows "Unscheduled Breaks" first
                    UpdateDisplayTeachers();

                    // Ensure the view model keeps Schedule View selected after generation.
                    // Set to 0 because Schedule View is the first top-level tab in MainWindow.xaml
                    SelectedTabIndex = 0;

                    SaveScheduleCommand.RaiseCanExecuteChanged();
                }
                catch (Exception ex)
                {
                    // Surface the exception so we can see why schedule generation fails
                    MessageBox.Show("Failed to generate schedule:\n" + ex.Message, "Generate Schedule Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                _isGenerating = false;
            }
        }

        // New helper: populate DisplayTeachers with a synthetic Unscheduled entry first,
        // then the real Setup.Teachers (do not mutate Setup.Teachers).
        private void UpdateDisplayTeachers()
        {
            DisplayTeachers.Clear();

            var unsched = new Teacher
            {
                Name = "Unscheduled Breaks",
                RoomNumber = "---",
                Start = TimeSpan.Zero,
                End = TimeSpan.Zero
            };

            DisplayTeachers.Add(unsched);

            // Append real teachers, skipping any real entry that already has that name
            foreach (var t in Setup.Teachers)
            {
                if (t == null) continue;
                if (string.Equals(t.Name, unsched.Name, StringComparison.OrdinalIgnoreCase)) continue;
                DisplayTeachers.Add(t);
            }

            // Notify if needed
            Raise(nameof(DisplayTeachers));
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

        // Save as PDF (PDFsharp): use configured folder if present; otherwise prompt.
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
                string outputDir = null;

                // Prefer user-configured folder
                if (!string.IsNullOrWhiteSpace(Setup.SaveFolder))
                {
                    outputDir = Setup.SaveFolder;
                    try
                    {
                        System.IO.Directory.CreateDirectory(outputDir);
                    }
                    catch
                    {
                        outputDir = null; // fall back to dialog
                    }
                }

                if (string.IsNullOrWhiteSpace(outputDir))
                {
                    var dlg = new Microsoft.Win32.SaveFileDialog
                    {
                        Title = "Save Schedule as PDF",
                        FileName = defaultName,
                        Filter = "PDF Document (*.pdf)|*.pdf",
                        AddExtension = true,
                        OverwritePrompt = true
                    };
                    if (dlg.ShowDialog() != true) return;
                    outputDir = System.IO.Path.GetDirectoryName(dlg.FileName)
                               ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                }

                var staffNames = string.Join(", ", Setup.Teachers.Select(t => t.Name));

                var savedPath = _printService.SaveScheduleAsPdf(
                    tabsList,
                    Setup.Teachers.ToList(),
                    outputDir,
                    staffNames,
                    appVersion: "1.0");

                MessageBox.Show($"Saved to:\n{savedPath}", "Save PDF", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to save to PDF:\n" + ex.Message, "Save", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // NEW: publicly callable silent save (used by auto-save)
        public void SaveSetupSilent()
        {
            SaveSetupDefault();
        }

        // NEW/UPDATED: Save entire Setup (Teachers, Supports, Preferences) to default file
        // now accepts an optional flag to control user feedback
        private void SaveSetupDefault()
        {
            try
            {
                var data = new SetupData
                {
                    Teachers = Setup.Teachers?.ToList() ?? new List<Teacher>(),
                    Supports = Setup.Supports?.ToList() ?? new List<Support>(),
                    Preferences = Setup.Preferences?.ToList() ?? new List<RoomPreference>(),
                    SchoolName = Setup.SchoolName,
                    SchoolAddress = Setup.SchoolAddress,
                    SchoolPhone = Setup.SchoolPhone,
                    SaveFolder = Setup.SaveFolder // <-- add
                };

                var serializer = new XmlSerializer(typeof(SetupData));
                using (var fs = File.Create(_defaultSetupPath))
                {
                    serializer.Serialize(fs, data);
                }

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

                    // restore institution details
                    Setup.SchoolName = data.SchoolName;
                    Setup.SchoolAddress = data.SchoolAddress;
                    Setup.SchoolPhone = data.SchoolPhone;
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