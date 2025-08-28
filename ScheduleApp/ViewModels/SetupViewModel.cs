using System.Collections.ObjectModel;
using ScheduleApp.Infrastructure;
using ScheduleApp.Models;
using System;
using System.Collections;
using System.Linq;

namespace ScheduleApp.ViewModels
{
    public class SetupViewModel : BaseViewModel
    {
        public ObservableCollection<Teacher> Teachers { get; } = new ObservableCollection<Teacher>();
        public ObservableCollection<Support> Supports { get; } = new ObservableCollection<Support>();
        public ObservableCollection<RoomPreference> Preferences { get; } = new ObservableCollection<RoomPreference>();

        private Teacher _selectedTeacher;
        public Teacher SelectedTeacher { get { return _selectedTeacher; } set { _selectedTeacher = value; Raise(); } }

        private Support _selectedSupport;
        public Support SelectedSupport { get { return _selectedSupport; } set { _selectedSupport = value; Raise(); } }

        private RoomPreference _selectedPreference;
        public RoomPreference SelectedPreference { get { return _selectedPreference; } set { _selectedPreference = value; Raise(); } }

        public RelayCommand AddTeacherCommand { get; }
        public RelayCommand<IList> RemoveTeacherCommand { get; }      // changed
        public RelayCommand AddSupportCommand { get; }
        public RelayCommand<IList> RemoveSupportCommand { get; }      // changed
        public RelayCommand AddPreferenceCommand { get; }
        public RelayCommand<IList> RemovePreferenceCommand { get; }   // changed

        public SetupViewModel()
        {
            AddTeacherCommand = new RelayCommand(AddTeacher);
            RemoveTeacherCommand = new RelayCommand<IList>(RemoveTeachers, sel => sel != null && sel.Count > 0);
            AddSupportCommand = new RelayCommand(AddSupport);
            RemoveSupportCommand = new RelayCommand<IList>(RemoveSupports, sel => sel != null && sel.Count > 0);
            AddPreferenceCommand = new RelayCommand(AddPreference);
            RemovePreferenceCommand = new RelayCommand<IList>(RemovePreferences, sel => sel != null && sel.Count > 0);

            // Seed data removed
        }

        private void AddTeacher()
        {
            Teachers.Add(new Teacher { RoomNumber = "", Name = "", Start = TimeSpan.FromHours(8), End = TimeSpan.FromHours(15) });
        }

        private void RemoveTeachers(IList selected)
        {
            var toRemove = selected.Cast<Teacher>().ToList();
            foreach (var t in toRemove) Teachers.Remove(t);
        }

        private void AddSupport()
        {
            Supports.Add(new Support { Name = "", Start = TimeSpan.FromHours(8), End = TimeSpan.FromHours(16) });
        }

        private void RemoveSupports(IList selected)
        {
            var toRemove = selected.Cast<Support>().ToList();
            foreach (var s in toRemove) Supports.Remove(s);
        }

        private void AddPreference()
        {
            Preferences.Add(new RoomPreference { RoomNumber = "", PreferredSupportName = "" });
        }

        private void RemovePreferences(IList selected)
        {
            var toRemove = selected.Cast<RoomPreference>().ToList();
            foreach (var p in toRemove) Preferences.Remove(p);
        }
    }
}