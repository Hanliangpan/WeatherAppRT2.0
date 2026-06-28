using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.Data.Json;
using WeatherAppRT2._0.Models;

namespace WeatherAppRT2._0.ApiClient
{
    /// <summary>
    /// 使用 Open-Meteo 免费 API（无需 Key）
    /// 使用 Windows.Data.Json 替代 Newtonsoft.Json，避免 MDIL 编译问题
    /// </summary>
    public class QWeatherClient : IWeatherApiClient
    {
        private const string ForecastUrl = "https://api.open-meteo.com/v1/forecast";
        private const string GeoUrl = "https://geocoding-api.open-meteo.com/v1/search";

        private static HttpClient _httpClient = new HttpClient();

        public QWeatherClient(string apiKey = null) { }

        #region 辅助解析（Windows.Data.Json）

        private static double D(IJsonValue v) { try { return v?.GetNumber() ?? 0; } catch { return 0; } }
        private static int I(IJsonValue v) { try { return (int)(v?.GetNumber() ?? 0); } catch { return 0; } }
        private static string S(IJsonValue v) { try { return v?.GetString(); } catch { return null; } }

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
                lat = D(coord.GetNamedValue("latitude"));
                lon = D(coord.GetNamedValue("longitude"));
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
                string json = await _httpClient.GetStringAsync(url);
                var root = JsonObject.Parse(json);
                var arr = root.GetNamedArray("results");
                if (arr == null) return results;

