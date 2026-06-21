using System;
using Windows.ApplicationModel.Background;
using WeatherAppRT2._0.ApiClient;
using WeatherAppRT2._0.Cache;
using WeatherAppRT2._0.Services;

namespace WeatherAppRT2._0.BackgroundTasks
{
    public sealed class WeatherRefreshTask : IBackgroundTask
    {
        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            var deferral = taskInstance.GetDeferral();

            try
            {
                var settings = await CacheManager.LoadSettingsAsync();
                var cityId = settings.DefaultCityId ?? AppConfig.DefaultCityId;
                var cityName = settings.DefaultCityName ?? AppConfig.DefaultCityName;

                var apiClient = new QWeatherClient();
                var weather = await apiClient.GetFullWeatherByCityIdAsync(cityId, cityName);

                if (weather != null)
                {
                    await CacheManager.SaveToCacheAsync(weather);

                    // 更新主磁贴 + 锁屏 Badge + 详细状态文字
                    TileService.UpdatePrimaryTile(weather);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("BackgroundTask error: " + ex.Message);
            }
            finally
            {
                deferral.Complete();
            }
        }
    }
}
