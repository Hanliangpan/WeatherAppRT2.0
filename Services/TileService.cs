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

            string cityName = weather.City?.Name ?? "";
            int temperature = (int)(weather.Current?.Temperature ?? 0);
            string weatherDesc = weather.Current?.WeatherDesc ?? "";
            string iconEmoji = WeatherIconMapper.GetEmoji(weather.Current?.WeatherIcon ?? "999");

            // 正面：应用图标
            string frontXml =
                "<tile><visual version=\"2\">" +
                "<binding template=\"TileSquare150x150Image\" branding=\"name\">" +
                "<image id=\"1\" src=\"ms-appx:///Assets/Logo.png\" alt=\"晴雨表\"/>" +
                "</binding>" +
                "<binding template=\"TileWide310x150Image\" branding=\"name\">" +
                "<image id=\"1\" src=\"ms-appx:///Assets/WideLogo.png\" alt=\"晴雨表\"/>" +
                "</binding>" +
                "</visual></tile>";

            // 构建详细状态文字（锁屏显示用）
            string detailLine = BuildDetailStatusText(weather);

            // 背面：天气数据 — 宽磁贴第一行是锁屏详细状态文字
            string backXml = string.Format(
                "<tile><visual version=\"2\">" +
                "<binding template=\"TileSquare150x150Block\" branding=\"name\">" +
                "<text id=\"1\">{1}°</text>" +
                "<text id=\"2\">{0}</text>" +
                "</binding>" +
                "<binding template=\"TileWide310x150Text05\" branding=\"name\">" +
                "<text id=\"1\">{4}</text>" +        // 锁屏详细状态文字
                "<text id=\"2\">{2} {3}</text>" +    // 天气描述
                "<text id=\"3\">{5}</text>" +        // 详情第2行
                "<text id=\"4\">{6}</text>" +        // 详情第3行
                "</binding>" +
                "<binding template=\"TileSquare310x310Text02\" branding=\"name\">" +
                "<text id=\"1\">{0}  {1}°C  {2}</text>" +
                "<text id=\"2\">{4}</text>" +
                "</binding>" +
                "</visual></tile>",
                EscapeXml(cityName), temperature,
                EscapeXml(weatherDesc), EscapeXml(iconEmoji),
                EscapeXml(detailLine),
                EscapeXml(BuildDetailLine2(weather)),
                EscapeXml(BuildDetailLine3(weather)));

            var frontDoc = new XmlDocument();
            frontDoc.LoadXml(frontXml);
            var backDoc = new XmlDocument();
            backDoc.LoadXml(backXml);

            var updater = TileUpdateManager.CreateTileUpdaterForApplication();
            updater.EnableNotificationQueue(true);
            updater.Clear();
            updater.Update(new TileNotification(frontDoc));
            updater.Update(new TileNotification(backDoc));

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
        public static async Task<bool> PinCityTile(string cityName, double temperature,
            string weatherDesc)
        {
            string tileId = "city_" + cityName;

            if (SecondaryTile.Exists(tileId))
            {
                UpdateSecondaryTile(tileId, cityName, temperature, weatherDesc);
                return true;
            }

            var tile = new SecondaryTile(
                tileId,
                cityName,
                cityName,
                new Uri("ms-appx:///Assets/Logo.png"),
                TileSize.Default);

            tile.VisualElements.Wide310x150Logo = new Uri("ms-appx:///Assets/WideLogo.png");
            tile.VisualElements.Square310x310Logo = new Uri("ms-appx:///Assets/Logo.png");
            tile.VisualElements.ForegroundText = ForegroundText.Light;
            tile.VisualElements.ShowNameOnSquare150x150Logo = true;
            tile.VisualElements.ShowNameOnWide310x150Logo = true;

            bool created = await tile.RequestCreateAsync();
            if (created)
                UpdateSecondaryTile(tileId, cityName, temperature, weatherDesc);

            return created;
        }

        /// <summary>更新二级磁贴</summary>
        public static void UpdateSecondaryTile(string tileId, string cityName,
            double temperature, string weatherDesc)
        {
            string xml = string.Format(
                "<tile><visual version=\"2\">" +
                "<binding template=\"TileSquare150x150Block\" branding=\"name\">" +
                "<text id=\"1\">{1}°</text><text id=\"2\">{0}</text>" +
                "</binding>" +
                "<binding template=\"TileWide310x150Text05\" branding=\"name\">" +
                "<text id=\"1\">{0}  {1}°C</text><text id=\"2\">{2}</text>" +
                "<text id=\"3\"/><text id=\"4\"/>" +
                "</binding>" +
                "</visual></tile>",
                EscapeXml(cityName), (int)temperature, EscapeXml(weatherDesc));

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

        private static string EscapeXml(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                     .Replace("\"", "&quot;").Replace("'", "&apos;");
        }
    }
}
