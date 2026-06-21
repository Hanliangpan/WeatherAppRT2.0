namespace WeatherAppRT2._0
{
    public static class AppConfig
    {
        // 使用 Open-Meteo 免费 API，无需 API Key

        // 缓存策略
        public const int CacheMaxAgeMinutes = 15;
        public const int MaxCities = 10;

        // 默认城市（北京）
        public const string DefaultCityName = "北京";
        public const string DefaultCityId = "39.9042,116.4074";  // lat,lon 格式
        public const double DefaultLatitude = 39.9042;
        public const double DefaultLongitude = 116.4074;
    }
}
