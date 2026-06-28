using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using WeatherAppRT2._0.ApiClient;
using WeatherAppRT2._0.Cache;
using WeatherAppRT2._0.Models;
using WeatherAppRT2._0.Services;

namespace WeatherAppRT2._0.BackgroundTasks
{
    public sealed class WeatherRefreshTask : IBackgroundTask
    {
        private const string LastNotifyTimeKey = "last_toast_notify_time";

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            BackgroundTaskDeferral deferral = null;
            AppSettings appSettings = null;
            try
            {
                deferral = taskInstance.GetDeferral();
                string taskId = taskInstance != null && taskInstance.Task != null ? taskInstance.Task.Name : "Unknown";
                await LogAsync(string.Format("[BGTASK] ======== 后台任务开始 ========"));
                await LogAsync(string.Format("[BGTASK] Task: {0}, InstanceId: {1}", taskId,
                    taskInstance != null ? taskInstance.InstanceId.ToString() : "N/A"));
                await LogAsync(string.Format("[BGTASK] DebugMode: {0}, RefreshInterval: {1}min", AppConfig.DebugMode, AppConfig.BackgroundRefreshMinutes));
                await LogAsync(string.Format("[BGTASK] Time: {0:yyyy-MM-dd HH:mm:ss}", DateTime.Now));

                try
                {
                // Step 1: 加载设置
                await LogAsync("[BGTASK] Step 1: 加载设置...");
                appSettings = await CacheManager.LoadSettingsAsync();
                var cityId = appSettings.DefaultCityId ?? AppConfig.DefaultCityId;
                var cityName = appSettings.DefaultCityName ?? AppConfig.DefaultCityName;
                await LogAsync(string.Format("[BGTASK]   城市: {0} ({1})", cityName, cityId));
                await LogAsync(string.Format("[BGTASK]   通知={0}, 间隔={1}min, 温度阈值={2}°C, 降水阈值={3}%, 风速阈值={4}km/h",
                    appSettings.EnableNotifications ? "开" : "关",
                    appSettings.GetNotifyMinInterval(),
                    appSettings.GetNotifyTempThreshold(),
                    appSettings.GetNotifyRainThreshold(),
                    appSettings.GetNotifyWindThreshold()));

                // Step 2: 读取旧缓存
                await LogAsync("[BGTASK] Step 2: 读取旧缓存...");
                var oldWeather = await CacheManager.GetStaleCachedWeatherAsync();
                if (oldWeather != null)
                {
                    await LogAsync(string.Format("[BGTASK]   旧缓存: {0}, 温度={1}°, 天气={2}, 风速={3}km/h, 湿度={4}%, 气压={5}hPa, 缓存时间={6:HH:mm:ss}",
                        oldWeather.City != null ? oldWeather.City.Name : "?",
                        oldWeather.Current != null ? oldWeather.Current.Temperature : 0,
                        oldWeather.Current != null ? oldWeather.Current.WeatherDesc : "?",
                        oldWeather.Current != null ? oldWeather.Current.WindSpeed : 0,
                        oldWeather.Current != null ? oldWeather.Current.Humidity : 0,
                        oldWeather.Current != null ? oldWeather.Current.Pressure : 0,
                        oldWeather.FetchedAt));
                    if (oldWeather.Daily != null && oldWeather.Daily.Count > 0)
                    {
                        await LogAsync(string.Format("[BGTASK]   旧逐日[0]: 最高={0}°, 最低={1}°, 降水={2}%",
                            oldWeather.Daily[0].TempMax, oldWeather.Daily[0].TempMin,
                            oldWeather.Daily[0].PrecipitationProbability));
                    }
                }
                else
                {
                    await LogAsync("[BGTASK]   旧缓存: 无 (首次运行或缓存已清除)");
                }

                // Step 3: 获取新天气（带超时保护，WP8.1 后台任务约 30 秒限制）
                await LogAsync("[BGTASK] Step 3: 调用 API 获取天气...");
                var apiClient = new QWeatherClient();
                var apiTask = apiClient.GetFullWeatherByCityIdAsync(cityId, cityName);
                var timeoutTask = Task.Delay(25000); // 25 秒超时
                var completedTask = await Task.WhenAny(apiTask, timeoutTask);
                if (completedTask == timeoutTask)
                {
                    await LogAsync("[BGTASK] !!! API 请求超时(25s)，任务结束");
                    return;
                }
                var weather = apiTask.Result;

                if (weather == null)
                {
                    await LogAsync("[BGTASK] !!! API 返回 null，任务结束");
                    return;
                }

                await LogAsync(string.Format("[BGTASK]   新数据: {0}, 温度={1}°, 天气={2}, 体感={3}°, 风速={4}km/h ({5}级), 风向={6}, 湿度={7}%, 气压={8}hPa, 能见度={9}km",
                    weather.City != null ? weather.City.Name : "?",
                    weather.Current != null ? weather.Current.Temperature : 0,
                    weather.Current != null ? weather.Current.WeatherDesc : "?",
                    weather.Current != null ? weather.Current.FeelsLike : 0,
                    weather.Current != null ? weather.Current.WindSpeed : 0,
                    weather.Current != null ? weather.Current.WindScale.ToString() : "?",
                    weather.Current != null ? weather.Current.WindDirection : "?",
                    weather.Current != null ? weather.Current.Humidity : 0,
                    weather.Current != null ? weather.Current.Pressure : 0,
                    weather.Current != null ? weather.Current.Visibility : 0));

                if (weather.Daily != null && weather.Daily.Count > 0)
                {
                    await LogAsync(string.Format("[BGTASK]   新逐日[0]: 最高={0}°, 最低={1}°, 降水={2}%, 日出={3}, 日落={4}",
                        weather.Daily[0].TempMax, weather.Daily[0].TempMin,
                        weather.Daily[0].PrecipitationProbability,
                        weather.Daily[0].Sunrise, weather.Daily[0].Sunset));
                }

                // Step 4: 保存缓存
                await LogAsync("[BGTASK] Step 4: 保存新缓存...");
                await CacheManager.SaveToCacheAsync(weather);
                await LogAsync("[BGTASK]   缓存已保存");

                // Step 5: 更新磁贴
                await LogAsync("[BGTASK] Step 5: 更新主磁贴 + Badge...");
                TileService.UpdatePrimaryTile(weather);
                await LogAsync("[BGTASK]   磁贴已更新");

                // Step 6: 检测天气变化
                await LogAsync("[BGTASK] Step 6: 检测天气变化...");
                if (!appSettings.EnableNotifications)
                {
                    await LogAsync("[BGTASK]   用户关闭了通知，跳过");
                }
                else if (oldWeather != null)
                {
                    TrySendWeatherAlert(appSettings, cityName, oldWeather, weather);
                }
                else
                {
                    await LogAsync("[BGTASK]   无旧缓存可对比，跳过变化检测");
                    // 调试模式：无旧缓存时也发一条通知
#if DEBUG
                    await LogAsync("[BGTASK]   [DEBUG] 无旧缓存，发送测试通知");
                    ShowToast(cityName, string.Format("首次后台更新完成\n{0} {1}° {2}",
                        cityName, (int)weather.Current.Temperature, weather.Current.WeatherDesc));
#endif
                }
            }
                catch (Exception ex)
                {
                    await LogAsync(string.Format("[BGTASK] !!! 异常: {0}: {1}", ex.GetType().Name, ex.Message));
                    await LogAsync(string.Format("[BGTASK] !!! Stack: {0}", ex.StackTrace));
                }
                finally
                {
                    await LogAsync(string.Format("[BGTASK] ======== 后台任务结束 (Time: {0:HH:mm:ss}) ========", DateTime.Now));
                    if (deferral != null) deferral.Complete();
                }
            }
            catch (Exception outerEx)
            {
                System.Diagnostics.Debug.WriteLine("[BGTASK] 外层异常: " + outerEx.GetType().Name + ": " + outerEx.Message);
                if (deferral != null) deferral.Complete();
            }
        }

