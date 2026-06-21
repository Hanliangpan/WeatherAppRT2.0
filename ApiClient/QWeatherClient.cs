using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WeatherAppRT2._0.Models;

namespace WeatherAppRT2._0.ApiClient
{
    /// <summary>
    /// 使用 Open-Meteo 免费 API（无需 Key）
    /// </summary>
    public class QWeatherClient : IWeatherApiClient
    {
        private const string ForecastUrl = "https://api.open-meteo.com/v1/forecast";
        private const string GeoUrl = "https://geocoding-api.open-meteo.com/v1/search";

        private static readonly HttpClient _httpClient = new HttpClient();

        public QWeatherClient(string apiKey = null) { }

        #region 辅助解析（兼容 Newtonsoft.Json 6.0.8）

        private static double D(JToken t) { try { return t != null ? (double)t : 0; } catch { return 0; } }
        private static int I(JToken t) { try { return t != null ? (int)t : 0; } catch { return 0; } }
        private static string S(JToken t) { try { return t != null ? (string)t : null; } catch { return null; } }

        #endregion

        #region 公共接口

        public async Task<WeatherData> GetFullWeatherByCityIdAsync(string cityId, string cityName)
        {
            System.Diagnostics.Debug.WriteLine("[API] GetFullWeatherByCityId: {0} ({1})", cityName, cityId);

            double lat, lon;
            if (!TryParseCoord(cityId, out lat, out lon))
            {
                System.Diagnostics.Debug.WriteLine("[API] cityId 非坐标，通过 geocoding 查询...");
                var coord = await GeocodeCityAsync(cityName);
                if (coord == null) throw new Exception("城市未找到: " + cityName);
                lat = D(coord["latitude"]);
                lon = D(coord["longitude"]);
            }

            return await GetWeatherByCoords(lat, lon, cityName);
        }

        public async Task<WeatherData> GetFullWeatherByLocationAsync(double lat, double lon)
        {
            System.Diagnostics.Debug.WriteLine("[API] GetFullWeatherByLocation: {0},{1}", lat, lon);
            string cityName = await ReverseGeocode(lat, lon);
            return await GetWeatherByCoords(lat, lon, cityName);
        }

