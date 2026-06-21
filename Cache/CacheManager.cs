using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Windows.Storage;
using WeatherAppRT2._0.Models;

namespace WeatherAppRT2._0.Cache
{
    public class CacheManager
    {
        private const string CacheFileName = "weather_cache.json";
        private const string SettingsFileName = "weather_settings.json";

        private static StorageFolder LocalFolder
        {
            get { return ApplicationData.Current.LocalFolder; }
        }

        private static async Task<StorageFile> GetFileIfExistsAsync(string fileName)
        {
            try { return await LocalFolder.GetFileAsync(fileName); }
            catch { return null; }
        }

        #region 天气缓存

        public static async Task<WeatherData> GetCachedWeatherAsync()
        {
            try
            {
                var file = await GetFileIfExistsAsync(CacheFileName);
                if (file == null) { System.Diagnostics.Debug.WriteLine("[Cache] 缓存文件不存在"); return null; }

                var json = await FileIO.ReadTextAsync(file);
                var data = JsonConvert.DeserializeObject<WeatherData>(json);

                if (data != null && (DateTime.Now - data.FetchedAt).TotalMinutes < AppConfig.CacheMaxAgeMinutes)
                {
                    System.Diagnostics.Debug.WriteLine("[Cache] 缓存有效: {0}", data.City?.Name);
                    return data;
                }
                System.Diagnostics.Debug.WriteLine("[Cache] 缓存已过期");
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[Cache] GetCachedWeather error: " + ex.Message); }
            return null;
        }

        /// <summary>获取缓存数据（忽略过期时间，用于离线显示）</summary>
        public static async Task<WeatherData> GetStaleCachedWeatherAsync()
        {
            try
            {
                var file = await GetFileIfExistsAsync(CacheFileName);
                if (file == null) { System.Diagnostics.Debug.WriteLine("[Cache] StaleCache: 文件不存在"); return null; }

                var json = await FileIO.ReadTextAsync(file);
                var data = JsonConvert.DeserializeObject<WeatherData>(json);
                System.Diagnostics.Debug.WriteLine("[Cache] StaleCache 读取成功: {0}", data?.City?.Name);
                return data;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[Cache] GetStaleCached error: " + ex.Message); }
            return null;
        }

        public static async Task SaveToCacheAsync(WeatherData data)
        {
            try
            {
                data.FetchedAt = DateTime.Now;
                var json = JsonConvert.SerializeObject(data);
                var file = await LocalFolder.CreateFileAsync(CacheFileName, CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(file, json);
                System.Diagnostics.Debug.WriteLine("[Cache] 缓存已保存: {0} bytes", json.Length);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[Cache] SaveToCache error: " + ex.Message); }
        }

        #endregion

        #region 应用设置

        public static async Task<AppSettings> LoadSettingsAsync()
        {
            try
            {
                var file = await GetFileIfExistsAsync(SettingsFileName);
                if (file == null) return AppSettings.GetDefault();

                var json = await FileIO.ReadTextAsync(file);
                return JsonConvert.DeserializeObject<AppSettings>(json) ?? AppSettings.GetDefault();
            }
            catch
            {
                return AppSettings.GetDefault();
            }
        }

        public static async Task SaveSettingsAsync(AppSettings settings)
        {
            try
            {
                var json = JsonConvert.SerializeObject(settings);
                var file = await LocalFolder.CreateFileAsync(SettingsFileName, CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(file, json);
            }
            catch { }
        }

        #endregion
    }
}
