using System;
using System.Xml;
using System.Xml.Serialization;

namespace ScheduleApp.Models
{
    public class Support
    {
        public string Name { get; set; }

        [XmlIgnore]
        public TimeSpan Start { get; set; }

        [XmlIgnore]
        public TimeSpan End { get; set; }

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
    }
}