using System;
using System.Xml;
using System.Xml.Serialization;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ScheduleApp.Models
{
    public class Teacher : INotifyPropertyChanged
    {
        private string _roomNumber;
        public string RoomNumber { get => _roomNumber; set { if (_roomNumber != value) { _roomNumber = value; Notify(); } } }

        private string _name;
        public string Name { get => _name; set { if (_name != value) { _name = value; Notify(); } } }

        private TimeSpan _start;
        [XmlIgnore]
        public TimeSpan Start { get => _start; set { if (_start != value) { _start = value; Notify(); } } }

        private TimeSpan _end;
        [XmlIgnore]
        public TimeSpan End { get => _end; set { if (_end != value) { _end = value; Notify(); } } }

        [XmlElement("Start")]
        public string StartXml
        {
            get => XmlConvert.ToString(Start);
            set => Start = string.IsNullOrEmpty(value) ? TimeSpan.Zero : XmlConvert.ToTimeSpan(value);
        }

        [XmlElement("End")]
        public string EndXml
        {
            get => XmlConvert.ToString(End);
            set => End = string.IsNullOrEmpty(value) ? TimeSpan.Zero : XmlConvert.ToTimeSpan(value);
        }

        public double ShiftHours => Math.Max(0, (End - Start).TotalHours);
        public bool LunchRequired => ShiftHours > 5.0;

        // Tag placeholder padding rows (not persisted)
        private bool _isPlaceholder;
        [XmlIgnore]
        public bool IsPlaceholder
        {
            get => _isPlaceholder;
            set { if (_isPlaceholder != value) { _isPlaceholder = value; Notify(); } }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void Notify([CallerMemberName] string propName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}