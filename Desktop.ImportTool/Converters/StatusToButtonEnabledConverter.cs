using System;
using System.Globalization;
using System.Windows.Data;
using Desktop.ImportTool.Models;

namespace Desktop.ImportTool.Converters
{
    public class StatusToButtonEnabledConverter : IValueConverter
    {
        // parameter: "Pause" or "Run"
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TaskStatus status && parameter is string buttonType)
            {
                switch (buttonType)
                {
                    case "Pause":
                        return status != TaskStatus.Paused;
                    case "Run":
                        return status == TaskStatus.Paused;
                }
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