        /// <summary>
        /// 通知优先级：极端天气 > 变化检测 > 定时播报
        /// </summary>
        private enum AlertType
        {
            None,
            DailyBriefing,  // 定时播报
            ChangeDetected, // 变化检测
            ExtremeWeather  // 极端天气预警
        }

        /// <summary>
        /// 检测天气变化并发送 Toast 推送通知。
        /// 三种触发机制：极端天气预警 > 变化检测 > 定时播报
        /// </summary>
        private async void TrySendWeatherAlert(AppSettings appSettings, string cityName, WeatherData oldWeather, WeatherData newWeather)
        {
            try
            {
            var old = oldWeather.Current;
            var now = newWeather.Current;
            if (old == null || now == null)
            {
                await LogAsync("[BGTASK]   Current 数据为空，跳过");
                return;
            }

            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;

            // 读取用户配置的阈值
            int notifyMinInterval = appSettings.GetNotifyMinInterval();
            double tempThreshold = appSettings.GetNotifyTempThreshold();
            int rainThreshold = appSettings.GetNotifyRainThreshold();
            double windThreshold = appSettings.GetNotifyWindThreshold();

            // 检查上次通知时间
            DateTime? lastNotifyTime = null;
            double minutesSinceLastNotify = double.MaxValue;
            object lastTicksObj;
            if (localSettings.Values.TryGetValue(LastNotifyTimeKey, out lastTicksObj))
            {
                lastNotifyTime = new DateTime((long)lastTicksObj);
                minutesSinceLastNotify = (DateTime.Now - lastNotifyTime.Value).TotalMinutes;
            }

            await LogAsync(string.Format("[BGTASK]   上次通知: {0}, 间隔: {1:F1}分钟",
                lastNotifyTime.HasValue ? lastNotifyTime.Value.ToString("HH:mm:ss") : "无",
                minutesSinceLastNotify));

            // 日志：当前天气
            await LogAsync(string.Format("[BGTASK]   当前: {0} {1}° {2} 降水{3}% 风速{4}km/h({5}级)",
                cityName, (int)now.Temperature, now.WeatherDesc,
                (newWeather.Daily != null && newWeather.Daily.Count > 0) ? newWeather.Daily[0].PrecipitationProbability.ToString() : "?",
                now.WindSpeed, now.WindScale));

            // ===== 按优先级检测通知类型 =====
            AlertType alertType = AlertType.None;
            string alertMessage = null;

            // 1. 极端天气预警（无视通知间隔）
            alertMessage = BuildExtremeWeatherAlert(cityName, now, newWeather.Daily);
            if (!string.IsNullOrEmpty(alertMessage))
            {
                alertType = AlertType.ExtremeWeather;
                await LogAsync(string.Format("[BGTASK]   >>> 触发极端天气预警"));
            }

            // 2. 变化检测（受通知间隔限制）
            if (alertType == AlertType.None && minutesSinceLastNotify >= notifyMinInterval)
            {
                // 日志：对比数据
                await LogAsync("[BGTASK]   --- 天气对比 ---");
                double tempDiff = Math.Abs(now.Temperature - old.Temperature);
                bool weatherChanged = old.WeatherDesc != now.WeatherDesc;
                bool tempChangedFlag = tempDiff >= tempThreshold;
                int oldRain = (oldWeather.Daily != null && oldWeather.Daily.Count > 0) ? oldWeather.Daily[0].PrecipitationProbability : 0;
                int newRain = (newWeather.Daily != null && newWeather.Daily.Count > 0) ? newWeather.Daily[0].PrecipitationProbability : 0;
                bool rainAlertFlag = newRain >= rainThreshold && (newRain - oldRain) >= 20;
                bool windAlertFlag = now.WindSpeed >= windThreshold;
                await LogAsync(string.Format("[BGTASK]   温度: {0}° → {1}° (差={2:F1}°, 阈值={3}°) {4}",
                    old.Temperature, now.Temperature, tempDiff, tempThreshold, tempChangedFlag ? "√" : "×"));
                await LogAsync(string.Format("[BGTASK]   天气: {0} → {1} {2}",
                    old.WeatherDesc, now.WeatherDesc, weatherChanged ? "√" : "×"));
                await LogAsync(string.Format("[BGTASK]   降水: {0}% → {1}% (阈值={2}%) {3}",
                    oldRain, newRain, rainThreshold, rainAlertFlag ? "√" : "×"));
                await LogAsync(string.Format("[BGTASK]   风速: {0}km/h → {1}km/h (阈值={2}km/h) {3}",
                    old.WindSpeed, now.WindSpeed, windThreshold, windAlertFlag ? "√" : "×"));

                alertMessage = BuildChangeAlert(cityName, old, now, oldWeather.Daily, newWeather.Daily,
                    tempThreshold, rainThreshold, windThreshold);
                if (!string.IsNullOrEmpty(alertMessage))
                {
                    alertType = AlertType.ChangeDetected;
                    await LogAsync(string.Format("[BGTASK]   >>> 触发天气变化通知"));
                }
            }

            // 3. 定时播报（受通知间隔限制，早晚各一次）
            if (alertType == AlertType.None && minutesSinceLastNotify >= 240) // 至少间隔4小时
            {
                alertMessage = BuildDailyBriefing(cityName, now, newWeather.Daily);
                if (!string.IsNullOrEmpty(alertMessage))
                {
                    alertType = AlertType.DailyBriefing;
                    await LogAsync(string.Format("[BGTASK]   >>> 触发定时播报"));
                }
            }

            // 发送通知
            if (!string.IsNullOrEmpty(alertMessage))
            {
                await LogAsync(string.Format("[BGTASK]   >>> 发送 Toast [{0}]: {1}", alertType, alertMessage));
                ShowToast(cityName, alertMessage);
                localSettings.Values[LastNotifyTimeKey] = DateTime.Now.Ticks;
            }
            else
            {
                await LogAsync("[BGTASK]   无需通知");

#if DEBUG
                // 调试模式：强制发送
                string debugMsg = string.Format("[DEBUG] {0} {1}° {2}",
                    cityName, (int)now.Temperature, now.WeatherDesc);
                await LogAsync(string.Format("[BGTASK]   >>> [DEBUG] 强制发送: {0}", debugMsg));
                ShowToast(cityName, debugMsg);
                localSettings.Values[LastNotifyTimeKey] = DateTime.Now.Ticks;
#endif
            }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[BGTASK] TrySendWeatherAlert 异常: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>
        /// 极端天气预警：高温/低温/暴雨/大风，无条件推送
        /// </summary>
        private string BuildExtremeWeatherAlert(string cityName, CurrentWeather now,
            System.Collections.Generic.List<DailyForecast> daily)
        {
            // 高温预警 > 35°C
            if (now.Temperature >= 35)
            {
                return string.Format("⚠ {0}高温预警：当前 {1}°，注意防暑降温", cityName, (int)now.Temperature);
            }

            // 低温预警 < 0°C
            if (now.Temperature <= 0)
            {
                return string.Format("⚠ {0}低温预警：当前 {1}°，注意防寒保暖", cityName, (int)now.Temperature);
            }

            // 暴雨预警：今日降水概率 > 80%
            int todayRain = (daily != null && daily.Count > 0) ? daily[0].PrecipitationProbability : 0;
            if (todayRain >= 80)
            {
                return string.Format("⚠ {0}暴雨预警：今日降水概率 {1}%，出门务必带伞", cityName, todayRain);
            }

            // 大风预警 > 30 km/h（约5级以上）
            if (now.WindSpeed >= 30)
            {
                return string.Format("⚠ {0}大风预警：当前风力 {1} 级 ({2}km/h)，注意安全", cityName, now.WindScale, (int)now.WindSpeed);
            }

            // 极端温差：今日最高最低温差 > 15°C
            if (daily != null && daily.Count > 0 && (daily[0].TempMax - daily[0].TempMin) >= 15)
            {
                return string.Format("⚠ {0}温差预警：今日 {1}° ~ {2}°，温差 {3}°，注意增减衣物",
                    cityName, (int)daily[0].TempMin, (int)daily[0].TempMax, (int)(daily[0].TempMax - daily[0].TempMin));
            }

            return null;
        }

        /// <summary>
        /// 天气变化检测通知（纯同步，日志由调用方输出）
        /// </summary>
        private string BuildChangeAlert(string cityName, CurrentWeather old, CurrentWeather now,
            System.Collections.Generic.List<DailyForecast> oldDaily,
            System.Collections.Generic.List<DailyForecast> newDaily,
            double tempThreshold, int rainThreshold, double windThreshold)
        {
            double tempDiff = Math.Abs(now.Temperature - old.Temperature);
            bool weatherChanged = old.WeatherDesc != now.WeatherDesc;
            bool tempChanged = tempDiff >= tempThreshold;

            int oldRain = (oldDaily != null && oldDaily.Count > 0) ? oldDaily[0].PrecipitationProbability : 0;
            int newRain = (newDaily != null && newDaily.Count > 0) ? newDaily[0].PrecipitationProbability : 0;
            bool rainAlert = newRain >= rainThreshold && (newRain - oldRain) >= 20;

            bool windAlert = now.WindSpeed >= windThreshold;

            if (rainAlert)
            {
                return string.Format("{0}今天降水概率升至 {1}%，出门记得带伞", cityName, newRain);
            }

            if (weatherChanged && tempChanged)
            {
                string trend = now.Temperature > old.Temperature ? "升至" : "降至";
                return string.Format("{0}天气转{1}，温度{2}{3}°",
                    cityName, now.WeatherDesc, trend, (int)now.Temperature);
            }

            if (weatherChanged)
            {
                return string.Format("{0}天气转为{1}，当前{2}°",
                    cityName, now.WeatherDesc, (int)now.Temperature);
            }

            if (tempChanged)
            {
                string trend = now.Temperature > old.Temperature ? "升" : "降";
                return string.Format("{0}温度{1}了 {2}°，当前{3}°",
                    cityName, trend, (int)tempDiff, (int)now.Temperature);
            }

            if (windAlert)
            {
                return string.Format("{0}风力增强至 {1} 级，请注意防风", cityName, now.WindScale);
            }

            return null;
        }

        /// <summary>
        /// 定时播报：早晚各一次（7:00-9:00 / 20:00-22:00），推送天气简报
        /// </summary>
        private string BuildDailyBriefing(string cityName, CurrentWeather now,
            System.Collections.Generic.List<DailyForecast> daily)
        {
            int hour = DateTime.Now.Hour;
            bool isMorning = hour >= 7 && hour <= 9;
            bool isEvening = hour >= 20 && hour <= 22;

            if (!isMorning && !isEvening)
                return null;

            string timeLabel = isMorning ? "早安" : "晚安";

            // 构建简明天气预报
            string forecast = "";
            if (daily != null && daily.Count > 0)
            {
                var today = daily[0];
                forecast = string.Format("，今日 {0}° ~ {1}° {2}",
                    (int)today.TempMin, (int)today.TempMax, now.WeatherDesc);

                // 降水提示
                if (today.PrecipitationProbability >= 50)
                    forecast += string.Format("，降水概率 {0}%", today.PrecipitationProbability);
            }

            return string.Format("{0}！{1}当前 {2}°{3}",
                timeLabel, cityName, (int)now.Temperature, forecast);
        }



        /// <summary>
        /// 发送 Toast 推送通知
        /// </summary>
        private void ShowToast(string cityName, string message)
        {
            try
            {
                var toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);
                var textNodes = toastXml.GetElementsByTagName("text");
                textNodes[0].InnerText = "MoooWeather - " + cityName;
                textNodes[1].InnerText = message;

                var toastElement = (XmlElement)toastXml.SelectSingleNode("/toast");
                toastElement.SetAttribute("launch", "city:" + cityName);

                var toast = new ToastNotification(toastXml);
                ToastNotificationManager.CreateToastNotifier().Show(toast);

                // Toast 发送成功
                System.Diagnostics.Debug.WriteLine("[BGTASK]   Toast 发送成功!");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("[BGTASK]   Toast 发送失败: {0}: {1}", ex.GetType().Name, ex.Message));
            }
        }

        /// <summary>
        /// 写日志到 LocalFolder 文件（后台任务中 Debug.WriteLine 不可靠）
        /// </summary>
        private static async System.Threading.Tasks.Task LogAsync(string msg)
        {
            // 始终输出到 Debug，方便在 VS Output 窗口查看
            System.Diagnostics.Debug.WriteLine(msg);

            try
            {
                var folder = Windows.Storage.ApplicationData.Current.LocalFolder;
                var file = await folder.CreateFileAsync("bg_task_log.txt",
                    Windows.Storage.CreationCollisionOption.OpenIfExists);
                await Windows.Storage.FileIO.AppendTextAsync(file,
                    DateTime.Now.ToString("MM-dd HH:mm:ss.fff") + " " + msg + "\r\n");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[BGTASK] 日志写文件失败: " + ex.GetType().Name + ": " + ex.Message);
            }
        }
    }
}
