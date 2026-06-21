using System;
using System.Collections.Generic;

namespace WeatherAppRT2._0.Models
{
    public class WeatherData
    {
        public CityInfo City { get; set; }
        public CurrentWeather Current { get; set; }
        public List<HourlyForecast> Hourly { get; set; }
        public List<DailyForecast> Daily { get; set; }
        public DateTime FetchedAt { get; set; }
    }

    public class CityInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Adm1 { get; set; }           // 省/州
        public string Country { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public bool IsCurrentLocation { get; set; } // GPS 定位城市
    }

    public class CurrentWeather
    {
        public double Temperature { get; set; }
        public double FeelsLike { get; set; }
        public string WeatherIcon { get; set; }     // QWeather icon code
        public string WeatherDesc { get; set; }
        public int Humidity { get; set; }
        public string WindDirection { get; set; }
        public double WindSpeed { get; set; }
        public int WindScale { get; set; }
        public int Pressure { get; set; }
        public double Visibility { get; set; }
        public string Sunrise { get; set; }
        public string Sunset { get; set; }
        public int UvIndex { get; set; }
    }

    public class HourlyForecast
    {
        public DateTime Time { get; set; }
        public double Temperature { get; set; }
        public string WeatherIcon { get; set; }
        public int PrecipitationProbability { get; set; }
        public string WindDirection { get; set; }
        public int WindScale { get; set; }
    }

    public class DailyForecast
    {
        public DateTime Date { get; set; }
        public string DayOfWeek { get; set; }
        public double TempMax { get; set; }
        public double TempMin { get; set; }
        public string IconDay { get; set; }
        public string IconNight { get; set; }
        public int PrecipitationProbability { get; set; }
        public string WeatherDesc { get; set; }
        public string Sunrise { get; set; }
        public string Sunset { get; set; }
        public int UvIndex { get; set; }
        public string WindDirection { get; set; }
        public int WindScale { get; set; }
        public int Humidity { get; set; }
    }

    public class CitySearchResult
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Adm1 { get; set; }
        public string Country { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public bool IsSaved { get; set; }  // 已收藏标记
    }

    public class AppSettings
    {
        public string DefaultCityId { get; set; }
        public string DefaultCityName { get; set; }
        public List<CityInfo> SavedCities { get; set; }

        public static AppSettings GetDefault()
        {
            return new AppSettings
            {
                DefaultCityId = AppConfig.DefaultCityId,
                DefaultCityName = AppConfig.DefaultCityName,
                SavedCities = new List<CityInfo>()
            };
        }
    }
}
