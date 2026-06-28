using System;
using System.Threading.Tasks;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using Windows.UI.StartScreen;
using WeatherAppRT2._0.Models;

namespace WeatherAppRT2._0.Services
{
    public class TileService
    {
        /// <summary>更新主磁贴 + 锁屏 Badge + 锁屏详细状态文字</summary>
        /// <remarks>
        /// WP8.1 锁屏能力（WinRT 应用）：
        ///   - Badge 数字：显示温度（需用户将应用添加到"显示即时状态"）
        ///   - 详细状态文字：锁屏上显示宽磁贴的第一行文本
        ///     （需 manifest 中 Notification="badgeAndTileText"，用户将应用设为"详细状态"提供者）
        ///   - 锁屏壁纸：WinRT 应用不支持 SetImageFileAsync（仅 Silverlight 可用）
        /// </remarks>
        public static void UpdatePrimaryTile(WeatherData weather)
        {
            if (weather == null) return;

            System.Diagnostics.Debug.WriteLine("[TileService] === UpdatePrimaryTile START ===");
            System.Diagnostics.Debug.WriteLine("[TileService] weather.Daily is null? {0}", weather.Daily == null);
            System.Diagnostics.Debug.WriteLine("[TileService] weather.Daily.Count={0}", weather.Daily?.Count ?? -1);
            if (weather.Daily != null && weather.Daily.Count > 0)
            {
                var d0 = weather.Daily[0];
                System.Diagnostics.Debug.WriteLine("[TileService] Daily[0]: Date={0}, Sunrise='{1}', Sunset='{2}', TempMax={3}, TempMin={4}",
                    d0.Date, d0.Sunrise ?? "(null)", d0.Sunset ?? "(null)", d0.TempMax, d0.TempMin);
            }

            string cityName = weather.City?.Name ?? "";
            int temperature = (int)(weather.Current?.Temperature ?? 0);
            string weatherDesc = weather.Current?.WeatherDesc ?? "";
            string iconEmoji = WeatherIconMapper.GetEmoji(weather.Current?.WeatherIcon ?? "999");

            // 正面：Logo + 天气摘要（简洁不重复城市名）
            string frontXml =
                "<tile><visual version=\"2\">" +
                // 中磁贴：只显示温度+天气+城市，避免溢出
                "<binding template=\"TileSquare150x150Block\" branding=\"none\">" +
                "<text id=\"1\">{1}°</text>" +
                "<text id=\"2\">{2}</text>" +
                "</binding>" +
                "<binding template=\"TileWide310x150BlockAndText01\" branding=\"none\">" +
                "<text id=\"1\">{1}°</text>" +
                "<text id=\"2\">{2}</text>" +
                "<text id=\"3\">{0}</text>" +
                "<text id=\"4\">{3}</text>" +
                "<text id=\"5\">{4}</text>" +
                "</binding>" +
                "</visual></tile>";

            // 构建详细状态文字（锁屏显示用）
            string detailLine = BuildDetailStatusText(weather);
            string line2 = BuildDetailLine2(weather);
            string line3 = BuildDetailLine3(weather);

            // 安全获取字段
            int feelsLike = (int)(weather.Current?.FeelsLike ?? 0);
            int humidity = weather.Current?.Humidity ?? 0;
            string windDir = weather.Current?.WindDirection ?? "";
            int windScale = weather.Current?.WindScale ?? 0;
            string rawSunrise = (weather.Daily != null && weather.Daily.Count > 0) ? (weather.Daily[0].Sunrise ?? "(null)") : "(no daily)";
            string rawSunset = (weather.Daily != null && weather.Daily.Count > 0) ? (weather.Daily[0].Sunset ?? "(null)") : "(no daily)";
            System.Diagnostics.Debug.WriteLine("[TileService] RAW Sunrise='{0}', RAW Sunset='{1}'", rawSunrise, rawSunset);
            string sunrise = (weather.Daily != null && weather.Daily.Count > 0 && !string.IsNullOrEmpty(weather.Daily[0].Sunrise)) ? FormatTileTime(weather.Daily[0].Sunrise) : "--";
            string sunset = (weather.Daily != null && weather.Daily.Count > 0 && !string.IsNullOrEmpty(weather.Daily[0].Sunset)) ? FormatTileTime(weather.Daily[0].Sunset) : "--";
            System.Diagnostics.Debug.WriteLine("[TileService] FORMATTED Sunrise='{0}', Sunset='{1}'", sunrise, sunset);
            System.Diagnostics.Debug.WriteLine("[TileService] AFTER EscapeXml: Sunrise='{0}', Sunset='{1}'", EscapeXml(sunrise), EscapeXml(sunset));
            int pressure = weather.Current?.Pressure ?? 0;
            string visibility = ((int)(weather.Current?.Visibility ?? 0)).ToString();

            System.Diagnostics.Debug.WriteLine("[TileService] Daily count={0}, Sunrise='{1}', Sunset='{2}'",
                weather.Daily?.Count ?? 0,
                weather.Daily != null && weather.Daily.Count > 0 ? weather.Daily[0].Sunrise ?? "(null)" : "(no daily)",
                weather.Daily != null && weather.Daily.Count > 0 ? weather.Daily[0].Sunset ?? "(null)" : "(no daily)");
            System.Diagnostics.Debug.WriteLine("[TileService] sunrise var='{0}'", sunrise);
            System.Diagnostics.Debug.WriteLine("[TileService] BEFORE Format: feelsLike={0}, humidity={1}, windDir='{2}', windScale={3}, sunrise='{4}', pressure={5}, visibility='{6}', sunset='{7}'",
                feelsLike, humidity, windDir, windScale, sunrise, pressure, visibility, sunset);

            // 构建正面 XML 的参数
            string frontDetailLine = BuildDetailStatusText(weather);
            string frontDetailLine2 = BuildDetailLine2(weather);
            string frontXmlFormatted = string.Format(frontXml,
                EscapeXml(cityName), temperature,
                EscapeXml(iconEmoji + " " + weatherDesc),
                EscapeXml(frontDetailLine),
                EscapeXml(frontDetailLine2));

            // 背面：丰富天气数据（branding="none" 避免重复显示城市名）
            string backXml = string.Format(
                "<tile><visual version=\"2\">" +
                // 中磁贴：简洁，温度+天气+城市
                "<binding template=\"TileSquare150x150Block\" branding=\"none\">" +
                "<text id=\"1\">{1}°</text>" +
                "<text id=\"2\">{2}</text>" +
                "</binding>" +
                "<binding template=\"TileWide310x150Text01\" branding=\"none\">" +
                "<text id=\"1\">{1}° {2}  {0}</text>" +
                "<text id=\"2\">体感{7}°  湿度{8}%</text>" +
                "<text id=\"3\">{9}{10}级  气压{12}hPa</text>" +
                "<text id=\"4\"></text>" +
                "<text id=\"5\"></text>" +
                "</binding>" +
                "<binding template=\"TileSquare310x310Text02\" branding=\"none\">" +
                "<text id=\"1\">{2} {1}°  {0}</text>" +
                "<text id=\"2\">体感{7}° | 湿度{8}% | {9}{10}级 | 气压{12}hPa | 日出{11}</text>" +
                "</binding>" +
                "</visual></tile>",
                EscapeXml(cityName), temperature,
                EscapeXml(iconEmoji + " " + weatherDesc),
                EscapeXml(frontDetailLine),
                "", // 4 placeholder
                "", // 5 placeholder
                "", // 6 placeholder
                feelsLike, humidity,
                EscapeXml(windDir), windScale,
                EscapeXml(sunrise), pressure, visibility,
                EscapeXml(sunset));

            System.Diagnostics.Debug.WriteLine("[TileService] backXml preview (first 600 chars): {0}", 
                backXml.Length > 600 ? backXml.Substring(0, 600) : backXml);

            var frontDoc = new XmlDocument();
            frontDoc.LoadXml(frontXmlFormatted);
            var backDoc = new XmlDocument();
            backDoc.LoadXml(backXml);
            System.Diagnostics.Debug.WriteLine("[TileService] LoadXml succeeded");

            var updater = TileUpdateManager.CreateTileUpdaterForApplication();
            updater.EnableNotificationQueue(false);
            // 先用新内容覆盖，再清理可能残留的旧通知
            updater.Update(new TileNotification(backDoc));
            updater.Clear();
            updater.Update(new TileNotification(backDoc));
            System.Diagnostics.Debug.WriteLine("[TileService] 磁贴已更新（先Update再Clear再Update）");

            // 同步更新锁屏 Badge（显示温度数字）
            UpdateBadge(temperature);
        }

