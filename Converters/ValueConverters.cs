using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace JoSystem.Converters
{
    // 根据是否是文件夹返回不同的图标
    public class IconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // \uE8B7: Folder (Outline), \uE8D5: FolderHorizontal, \uE838: FolderOpen
            // Using \uE838 (FolderOpen) or \uE8D5 for better look
            return (value is bool isFolder && isFolder) ? "\uE8D5" : "\uE7C3"; // 文件夹 : 文件
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    // 根据服务器运行状态返回小圆点颜色
    public class StatusColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool isRunning && isRunning) ? new SolidColorBrush(Color.FromRgb(16, 124, 16)) : new SolidColorBrush(Color.FromRgb(192, 192, 192));
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    // 判断两个值是否相等
    public class EqualityToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.ToString() == parameter?.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
             return (value is bool b && b) ? parameter : Binding.DoNothing;
        }
    }

    // 布尔值取反
    public class InvertBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b) return !b;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b) return !b;
            return false;
        }
    }

    // bool取反转Visibility (True -> Collapsed, False -> Visible)
    public class InvertedBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b) return System.Windows.Visibility.Collapsed;
            return System.Windows.Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