        public async Task<List<CitySearchResult>> SearchCityAsync(string keyword)
        {
            System.Diagnostics.Debug.WriteLine("[API] SearchCity: {0}", keyword);
            var results = new List<CitySearchResult>();
            try
            {
                string url = string.Format("{0}?name={1}&count=10&language=zh",
                    GeoUrl, Uri.EscapeDataString(keyword));
                string json = await _httpClient.GetStringAsync(new Uri(url, UriKind.Absolute));
                var root = JObject.Parse(json);
                var arr = root["results"] as JArray;
                if (arr == null) return results;

                foreach (var city in arr)
                {
                    results.Add(new CitySearchResult
                    {
                        Id = string.Format("{0},{1}", D(city["latitude"]), D(city["longitude"])),
                        Name = S(city["name"]),
                        Adm1 = S(city["admin1"]),
                        Country = S(city["country"]),
                        Latitude = D(city["latitude"]),
                        Longitude = D(city["longitude"])
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[API] SearchCity error: " + ex.Message);
            }
            return results;
        }

        #endregion

        #region 核心请求

        private async Task<WeatherData> GetWeatherByCoords(double lat, double lon, string cityName)
        {
            string url = string.Format(
                "{0}?latitude={1}&longitude={2}" +
                "&current=temperature_2m,apparent_temperature,relative_humidity_2m,weather_code,wind_speed_10m,wind_direction_10m,surface_pressure,visibility" +
                "&hourly=temperature_2m,weather_code,precipitation_probability" +
                "&daily=weather_code,temperature_2m_max,temperature_2m_min,sunrise,sunset,uv_index_max,precipitation_probability_max,wind_speed_10m_max,wind_direction_10m_dominant,relative_humidity_2m_max" +
                "&timezone=auto&forecast_days=7",
                ForecastUrl, lat.ToString("F4"), lon.ToString("F4"));

            System.Diagnostics.Debug.WriteLine("[API] URL: " + url);
            System.Diagnostics.Debug.WriteLine("[API] Fetching weather with GetStringAsync...");

            string json;
            try
            {
                // 用 Task.WhenAny 做 30 秒超时（WP8.1 HttpClient 可能无限挂起）
                var fetchTask = _httpClient.GetStringAsync(new Uri(url, UriKind.Absolute));
                var timeoutTask = Task.Delay(30000);
                var winner = await Task.WhenAny(fetchTask, timeoutTask);
                if (winner == timeoutTask)
                {
                    System.Diagnostics.Debug.WriteLine("[API] GetStringAsync 超时 (30s)");
                    throw new TimeoutException("HTTP 请求超时");
                }
                json = fetchTask.Result;
                System.Diagnostics.Debug.WriteLine("[API] Response received: " + json.Length + " chars");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[API] GetStringAsync 异常: " + ex.GetType().Name + ": " + ex.Message);
                if (ex.InnerException != null)
                    System.Diagnostics.Debug.WriteLine("[API] Inner: " + ex.InnerException.GetType().Name + ": " + ex.InnerException.Message);
                throw;
            }

            var root = JObject.Parse(json);

            var data = new WeatherData
            {
                FetchedAt = DateTime.Now,
                City = new CityInfo
                {
                    Id = string.Format("{0},{1}", lat, lon),
                    Name = cityName,
                    Latitude = lat,
                    Longitude = lon
                },
                Current = ParseCurrent(root),
                Hourly = ParseHourly(root),
                Daily = ParseDaily(root)
            };

            System.Diagnostics.Debug.WriteLine("[API] Done. Current={0}°, Hourly={1}, Daily={2}",
                data.Current != null ? data.Current.Temperature : -999,
                data.Hourly != null ? data.Hourly.Count : 0,
                data.Daily != null ? data.Daily.Count : 0);
            return data;
        }

        #endregion

        #region 解析

        private CurrentWeather ParseCurrent(JObject root)
        {
            var cur = root["current"];
            if (cur == null) return null;
            return new CurrentWeather
            {
                Temperature = D(cur["temperature_2m"]),
                FeelsLike = D(cur["apparent_temperature"]),
                WeatherIcon = MapWeatherCode(I(cur["weather_code"])),
                WeatherDesc = GetWeatherDesc(I(cur["weather_code"])),
                Humidity = I(cur["relative_humidity_2m"]),
                WindDirection = GetWindDir(D(cur["wind_direction_10m"])),
                WindSpeed = D(cur["wind_speed_10m"]),
                WindScale = WindSpeedToScale(D(cur["wind_speed_10m"])),
                Pressure = (int)D(cur["surface_pressure"]),
                Visibility = D(cur["visibility"]) / 1000.0,
            };
        }

        private List<HourlyForecast> ParseHourly(JObject root)
        {
            var list = new List<HourlyForecast>();
            var hourly = root["hourly"];
            if (hourly == null) return list;

            var times = hourly["time"] as JArray;
            var temps = hourly["temperature_2m"] as JArray;
            var codes = hourly["weather_code"] as JArray;
            var pops = hourly["precipitation_probability"] as JArray;

            if (times == null) return list;
            int count = Math.Min(24, times.Count);

            for (int i = 0; i < count; i++)
            {
                DateTime dt;
                DateTime.TryParse(S(times[i]), out dt);
                list.Add(new HourlyForecast
                {
                    Time = dt,
                    Temperature = temps != null && i < temps.Count ? D(temps[i]) : 0,
                    WeatherIcon = MapWeatherCode(codes != null && i < codes.Count ? I(codes[i]) : 0),
                    PrecipitationProbability = pops != null && i < pops.Count ? I(pops[i]) : 0,
                });
            }
            return list;
        }

        private List<DailyForecast> ParseDaily(JObject root)
        {
            var list = new List<DailyForecast>();
            var daily = root["daily"];
            if (daily == null) return list;

            var dates = daily["time"] as JArray;
            var maxT = daily["temperature_2m_max"] as JArray;
            var minT = daily["temperature_2m_min"] as JArray;
            var codes = daily["weather_code"] as JArray;
            var sunrises = daily["sunrise"] as JArray;
            var sunsets = daily["sunset"] as JArray;
            var uvMax = daily["uv_index_max"] as JArray;
            var popMax = daily["precipitation_probability_max"] as JArray;
            var windMax = daily["wind_speed_10m_max"] as JArray;
            var windDir = daily["wind_direction_10m_dominant"] as JArray;
            var humMax = daily["relative_humidity_2m_max"] as JArray;

            if (dates == null) return list;

            for (int i = 0; i < dates.Count; i++)
            {
                DateTime date;
                DateTime.TryParse(S(dates[i]), out date);

                list.Add(new DailyForecast
                {
                    Date = date,
                    DayOfWeek = GetDayOfWeek(date),
                    TempMax = maxT != null && i < maxT.Count ? D(maxT[i]) : 0,
                    TempMin = minT != null && i < minT.Count ? D(minT[i]) : 0,
                    IconDay = MapWeatherCode(codes != null && i < codes.Count ? I(codes[i]) : 0),
                    IconNight = MapWeatherCode(codes != null && i < codes.Count ? I(codes[i]) : 0),
                    PrecipitationProbability = popMax != null && i < popMax.Count ? I(popMax[i]) : 0,
                    WeatherDesc = GetWeatherDesc(codes != null && i < codes.Count ? I(codes[i]) : 0),
                    Sunrise = sunrises != null && i < sunrises.Count ? ShortenTime(S(sunrises[i])) : "",
                    Sunset = sunsets != null && i < sunsets.Count ? ShortenTime(S(sunsets[i])) : "",
                    UvIndex = uvMax != null && i < uvMax.Count ? I(uvMax[i]) : 0,
                    WindDirection = windDir != null && i < windDir.Count ? GetWindDir(D(windDir[i])) : "",
                    WindScale = windMax != null && i < windMax.Count ? WindSpeedToScale(D(windMax[i])) : 0,
                    Humidity = humMax != null && i < humMax.Count ? I(humMax[i]) : 0,
                });
            }
            return list;
        }

        #endregion

        #region 辅助方法

        private bool TryParseCoord(string cityId, out double lat, out double lon)
        {
            lat = 0; lon = 0;
            if (string.IsNullOrEmpty(cityId) || !cityId.Contains(",")) return false;
            var parts = cityId.Split(',');
            return parts.Length == 2
                && double.TryParse(parts[0], out lat)
                && double.TryParse(parts[1], out lon);
        }

        private async Task<JObject> GeocodeCityAsync(string cityName)
        {
            try
            {
                string url = string.Format("{0}?name={1}&count=1&language=zh", GeoUrl, Uri.EscapeDataString(cityName));
                string json = await _httpClient.GetStringAsync(new Uri(url, UriKind.Absolute));
                var root = JObject.Parse(json);
                var first = (root["results"] as JArray);
                if (first == null || first.Count == 0) return null;
                return first[0] as JObject;
            }
            catch { return null; }
        }

        private async Task<string> ReverseGeocode(double lat, double lon)
        {
            try
            {
                string url = string.Format(
                    "https://nominatim.openstreetmap.org/reverse?lat={0}&lon={1}&format=json&accept-language=zh",
                    lat, lon);
                string json = await _httpClient.GetStringAsync(new Uri(url, UriKind.Absolute));
                var root = JObject.Parse(json);
                var addr = root["address"] as JObject;
                if (addr != null)
                {
                    string city = S(addr["city"]) ?? S(addr["town"]) ?? S(addr["county"]);
                    if (!string.IsNullOrEmpty(city)) return city;
                }
                string displayName = S(root["display_name"]);
                if (displayName != null)
                {
                    string[] parts = displayName.Split(',');
                    return parts[0].Trim();
                }
                return "未知位置";
            }
            catch { return "未知位置"; }
        }

        private static string ShortenTime(string isoDateTime)
        {
            if (string.IsNullOrEmpty(isoDateTime)) return "";
            int tIdx = isoDateTime.IndexOf('T');
            if (tIdx > 0 && isoDateTime.Length > tIdx + 5)
                return isoDateTime.Substring(tIdx + 1, 5);
            return isoDateTime;
        }

        private static string GetDayOfWeek(DateTime date)
        {
            if (date.Date == DateTime.Today) return "今天";
            if (date.Date == DateTime.Today.AddDays(1)) return "明天";
            if (date.Date == DateTime.Today.AddDays(2)) return "后天";
            var days = new[] { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };
            return days[(int)date.DayOfWeek];
        }

        private static string MapWeatherCode(int code)
        {
            if (code == 0) return "100";
            if (code <= 3) return "101";
            if (code <= 48) return "104";
            if (code <= 55) return "300";
            if (code <= 57) return "301";
            if (code <= 65) return "305";
            if (code <= 67) return "306";
            if (code <= 77) return "404";
            if (code <= 82) return "309";
            if (code <= 86) return "407";
            if (code <= 99) return "501";
            return "999";
        }

        private static string GetWeatherDesc(int code)
        {
            if (code == 0) return "晴天";
            if (code == 1) return "大部晴朗";
            if (code == 2) return "局部多云";
            if (code == 3) return "多云";
            if (code <= 48) return "雾";
            if (code <= 55) return "毛毛雨";
            if (code <= 57) return "冻毛毛雨";
            if (code <= 61) return "小雨";
            if (code <= 63) return "中雨";
            if (code <= 65) return "大雨";
            if (code <= 67) return "冻雨";
            if (code <= 71) return "小雪";
            if (code <= 73) return "中雪";
            if (code <= 75) return "大雪";
            if (code == 77) return "雪粒";
            if (code <= 82) return "阵雨";
            if (code <= 86) return "阵雪";
            if (code == 95) return "雷暴";
            if (code <= 99) return "雷暴伴冰雹";
            return "未知";
        }

        private static string GetWindDir(double degrees)
        {
            string[] dirs = { "北", "东北", "东", "东南", "南", "西南", "西", "西北" };
            int idx = (int)((degrees + 22.5) / 45) % 8;
            return dirs[idx];
        }

        private static int WindSpeedToScale(double kmh)
        {
            if (kmh < 1) return 0;
            if (kmh < 6) return 1;
            if (kmh < 12) return 2;
            if (kmh < 20) return 3;
            if (kmh < 29) return 4;
            if (kmh < 39) return 5;
            if (kmh < 50) return 6;
            if (kmh < 62) return 7;
            if (kmh < 75) return 8;
            if (kmh < 89) return 9;
            if (kmh < 103) return 10;
            return 11;
        }

        #endregion
    }
}
