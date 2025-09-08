using System.Collections.ObjectModel;
using ScheduleApp.Infrastructure;
using ScheduleApp.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace ScheduleApp.ViewModels
{
    public class SetupViewModel : BaseViewModel
    {
        public ObservableCollection<Teacher> Teachers { get; } = new ObservableCollection<Teacher>();
        public ObservableCollection<Support> Supports { get; } = new ObservableCollection<Support>();
        public ObservableCollection<RoomPreference> Preferences { get; } = new ObservableCollection<RoomPreference>();

        // Institution details (bindable)
        private string _schoolName;
        public string SchoolName { get { return _schoolName; } set { _schoolName = value; Raise(); } }

        private string _schoolAddress;
        public string SchoolAddress { get { return _schoolAddress; } set { _schoolAddress = value; Raise(); } }

        private string _schoolPhone;
        public string SchoolPhone { get { return _schoolPhone; } set { _schoolPhone = value; Raise(); } }

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

        // Keyboard move commands
        public RelayCommand MoveTeacherUpCommand { get; }
        public RelayCommand MoveTeacherDownCommand { get; }
        public RelayCommand MoveSupportUpCommand { get; }
        public RelayCommand MoveSupportDownCommand { get; }
        public RelayCommand MovePreferenceUpCommand { get; }
        public RelayCommand MovePreferenceDownCommand { get; }

        public SetupViewModel()
        {
            AddTeacherCommand = new RelayCommand(AddTeacher);
            RemoveTeacherCommand = new RelayCommand<IList>(RemoveTeachers, sel => sel != null && sel.Count > 0);
            AddSupportCommand = new RelayCommand(AddSupport);
            RemoveSupportCommand = new RelayCommand<IList>(RemoveSupports, sel => sel != null && sel.Count > 0);
            AddPreferenceCommand = new RelayCommand(AddPreference);
            RemovePreferenceCommand = new RelayCommand<IList>(RemovePreferences, sel => sel != null && sel.Count > 0);

            MoveTeacherUpCommand = new RelayCommand(MoveTeacherUp, () => SelectedTeacher != null && Teachers.IndexOf(SelectedTeacher) > 0);
            MoveTeacherDownCommand = new RelayCommand(MoveTeacherDown, () => SelectedTeacher != null && Teachers.IndexOf(SelectedTeacher) >= 0 && Teachers.IndexOf(SelectedTeacher) < Teachers.Count - 1);

            MoveSupportUpCommand = new RelayCommand(MoveSupportUp, () => SelectedSupport != null && Supports.IndexOf(SelectedSupport) > 0);
            MoveSupportDownCommand = new RelayCommand(MoveSupportDown, () => SelectedSupport != null && Supports.IndexOf(SelectedSupport) >= 0 && Supports.IndexOf(SelectedSupport) < Supports.Count - 1);

            MovePreferenceUpCommand = new RelayCommand(MovePreferenceUp, () => SelectedPreference != null && Preferences.IndexOf(SelectedPreference) > 0);
            MovePreferenceDownCommand = new RelayCommand(MovePreferenceDown, () => SelectedPreference != null && Preferences.IndexOf(SelectedPreference) >= 0 && Preferences.IndexOf(SelectedPreference) < Preferences.Count - 1);
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

        #region keyboard move implementations

        private void MoveTeacherUp()
        {
            if (SelectedTeacher == null) return;
            var idx = Teachers.IndexOf(SelectedTeacher);
            if (idx <= 0) return;

            var target = Teachers[idx - 1];
            if (HasEmptyNameOrStart(target)) return;

            Teachers.Move(idx, idx - 1);
            SelectedTeacher = Teachers[idx - 1];
            RaiseMoveCommandsCanExecute();
        }

        private void MoveTeacherDown()
        {
            if (SelectedTeacher == null) return;
            var idx = Teachers.IndexOf(SelectedTeacher);
            if (idx < 0 || idx >= Teachers.Count - 1) return;

            var target = Teachers[idx + 1];
            if (HasEmptyNameOrStart(target)) return;

            Teachers.Move(idx, idx + 1);
            SelectedTeacher = Teachers[idx + 1];
            RaiseMoveCommandsCanExecute();
        }

        private void MoveSupportUp()
        {
            if (SelectedSupport == null) return;
            var idx = Supports.IndexOf(SelectedSupport);
            if (idx <= 0) return;

            var target = Supports[idx - 1];
            if (HasEmptyNameOrStart(target)) return;

            Supports.Move(idx, idx - 1);
            SelectedSupport = Supports[idx - 1];
            RaiseMoveCommandsCanExecute();
        }

        private void MoveSupportDown()
        {
            if (SelectedSupport == null) return;
            var idx = Supports.IndexOf(SelectedSupport);
            if (idx < 0 || idx >= Supports.Count - 1) return;

            var target = Supports[idx + 1];
            if (HasEmptyNameOrStart(target)) return;

            Supports.Move(idx, idx + 1);
            SelectedSupport = Supports[idx + 1];
            RaiseMoveCommandsCanExecute();
        }

        private void MovePreferenceUp()
        {
            if (SelectedPreference == null) return;
            var idx = Preferences.IndexOf(SelectedPreference);
            if (idx <= 0) return;

            var target = Preferences[idx - 1];
            // Preference: treat PreferredSupportName as name field if present
            if (HasEmptyNameOrStart(target)) return;

            Preferences.Move(idx, idx - 1);
            SelectedPreference = Preferences[idx - 1];
            RaiseMoveCommandsCanExecute();
        }

        private void MovePreferenceDown()
        {
            if (SelectedPreference == null) return;
            var idx = Preferences.IndexOf(SelectedPreference);
            if (idx < 0 || idx >= Preferences.Count - 1) return;

            var target = Preferences[idx + 1];
            if (HasEmptyNameOrStart(target)) return;

            Preferences.Move(idx, idx + 1);
            SelectedPreference = Preferences[idx + 1];
            RaiseMoveCommandsCanExecute();
        }

        private bool HasEmptyNameOrStart(object item)
        {
            if (item == null) return false;
            var nameProp = item.GetType().GetProperty("TeacherName") ?? item.GetType().GetProperty("Name") ?? item.GetType().GetProperty("PreferredSupportName");
            if (nameProp != null)
            {
                var val = nameProp.GetValue(item) as string;
                if (string.IsNullOrWhiteSpace(val)) return true;
            }

            var startProp = item.GetType().GetProperty("Start") ?? item.GetType().GetProperty("StartTime");
            if (startProp != null)
            {
                var sval = startProp.GetValue(item);
                if (sval is TimeSpan ts)
                {
                    if (ts == TimeSpan.Zero) return true;
                }
                else if (sval == null) return true;
            }
            return false;
        }

        private void RaiseMoveCommandsCanExecute()
        {
            MoveTeacherUpCommand.RaiseCanExecuteChanged();
            MoveTeacherDownCommand.RaiseCanExecuteChanged();
            MoveSupportUpCommand.RaiseCanExecuteChanged();
            MoveSupportDownCommand.RaiseCanExecuteChanged();
            MovePreferenceUpCommand.RaiseCanExecuteChanged();
            MovePreferenceDownCommand.RaiseCanExecuteChanged();
        }

        #endregion
    }
}