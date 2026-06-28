using System;
using Windows.UI.Xaml.Data;
using WeatherAppRT2._0.Models;

namespace WeatherAppRT2._0.Converters
{
    /// <summary>温度值 → "32°" 格式</summary>
    public class TemperatureConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is double)
                return string.Format("{0}°", (int)(double)value);
            return "—";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>时间 → "15:00" 格式</summary>
    public class HourTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is DateTime)
            {
                var dt = (DateTime)value;
                return dt.ToString("HH:mm");
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>天气图标代码 → Emoji</summary>
    public class WeatherIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var code = value as string;
            if (code != null)
                return WeatherIconMapper.GetEmoji(code);
            return "🌡";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>Bool → Visibility</summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool)
                return (bool)value
                    ? Windows.UI.Xaml.Visibility.Visible
                    : Windows.UI.Xaml.Visibility.Collapsed;
            return Windows.UI.Xaml.Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>反向 Bool → Visibility</summary>
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool)
                return (bool)value
                    ? Windows.UI.Xaml.Visibility.Collapsed
                    : Windows.UI.Xaml.Visibility.Visible;
            return Windows.UI.Xaml.Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>降雨概率 → 显示文案 "降雨概率: 15:00 最高 60%"</summary>
    public class RainInfoConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            // value 传入 RainInfo 对象
            var info = value as RainInfo;
            if (info == null || info.MaxPop <= 0)
                return "";
            return string.Format("降雨概率: {0} 最高 {1}%", info.MaxTime, info.MaxPop);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>降雨信息辅助类</summary>
    public class RainInfo
    {
        public int MaxPop { get; set; }
        public string MaxTime { get; set; }
    }

    /// <summary>风力等级+风向 → "北风 3级"</summary>
    public class WindInfoConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var weather = value as CurrentWeather;
            if (weather == null) return "—";
            return string.Format("{0} {1}级", weather.WindDirection, weather.WindScale);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>DateTime → "N分钟前"</summary>
    public class RelativeTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is DateTime)
            {
                var diff = DateTime.Now - (DateTime)value;
                if (diff.TotalMinutes < 1) return "刚刚更新";
                if (diff.TotalMinutes < 60) return string.Format("{0}分钟前更新", (int)diff.TotalMinutes);
                if (diff.TotalHours < 24) return string.Format("{0}小时前更新", (int)diff.TotalHours);
                return string.Format("{0}天前更新", (int)diff.TotalDays);
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>降雨概率 → Visibility（0时隐藏）</summary>
    public class PopVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is int)
                return (int)value > 0
                    ? Windows.UI.Xaml.Visibility.Visible
                    : Windows.UI.Xaml.Visibility.Collapsed;
            return Windows.UI.Xaml.Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>double 比例 (0~1) → GridLength.Star</summary>
    public class RatioConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is double)
            {
                double ratio = (double)value;
                if (ratio <= 0) ratio = 0.01;
                return new Windows.UI.Xaml.GridLength(ratio, Windows.UI.Xaml.GridUnitType.Star);
            }
            return new Windows.UI.Xaml.GridLength(0.01, Windows.UI.Xaml.GridUnitType.Star);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
