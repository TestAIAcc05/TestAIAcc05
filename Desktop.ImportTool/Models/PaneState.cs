using System;
using System.Xml.Serialization;

namespace Desktop.ImportTool.Models
{
    [Serializable]
    public class PaneState
    {
        [XmlAttribute]
        public string Id { get; set; }

        // true = visible/open, false = closed/hidden
        public bool IsVisible { get; set; }
    }
}