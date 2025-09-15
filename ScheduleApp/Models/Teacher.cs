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

        // XML proxies to persist Start/End as xsd:duration
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

        public double ShiftHours
        {
            get
            {
                var span = End - Start;
                return Math.Max(0, span.TotalHours);
            }
        }

        public bool LunchRequired
        {
            get { return ShiftHours > 5.0; }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void Notify([CallerMemberName] string propName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}