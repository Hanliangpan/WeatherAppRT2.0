namespace WeatherAppRT2._0
{
    public static class AppConfig
    {
        // 使用 Open-Meteo 免费 API，无需 API Key

        // 调试模式：后台任务 1 分钟触发 + 强制推送通知 + 详细日志
#if DEBUG
        public const bool DebugMode = true;
#else
        public const bool DebugMode = false;
#endif

        // 缓存策略
        public const int CacheMaxAgeMinutes = 30;
        public const int ManualRefreshCooldownMinutes = 1;
        // WP8.1 TimeTrigger 最小间隔为 15 分钟，调试模式也用 15 分钟
        public const int BackgroundRefreshMinutes = DebugMode ? 15 : 30;
        public const int MaxCities = 10;

        // 默认城市（北京）
        public const string DefaultCityName = "北京";
        public const string DefaultCityId = "39.9042,116.4074";  // lat,lon 格式
        public const double DefaultLatitude = 39.9042;
        public const double DefaultLongitude = 116.4074;

        // 后台推送通知阈值
        public const double NotifyTempChangeThreshold = DebugMode ? 0.01 : 3.0;    // 调试：几乎任何温度变化都触发
        public const int NotifyRainProbabilityThreshold = DebugMode ? 0 : 50;       // 调试：任何降水概率都触发
        public const double NotifyWindSpeedThreshold = DebugMode ? 0.01 : 20.0;     // 调试：任何风速都触发
        public const int NotifyMinIntervalMinutes = DebugMode ? 0 : 60;             // 调试：不限制间隔
    }
}
