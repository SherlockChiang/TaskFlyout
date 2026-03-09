using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Task_Flyout
{
    public class EventMarkerConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // values[0] 获取到的是日期 DateTime
            // values[1] 获取到的是我们存放的 MarkedDates 集合
            if (values.Length >= 2 && values[0] is DateTime currentDay && values[1] is HashSet<DateTime> markedDates)
            {
                return markedDates.Contains(currentDay.Date) ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}