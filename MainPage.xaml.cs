using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;
using Windows.Phone.UI.Input;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;
using WeatherAppRT2._0.Cache;
using WeatherAppRT2._0.Models;
using WeatherAppRT2._0.Services;
using WeatherAppRT2._0.ViewModels;

namespace WeatherAppRT2._0
{
    public sealed partial class MainPage : Page
    {
        private MainViewModel _vm;
        private bool _isFirstNavigation = true;

        /// <summary>搜索页选择城市后通过此静态属性传递参数，然后用 GoBack 返回</summary>
        public static CityNavigateParam PendingCityParam { get; set; }

        public MainPage()
        {
            System.Diagnostics.Debug.WriteLine("[MainPage] Constructor 开始");
            this.InitializeComponent();
            System.Diagnostics.Debug.WriteLine("[MainPage] InitializeComponent 完成");
            _vm = new MainViewModel();
            this.DataContext = _vm;
            System.Diagnostics.Debug.WriteLine("[MainPage] ViewModel 已设置");

            HardwareButtons.BackPressed += OnBackPressed;
            System.Diagnostics.Debug.WriteLine("[MainPage] Constructor 完成");
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            System.Diagnostics.Debug.WriteLine("[MainPage] OnNavigatedTo 开始, isFirst={0}", _isFirstNavigation);

            // 检查是否从城市搜索页返回（通过静态 PendingCityParam）
            var cityParam = PendingCityParam;
            PendingCityParam = null; // 消费掉

            if (cityParam != null)
            {
                // 同城市去重：如果当前已显示该城市，跳过 API 请求
                if (string.Equals(_vm.CityName, cityParam.CityName, StringComparison.OrdinalIgnoreCase))
                {
                    System.Diagnostics.Debug.WriteLine("[MainPage] 城市未变化 ({0})，跳过切换", cityParam.CityName);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[MainPage] 从搜索页返回，切换城市: {0}", cityParam.CityName);
                    _isFirstNavigation = false; // 防止后续 InitializeAsync 被意外触发
                    _vm.SwitchToCityAsync(cityParam.CityId, cityParam.CityName).ContinueWith(t =>
                    {
                        if (t.Exception != null)
                            System.Diagnostics.Debug.WriteLine("[MainPage] 切换城市异常: " + t.Exception.InnerException?.Message);
                        else
                            System.Diagnostics.Debug.WriteLine("[MainPage] 切换城市完成，刷新图表");

                        // SwitchToCityAsync 已经完成了 API 请求和 UI 更新，
                        // 这里只需要刷新折线图，不需要再发起 RefreshAsync
                        var ignored = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => RefreshChart());
                    });
                }
            }
            else if (_isFirstNavigation)
            {
                _isFirstNavigation = false;
                System.Diagnostics.Debug.WriteLine("[MainPage] 首次启动，fire-and-forget InitializeAsync...");
                _vm.InitializeAsync().ContinueWith(t =>
                {
                    if (t.Exception != null)
                    {
                        System.Diagnostics.Debug.WriteLine("[MainPage] 初始化异常: " + t.Exception.InnerException?.Message);
                        _vm.IsLoading = false;
                        _vm.HasError = true;
                        _vm.ErrorMessage = "启动失败，请检查网络后点击刷新";
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[MainPage] InitializeAsync 完成，刷新图表");
                    }
                    var ignored = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => RefreshChart());
                });

                // 首次启动：引导用户设置锁屏天气
                PromptLockScreenSetupAsync();
            }
            else
            {
                // 非首次导航且无城市切换参数（例如从后台恢复），仅刷新图表
                System.Diagnostics.Debug.WriteLine("[MainPage] 非首次导航，仅刷新图表");
                RefreshChart();
            }

