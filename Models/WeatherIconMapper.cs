using System.Collections.Generic;

namespace WeatherAppRT2._0.Models
{
    /// <summary>
    /// 天气图标映射器
    /// WP8.1 Segoe UI 不支持彩色 Emoji，使用 Segoe UI Symbol 单色字符
    /// 字体: FontFamily="Segoe UI Symbol"
    /// 
    /// 注意: 图标代码来自 QWeatherClient.MapWeatherCode() 转换后的 QWeather 风格代码
    /// </summary>
    public static class WeatherIconMapper
    {
        private static readonly Dictionary<string, string> _iconMap = new Dictionary<string, string>
        {
            { "100", "sunny" },          // 晴
            { "101", "cloudy" },         // 多云
            { "102", "partly_cloudy" },  // 少云
            { "103", "partly_cloudy" },  // 晴间多云
            { "104", "overcast" },       // 阴
            { "150", "clear_night" },    // 晴（夜）
            { "151", "cloudy_night" },   // 多云（夜）
            { "153", "partly_cloudy_night" }, // 晴间多云（夜）
            { "300", "light_rain" },     // 阵雨
            { "301", "heavy_rain" },     // 强阵雨
            { "302", "thunderstorm" },   // 雷阵雨
            { "303", "thunderstorm" },   // 强雷阵雨
            { "304", "hail" },           // 雷阵雨伴有冰雹
            { "305", "light_rain" },     // 小雨
            { "306", "moderate_rain" },  // 中雨
            { "307", "heavy_rain" },     // 大雨
            { "308", "storm_rain" },     // 极端降雨
            { "309", "light_rain" },     // 毛毛雨/细雨
            { "310", "storm_rain" },     // 暴雨
            { "311", "storm_rain" },     // 大暴雨
            { "312", "storm_rain" },     // 特大暴雨
            { "313", "freezing_rain" },  // 冻雨
            { "314", "light_rain" },     // 小到中雨
            { "315", "heavy_rain" },     // 中到大雨
            { "316", "storm_rain" },     // 大到暴雨
            { "399", "rain" },           // 雨
            { "400", "light_snow" },     // 小雪
            { "401", "moderate_snow" },  // 中雪
            { "402", "heavy_snow" },     // 大雪
            { "403", "blizzard" },       // 暴雪
            { "404", "sleet" },          // 雨夹雪
            { "405", "sleet" },          // 雨雪天气
            { "406", "sleet" },          // 阵雨夹雪
            { "407", "light_snow" },     // 阵雪
            { "499", "snow" },           // 雪
            { "500", "mist" },           // 薄雾
            { "501", "fog" },            // 雾
            { "502", "haze" },           // 霾
            { "503", "sandstorm" },      // 扬沙
            { "504", "duststorm" },      // 浮尘
            { "507", "duststorm" },      // 沙尘暴
            { "508", "duststorm" },      // 强沙尘暴
            { "900", "hot" },            // 热
            { "901", "cold" },           // 冷
            { "999", "unknown" },        // 未知
        };

        public static string GetIconName(string qweatherCode)
        {
            string name;
            if (_iconMap.TryGetValue(qweatherCode, out name))
                return name;
            return "unknown";
        }

        /// <summary>
        /// 获取 Segoe UI Symbol 单色天气图标字符
        /// WP8.1 不支持彩色 Emoji，使用 Unicode 符号字符
        /// </summary>
        public static string GetEmoji(string qweatherCode)
        {
            var name = GetIconName(qweatherCode);
            switch (name)
            {
                // 晴/夜
                case "sunny":
                case "clear_night":     return "\u2600";  // ☀

                // 多云/少云
                case "cloudy":
                case "cloudy_night":
                case "partly_cloudy":
                case "partly_cloudy_night": return "\u26C5"; // ⛅

                // 阴
                case "overcast":        return "\u2601";  // ☁

                // 小雨/中雨
                case "light_rain":
                case "moderate_rain":
                case "rain":            return "\u2614";  // ☔

                // 大雨/暴雨
                case "heavy_rain":
                case "storm_rain":
                case "freezing_rain":   return "\u2602";  // ☂

                // 雷暴
                case "thunderstorm":
                case "hail":            return "\u26A1";  // ⚡

                // 雪
                case "light_snow":
                case "moderate_snow":
                case "heavy_snow":
                case "snow":
                case "blizzard":        return "\u2744";  // ❄

                // 雨夹雪
                case "sleet":           return "\u2603";  // ☃

                // 雾/薄雾/霾 — 使用三条水平线符号
                case "mist":
                case "fog":
                case "haze":            return "\u2261";  // ≡ (三条横线，比‖更明显)

                // 沙尘暴
                case "sandstorm":
                case "duststorm":       return "\u2248";  // ≈

                // 极端温度
                case "hot":             return "\u2668";  // ♨
                case "cold":            return "\u2731";  // ✱

                default:                return "\u00B0";  // °
            }
        }
    }
}