        /// <summary>构建锁屏详细状态文字（宽磁贴第一行，显示在锁屏时间下方）</summary>
        private static string BuildDetailStatusText(WeatherData weather)
        {
            var c = weather.Current;
            var d = weather.Daily;
            string city = weather.City?.Name ?? "";
            string desc = c.WeatherDesc;

            // 精简格式：城市 · 天气 · ↑最高° ↓最低°
            // 锁屏详细状态只有一行，用空格和点号分隔，充分利用水平空间
            if (d != null && d.Count > 0)
                return string.Format("{0}  ·  {1}  ·  ↑{2}° ↓{3}°",
                    city, desc, (int)d[0].TempMax, (int)d[0].TempMin);

            return string.Format("{0}  ·  {1}", city, desc);
        }

        private static string BuildDetailLine2(WeatherData weather)
        {
            var c = weather.Current;
            return string.Format("体感{0}° | 湿度{1}% | {2}{3}级",
                (int)c.FeelsLike, c.Humidity,
                c.WindDirection, c.WindScale);
        }

        private static string BuildDetailLine3(WeatherData weather)
        {
            var c = weather.Current;
            var d = weather.Daily;
            string sunrise = "";
            if (d != null && d.Count > 0 && !string.IsNullOrEmpty(d[0].Sunrise))
                sunrise = " | 日出" + d[0].Sunrise;

            return string.Format("气压{0}hPa | 能见度{1}km{2}",
                c.Pressure, c.Visibility.ToString("F1"), sunrise);
        }