            System.Diagnostics.Debug.WriteLine("[MainPage] OnNavigatedTo 完成（异步操作在后台进行）");
        }

        private void OnBackPressed(object sender, BackPressedEventArgs e)
        {
            var frame = Window.Current.Content as Frame;
            if (frame != null && frame.CanGoBack)
            {
                frame.GoBack();
                e.Handled = true;
            }
        }

        #region 按钮事件

        private DateTime _lastRefreshClick = DateTime.MinValue;
        private const int RefreshDebounceMs = 2000; // 2秒内不重复触发

        private async void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            // 去抖动：2秒内的重复点击直接忽略
            var now = DateTime.Now;
            if ((now - _lastRefreshClick).TotalMilliseconds < RefreshDebounceMs)
            {
                System.Diagnostics.Debug.WriteLine("[MainPage] OnRefreshClick 去抖动忽略（距上次 {0:0}ms）",
                    (now - _lastRefreshClick).TotalMilliseconds);
                return;
            }
            _lastRefreshClick = now;

            System.Diagnostics.Debug.WriteLine("[MainPage] OnRefreshClick 触发刷新");
            await _vm.RefreshAsync();
        }

        private void OnSearchClick(object sender, RoutedEventArgs e)
        {
            var frame = Window.Current.Content as Frame;
            if (frame != null)
                frame.Navigate(typeof(CitySearchPage));
        }

        private void OnCitySelectClick(object sender, RoutedEventArgs e)
        {
            OnSearchClick(sender, e);
        }

        private async void OnPinTileClick(object sender, RoutedEventArgs e)
        {
            if (_vm.CurrentWeatherData == null) return;

            try
            {
                await TileService.PinCityTile(
                    _vm.CityName,
                    _vm.CurrentTemp,
                    _vm.WeatherDesc);
            }
            catch (Exception ex)
            {
                var dialog = new MessageDialog("固定磁贴失败: " + ex.Message);
                await dialog.ShowAsync();
            }
        }

        /// <summary>首次启动时引导用户设置锁屏天气</summary>
        private async void PromptLockScreenSetupAsync()
        {
            try
            {
                const string key = "LockScreenPrompted";
                var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
                if (settings.Values.ContainsKey(key))
                {
                    System.Diagnostics.Debug.WriteLine("[MainPage] 已提示过锁屏设置，跳过");
                    return;
                }

                settings.Values[key] = true;

                // 延迟一下，等 UI 加载完毕
                await System.Threading.Tasks.Task.Delay(1500);

                var dialog = new MessageDialog(
                    "想让锁屏上显示当前温度吗？\n\n" +
                    "1. 点击下方\"前往设置\"打开锁屏设置\n" +
                    "2. 选择\"选择快速状态的应用\"\n" +
                    "3. 找到\"晴雨表\"并勾选\n" +
                    "4. 设置完成后返回应用刷新天气即可",
                    "锁屏天气");

                dialog.Commands.Add(new UICommand("前往设置", cmd => { OpenLockScreenSettings(); }));
                dialog.Commands.Add(new UICommand("以后再说"));

                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[MainPage] 锁屏引导异常: " + ex.Message);
            }
        }

        private async void OpenLockScreenSettings()
        {
            try
            {
                // WP8.1 URI scheme: ms-settings-lock: 跳转到锁屏设置
                await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings-lock:"));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[MainPage] 打开锁屏设置失败: " + ex.Message);
            }
        }

        #endregion

        #region 温度折线图

        private void OnTempChartSizeChanged(object sender, SizeChangedEventArgs e)
        {
            DrawTempChart();
        }

        private void DrawTempChart()
        {
            if (TempChartCanvas == null || _vm.HourlyItems == null || _vm.HourlyItems.Count == 0)
                return;

            TempChartCanvas.Children.Clear();

            var w = TempChartCanvas.ActualWidth;
            var h = TempChartCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;

            var temps = _vm.HourlyItems.Select(x => x.Temperature).ToList();
            double minT = temps.Min();
            double maxT = temps.Max();
            double range = maxT - minT;
            if (range < 1) range = 1;

            double margin = 16;
            double chartW = w - margin * 2;
            double chartH = h - margin * 2;

            var points = new PointCollection();
            for (int i = 0; i < temps.Count; i++)
            {
                double x = margin + (chartW / (temps.Count - 1)) * i;
                double y = margin + chartH - ((temps[i] - minT) / range * chartH);
                points.Add(new Point(x, y));
            }

            // 折线
            var polyline = new Polyline
            {
                Points = points,
                Stroke = new SolidColorBrush(Color.FromArgb(255, 255, 149, 0)),
                StrokeThickness = 2
            };
            TempChartCanvas.Children.Add(polyline);

            // 标签：最高/最低温度
            AddChartLabel(0, margin - 2, string.Format("{0}°", (int)maxT), "#FFFF9500");
            AddChartLabel(0, h - margin + 2, string.Format("{0}°", (int)minT), "#FF4FC3F7");
        }

        private void AddChartLabel(double x, double y, string text, string colorHex)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontSize = 11,
                Foreground = ParseColor(colorHex)
            };
            Canvas.SetLeft(tb, x);
            Canvas.SetTop(tb, y - 6);
            TempChartCanvas.Children.Add(tb);
        }

        private static SolidColorBrush ParseColor(string hex)
        {
            var a = byte.Parse(hex.Substring(1, 2), System.Globalization.NumberStyles.HexNumber);
            var r = byte.Parse(hex.Substring(3, 2), System.Globalization.NumberStyles.HexNumber);
            var g = byte.Parse(hex.Substring(5, 2), System.Globalization.NumberStyles.HexNumber);
            var b = byte.Parse(hex.Substring(7, 2), System.Globalization.NumberStyles.HexNumber);
            return new SolidColorBrush(Color.FromArgb(a, r, g, b));
        }

        #endregion

        #region 逐日详情 Popup

        private void OnDayClick(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;
            var day = btn.DataContext as DailyForecast;
            if (day == null) return;

            PopupDayTitle.Text = string.Format("{0}  详情", day.DayOfWeek);
            PopupDetailPanel.Children.Clear();

            AddPopupRow("天气", day.WeatherDesc);
            AddPopupRow("最高温", string.Format("{0}°", (int)day.TempMax));
            AddPopupRow("最低温", string.Format("{0}°", (int)day.TempMin));
            AddPopupRow("湿度", string.Format("{0}%", day.Humidity));
            AddPopupRow("风力", string.Format("{0} {1}级", day.WindDirection, day.WindScale));
            AddPopupRow("降雨概率", string.Format("{0}%", day.PrecipitationProbability));
            AddPopupRow("日出", day.Sunrise ?? "—");
            AddPopupRow("日落", day.Sunset ?? "—");
            AddPopupRow("紫外线", day.UvIndex.ToString());

            DayDetailPopup.IsOpen = true;
        }

        private void AddPopupRow(string label, string value)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.Margin = new Thickness(0, 4, 0, 4);

            var lbl = new TextBlock { Text = label, FontSize = 14, Foreground = new SolidColorBrush(Color.FromArgb(255, 136, 136, 136)) };
            Grid.SetColumn(lbl, 0);
            grid.Children.Add(lbl);

            var val = new TextBlock { Text = value, FontSize = 14, Foreground = new SolidColorBrush(Colors.White) };
            Grid.SetColumn(val, 1);
            grid.Children.Add(val);

            PopupDetailPanel.Children.Add(grid);
        }

        #endregion

        /// <summary>页面数据加载后绘制温度折线图</summary>
        public void RefreshChart()
        {
            DrawTempChart();
        }
    }

    /// <summary>城市搜索页 → 主页 导航参数</summary>
    public class CityNavigateParam
    {
        public string CityId { get; set; }
        public string CityName { get; set; }
    }
}