                foreach (var city in arr)
                {
                    var obj = city.GetObject();
                    results.Add(new CitySearchResult
                    {
                        Id = string.Format("{0},{1}", D(obj.GetNamedValue("latitude")), D(obj.GetNamedValue("longitude"))),
                        Name = S(obj.GetNamedValue("name")),
                        Adm1 = S(obj.GetNamedValue("admin1")),
                        Country = S(obj.GetNamedValue("country")),
                        Latitude = D(obj.GetNamedValue("latitude")),
                        Longitude = D(obj.GetNamedValue("longitude"))
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

            string json;
            try
            {
                json = await _httpClient.GetStringAsync(url);
                System.Diagnostics.Debug.WriteLine("[API] Response received: " + json.Length + " chars");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[API] GetStringAsync 异常: " + ex.GetType().Name + ": " + ex.Message);
                if (ex.InnerException != null)
                    System.Diagnostics.Debug.WriteLine("[API] Inner: " + ex.InnerException.GetType().Name + ": " + ex.InnerException.Message);
                throw;
            }

            var root = JsonObject.Parse(json);

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

        private CurrentWeather ParseCurrent(JsonObject root)
        {
            var cur = root.GetNamedObject("current", null);
            if (cur == null) return null;
            return new CurrentWeather
            {
                Temperature = D(cur.GetNamedValue("temperature_2m")),
                FeelsLike = D(cur.GetNamedValue("apparent_temperature")),
                WeatherIcon = MapWeatherCode(I(cur.GetNamedValue("weather_code"))),
                WeatherDesc = GetWeatherDesc(I(cur.GetNamedValue("weather_code"))),
                Humidity = I(cur.GetNamedValue("relative_humidity_2m")),
                WindDirection = GetWindDir(D(cur.GetNamedValue("wind_direction_10m"))),
                WindSpeed = D(cur.GetNamedValue("wind_speed_10m")),
                WindScale = WindSpeedToScale(D(cur.GetNamedValue("wind_speed_10m"))),
                Pressure = (int)D(cur.GetNamedValue("surface_pressure")),
                Visibility = D(cur.GetNamedValue("visibility")) / 1000.0,
            };
        }

        private List<HourlyForecast> ParseHourly(JsonObject root)
        {
            var list = new List<HourlyForecast>();
            var hourly = root.GetNamedObject("hourly", null);
            if (hourly == null) return list;

            var times = hourly.GetNamedArray("time");
            var temps = hourly.GetNamedArray("temperature_2m");
            var codes = hourly.GetNamedArray("weather_code");
            var pops = hourly.GetNamedArray("precipitation_probability");

            System.Diagnostics.Debug.WriteLine("[API] ParseHourly: pops is null? {0}, count={1}",
                pops == null, pops?.Count ?? 0);
            if (pops != null && pops.Count > 0)
            {
                int nonZero = 0;
                for (int k = 0; k < Math.Min(10, pops.Count); k++)
                    if (I(pops[k]) != 0) nonZero++;
                System.Diagnostics.Debug.WriteLine("[API] ParseHourly: first 10 pops non-zero count={0}", nonZero);
            }

            if (times == null) return list;

            var now = DateTime.Now;
            int count = Math.Min(48, times.Count);

            for (int i = 0; i < count; i++)
            {
                DateTime dt;
                DateTime.TryParse(S(times[i]), out dt);
                if (dt < now.Date) continue;
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

        private List<DailyForecast> ParseDaily(JsonObject root)
        {
            var list = new List<DailyForecast>();
            var daily = root.GetNamedObject("daily", null);
            if (daily == null) return list;

            var dates = daily.GetNamedArray("time");
            var maxT = daily.GetNamedArray("temperature_2m_max");
            var minT = daily.GetNamedArray("temperature_2m_min");
            var codes = daily.GetNamedArray("weather_code");
            var sunrises = daily.GetNamedArray("sunrise");
            var sunsets = daily.GetNamedArray("sunset");
            System.Diagnostics.Debug.WriteLine("[API] ParseDaily: sunrises count={0}, first='{1}'",
                sunrises?.Count ?? 0,
                sunrises != null && sunrises.Count > 0 ? S(sunrises[0]) ?? "(null)" : "(empty)");
            var uvMax = daily.GetNamedArray("uv_index_max");
            var popMax = daily.GetNamedArray("precipitation_probability_max");
            var windMax = daily.GetNamedArray("wind_speed_10m_max");
            var windDir = daily.GetNamedArray("wind_direction_10m_dominant");
            var humMax = daily.GetNamedArray("relative_humidity_2m_max");

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

        private async Task<JsonObject> GeocodeCityAsync(string cityName)
        {
            try
            {
                string url = string.Format("{0}?name={1}&count=1&language=zh", GeoUrl, Uri.EscapeDataString(cityName));
                string json = await _httpClient.GetStringAsync(url);
                var root = JsonObject.Parse(json);
                var results = root.GetNamedArray("results");
                if (results == null || results.Count == 0) return null;
                return results[0].GetObject();
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
                string json = await _httpClient.GetStringAsync(url);
                var root = JsonObject.Parse(json);
                var addr = root.GetNamedObject("address", null);
                if (addr != null)
                {
                    string city = S(addr.GetNamedValue("city")) ?? S(addr.GetNamedValue("town")) ?? S(addr.GetNamedValue("county"));
                    if (!string.IsNullOrEmpty(city)) return city;
                }
                string displayName = S(root.GetNamedValue("display_name"));
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
            if (string.IsNullOrEmpty(isoDateTime))
            {
                System.Diagnostics.Debug.WriteLine("[API] ShortenTime: input is null or empty");
                return "";
            }
            int tIdx = isoDateTime.IndexOf('T');
            if (tIdx > 0 && isoDateTime.Length > tIdx + 5)
            {
                string result = isoDateTime.Substring(tIdx + 1, 5);
                System.Diagnostics.Debug.WriteLine("[API] ShortenTime: '{0}' -> '{1}'", isoDateTime, result);
                return result;
            }
            System.Diagnostics.Debug.WriteLine("[API] ShortenTime: no T found or too short, returning raw: '{0}'", isoDateTime);
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

        private static readonly Dictionary<int, string> WeatherCodeMap = new Dictionary<int, string>
        {
            {0, "100"}, {1, "102"}, {2, "103"}, {3, "104"},
            {45, "501"}, {48, "501"},
            {51, "309"}, {53, "309"}, {55, "309"},
            {56, "313"}, {57, "313"},
            {61, "305"}, {63, "306"}, {65, "307"},
            {66, "313"}, {67, "313"},
            {71, "400"}, {73, "401"}, {75, "402"}, {77, "400"},
            {80, "300"}, {81, "300"}, {82, "300"},
            {85, "407"}, {86, "407"},
            {95, "302"}, {96, "304"}, {99, "304"},
        };

        private static readonly Dictionary<int, string> WeatherDescMap = new Dictionary<int, string>
        {
            {0, "晴天"}, {1, "大部晴朗"}, {2, "局部多云"}, {3, "多云"},
            {45, "雾"}, {48, "雾"},
            {51, "毛毛雨"}, {53, "毛毛雨"}, {55, "毛毛雨"},
            {56, "冻毛毛雨"}, {57, "冻毛毛雨"},
            {61, "小雨"}, {63, "中雨"}, {65, "大雨"},
            {66, "冻雨"}, {67, "冻雨"},
            {71, "小雪"}, {73, "中雪"}, {75, "大雪"}, {77, "雪粒"},
            {80, "阵雨"}, {81, "阵雨"}, {82, "阵雨"},
            {85, "阵雪"}, {86, "阵雪"},
            {95, "雷暴"}, {96, "雷暴伴冰雹"}, {99, "雷暴伴冰雹"},
        };

        private static string MapWeatherCode(int code)
        {
            string result;
            if (WeatherCodeMap.TryGetValue(code, out result))
                return result;
            return "999";
        }

        private static string GetWeatherDesc(int code)
        {
            string result;
            if (WeatherDescMap.TryGetValue(code, out result))
                return result;
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
