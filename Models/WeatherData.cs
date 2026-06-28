using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace WeatherAppRT2._0.Models
{
    [DataContract]
    public class WeatherData
    {
        [DataMember] public CityInfo City { get; set; }
        [DataMember] public CurrentWeather Current { get; set; }
        [DataMember] public List<HourlyForecast> Hourly { get; set; }
        [DataMember] public List<DailyForecast> Daily { get; set; }
        [DataMember] public DateTime FetchedAt { get; set; }
    }

    [DataContract]
    public class CityInfo
    {
        [DataMember] public string Id { get; set; }
        [DataMember] public string Name { get; set; }
        [DataMember] public string Adm1 { get; set; }           // 省/州
        [DataMember] public string Country { get; set; }
        [DataMember] public double Latitude { get; set; }
        [DataMember] public double Longitude { get; set; }
        [DataMember] public bool IsCurrentLocation { get; set; } // GPS 定位城市
    }

    [DataContract]
    public class CurrentWeather
    {
        [DataMember] public double Temperature { get; set; }
        [DataMember] public double FeelsLike { get; set; }
        [DataMember] public string WeatherIcon { get; set; }     // QWeather icon code
        [DataMember] public string WeatherDesc { get; set; }
        [DataMember] public int Humidity { get; set; }
        [DataMember] public string WindDirection { get; set; }
        [DataMember] public double WindSpeed { get; set; }
        [DataMember] public int WindScale { get; set; }
        [DataMember] public int Pressure { get; set; }
        [DataMember] public double Visibility { get; set; }
        [DataMember] public string Sunrise { get; set; }
        [DataMember] public string Sunset { get; set; }
        [DataMember] public int UvIndex { get; set; }
    }

    [DataContract]
    public class HourlyForecast
    {
        [DataMember] public DateTime Time { get; set; }
        [DataMember] public double Temperature { get; set; }
        [DataMember] public string WeatherIcon { get; set; }
        [DataMember] public int PrecipitationProbability { get; set; }
        [DataMember] public string WindDirection { get; set; }
        [DataMember] public int WindScale { get; set; }
    }

    [DataContract]
    public class DailyForecast
    {
        [DataMember] public DateTime Date { get; set; }
        [DataMember] public string DayOfWeek { get; set; }
        [DataMember] public double TempMax { get; set; }
        [DataMember] public double TempMin { get; set; }
        [DataMember] public string IconDay { get; set; }
        [DataMember] public string IconNight { get; set; }
        [DataMember] public int PrecipitationProbability { get; set; }
        [DataMember] public string WeatherDesc { get; set; }
        [DataMember] public string Sunrise { get; set; }
        [DataMember] public string Sunset { get; set; }
        [DataMember] public int UvIndex { get; set; }
        [DataMember] public string WindDirection { get; set; }
        [DataMember] public int WindScale { get; set; }
        [DataMember] public int Humidity { get; set; }

        /// <summary>温度条宽度比例（0~1），由 ViewModel 根据全局范围计算</summary>
        [IgnoreDataMember]
        public double TempBarRatio { get; set; }

        /// <summary>温度条左边距比例（0~1）</summary>
        [IgnoreDataMember]
        public double TempBarLeftRatio { get; set; }
    }

    [DataContract]
    public class CitySearchResult
    {
        [DataMember] public string Id { get; set; }
        [DataMember] public string Name { get; set; }
        [DataMember] public string Adm1 { get; set; }
        [DataMember] public string Country { get; set; }
        [DataMember] public double Latitude { get; set; }
        [DataMember] public double Longitude { get; set; }
        [DataMember] public bool IsSaved { get; set; }  // 已收藏标记
    }

    [DataContract]
    public class AppSettings
    {
        [DataMember] public string DefaultCityId { get; set; }
        [DataMember] public string DefaultCityName { get; set; }
        [DataMember] public List<CityInfo> SavedCities { get; set; }
        [DataMember] public List<CityInfo> SearchHistory { get; set; }  // 搜索历史，最多10条

        // ===== 用户可配置项（首次运行时使用 AppConfig 默认值） =====
        /// <summary>后台刷新间隔（分钟），最小15分钟</summary>
        [DataMember] public int RefreshIntervalMinutes { get; set; } = -1;  // -1 表示未设置，使用 AppConfig 默认值

        /// <summary>是否启用天气变化通知</summary>
        [DataMember] public bool EnableNotifications { get; set; } = true;

        /// <summary>通知最小间隔（分钟），避免频繁打扰</summary>
        [DataMember] public int NotifyMinIntervalMinutes { get; set; } = -1;

        /// <summary>温度变化通知阈值（°C）</summary>
        [DataMember] public double NotifyTempThreshold { get; set; } = -1;

        /// <summary>降水概率通知阈值（%）</summary>
        [DataMember] public int NotifyRainThreshold { get; set; } = -1;

        /// <summary>大风通知阈值（km/h）</summary>
        [DataMember] public double NotifyWindThreshold { get; set; } = -1;

        // ===== 辅助方法：解析实际值（用户设置 > AppConfig 默认值） =====
        public int GetRefreshInterval()
        {
            return RefreshIntervalMinutes > 0 ? RefreshIntervalMinutes : AppConfig.BackgroundRefreshMinutes;
        }

        public int GetNotifyMinInterval()
        {
            return NotifyMinIntervalMinutes > 0 ? NotifyMinIntervalMinutes : AppConfig.NotifyMinIntervalMinutes;
        }

        public double GetNotifyTempThreshold()
        {
            return NotifyTempThreshold >= 0 ? NotifyTempThreshold : AppConfig.NotifyTempChangeThreshold;
        }

        public int GetNotifyRainThreshold()
        {
            return NotifyRainThreshold >= 0 ? NotifyRainThreshold : AppConfig.NotifyRainProbabilityThreshold;
        }

        public double GetNotifyWindThreshold()
        {
            return NotifyWindThreshold >= 0 ? NotifyWindThreshold : AppConfig.NotifyWindSpeedThreshold;
        }

        public static AppSettings GetDefault()
        {
            return new AppSettings
            {
                DefaultCityId = AppConfig.DefaultCityId,
                DefaultCityName = AppConfig.DefaultCityName,
                SavedCities = new List<CityInfo>(),
                SearchHistory = new List<CityInfo>(),
                RefreshIntervalMinutes = -1,
                EnableNotifications = true,
                NotifyMinIntervalMinutes = -1,
                NotifyTempThreshold = -1,
                NotifyRainThreshold = -1,
                NotifyWindThreshold = -1
            };
        }
    }
}