        /// <summary>固定城市二级磁贴</summary>
        public static async Task<bool> PinCityTile(WeatherData weather)
        {
            if (weather == null) return false;
            string cityName = weather.City?.Name ?? "";
            string weatherDesc = weather.Current?.WeatherDesc ?? "";
            string tileId = "city_" + cityName;

            if (SecondaryTile.Exists(tileId))
            {
                UpdateSecondaryTile(weather);
                return true;
            }

            // displayName 用天气描述，避免磁贴左下角显示城市名（与内容重复）
            string displayName = weatherDesc.Length > 20 ? weatherDesc.Substring(0, 18) + "…" : weatherDesc;

            var tile = new SecondaryTile(
                tileId,
                displayName,       // 显示天气描述而非城市名
                cityName,          // 参数（内部标识用）
                new Uri("ms-appx:///Assets/Logo.png"),
                TileSize.Default);

            tile.VisualElements.Wide310x150Logo = new Uri("ms-appx:///Assets/WideLogo.png");
            tile.VisualElements.Square310x310Logo = new Uri("ms-appx:///Assets/Logo.png");
            tile.VisualElements.ForegroundText = ForegroundText.Light;
            // 不显示磁贴名称，内容已包含所有信息
            tile.VisualElements.ShowNameOnSquare150x150Logo = false;
            tile.VisualElements.ShowNameOnWide310x150Logo = false;

            bool created = await tile.RequestCreateAsync();
            if (created)
                UpdateSecondaryTile(weather);

            return created;
        }

        /// <summary>更新二级磁贴</summary>
        public static void UpdateSecondaryTile(WeatherData weather)
        {
            if (weather == null) return;
            string tileId = "city_" + (weather.City?.Name ?? "");
            string cityName = weather.City?.Name ?? "";
            int temperature = (int)(weather.Current?.Temperature ?? 0);
            string weatherDesc = weather.Current?.WeatherDesc ?? "";
            string iconEmoji = WeatherIconMapper.GetEmoji(weather.Current?.WeatherIcon ?? "999");

            // 安全获取字段
            int sFeelsLike = (int)(weather.Current?.FeelsLike ?? 0);
            int sHumidity = weather.Current?.Humidity ?? 0;
            string sWindDir = weather.Current?.WindDirection ?? "";
            int sWindScale = weather.Current?.WindScale ?? 0;
            string sSunrise = (weather.Daily != null && weather.Daily.Count > 0 && !string.IsNullOrEmpty(weather.Daily[0].Sunrise)) ? FormatTileTime(weather.Daily[0].Sunrise) : "--";
            string sSunset = (weather.Daily != null && weather.Daily.Count > 0 && !string.IsNullOrEmpty(weather.Daily[0].Sunset)) ? FormatTileTime(weather.Daily[0].Sunset) : "--";
            int sPressure = weather.Current?.Pressure ?? 0;
            string sVisibility = ((int)(weather.Current?.Visibility ?? 0)).ToString();
            string sDetailLine = string.Format("↑{0}° ↓{1}°",
                (int)(weather.Daily?[0]?.TempMax ?? 0),
                (int)(weather.Daily?[0]?.TempMin ?? 0));

            // branding="none" 避免与磁贴内容重复
            string xml = string.Format(
                "<tile><visual version=\"2\">" +
                // 中磁贴：简洁，温度+天气+城市
                "<binding template=\"TileSquare150x150Block\" branding=\"none\">" +
                "<text id=\"1\">{1}°</text>" +
                "<text id=\"2\">{2}</text>" +
                "</binding>" +
                "<binding template=\"TileWide310x150Text01\" branding=\"none\">" +
                "<text id=\"1\">{1}° {2}  {0}</text>" +
                "<text id=\"2\">体感{3}°  湿度{4}%</text>" +
                "<text id=\"3\">{5}{6}级  气压{8}hPa</text>" +
                "<text id=\"4\"></text>" +
                "<text id=\"5\"></text>" +
                "</binding>" +
                "<binding template=\"TileSquare310x310Text02\" branding=\"none\">" +
                "<text id=\"1\">{2} {1}°  {0}</text>" +
                "<text id=\"2\">体感{3}° | 湿度{4}% | {5}{6}级 | 气压{8}hPa | 日出{7}</text>" +
                "</binding>" +
                "</visual></tile>",
                EscapeXml(cityName), temperature,
                EscapeXml(iconEmoji + " " + weatherDesc),
                sFeelsLike, sHumidity,
                EscapeXml(sWindDir), sWindScale,
                EscapeXml(sSunrise), sPressure, sVisibility,
                EscapeXml(sSunset), EscapeXml(sDetailLine));

            var doc = new XmlDocument();
            doc.LoadXml(xml);

            var updater = TileUpdateManager.CreateTileUpdaterForSecondaryTile(tileId);
            updater.EnableNotificationQueue(true);
            updater.Clear();
            updater.Update(new TileNotification(doc));
        }

        /// <summary>更新锁屏 Badge 显示温度数字</summary>
        /// <remarks>
        /// WP8.1 锁屏 badge 显示前提条件：
        /// 1. Package.appxmanifest 中声明 LockScreen Notification="badge" 且 BadgeLogo 为 24×24 单色 PNG
        /// 2. 应用已注册后台任务（锁屏通知需要后台任务支持）
        /// 3. 用户在 设置→锁屏界面→通知 中已将本应用添加到"显示即时状态"
        /// </remarks>
        public static void UpdateBadge(int temperature)
        {
            try
            {
                // 限制数字范围：WP8.1 badge 数字最大99
                int badgeValue = temperature;
                if (badgeValue > 99) badgeValue = 99;
                if (badgeValue < -99) badgeValue = -99;

                var badgeXml = BadgeUpdateManager.GetTemplateContent(BadgeTemplateType.BadgeNumber);
                var badgeElement = (XmlElement)badgeXml.SelectSingleNode("/badge");
                badgeElement.SetAttribute("value", badgeValue.ToString());

                System.Diagnostics.Debug.WriteLine("[TileService] Badge XML: " + badgeXml.GetXml());

                var badgeNotification = new BadgeNotification(badgeXml);
                BadgeUpdateManager.CreateBadgeUpdaterForApplication().Update(badgeNotification);

                System.Diagnostics.Debug.WriteLine("[TileService] Badge updated: " + badgeValue + "°");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[TileService] UpdateBadge error: " + ex.Message);
            }
        }

        /// <summary>将 "HH:mm" 格式转为磁贴安全的 "HH时mm分"，避免 ':' 被系统解析</summary>
        private static string FormatTileTime(string time)
        {
            if (string.IsNullOrEmpty(time)) return "--";
            var parts = time.Split(':');
            if (parts.Length == 2)
                return parts[0] + "时" + parts[1] + "分";
            return time;
        }

        private static string EscapeXml(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            string before = s;
            // 先移除 WP8.1 XmlDocument 不支持的 emoji / 高位 Unicode 字符
            s = StripEmoji(s);
            if (before != s)
                System.Diagnostics.Debug.WriteLine("[TileService] StripEmoji changed: '{0}' -> '{1}'", before, s);
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                     .Replace("\"", "&quot;").Replace("'", "&apos;");
        }

        /// <summary>
        /// 移除 emoji 和高位 Unicode 字符。
        /// WP8.1 的 XmlDocument.LoadXml() 遇到这些字符会导致解析异常或静默丢弃内容。
        /// </summary>
        private static string StripEmoji(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s)
            {
                // 过滤掉 emoji 范围：U+1F000~U+1FFFF, U+2600~U+27BF, U+2300~U+23FF 等
                // 简单方案：只保留 ASCII + 基本 CJK + 常用符号
                int code = (int)c;
                if (code >= 0x2600 && code <= 0x27BF) continue; // 杂项符号 (含天气emoji)
                if (code >= 0x1F000 && code <= 0x1FFFF) continue; // emoji 补充
                if (code >= 0xFE00 && code <= 0xFE0F) continue;   // 变体选择器
                if (code >= 0xE000 && code <= 0xF8FF) continue;   // 私用区
                sb.Append(c);
            }
            return sb.ToString().Trim();
        }
    }
}
