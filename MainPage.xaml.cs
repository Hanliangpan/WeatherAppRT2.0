using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
// using Windows.ApplicationModel.Background;   // DEBUG: 测试 BGTask 用
using Windows.Foundation;
using Windows.Phone.UI.Input;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;
// using WeatherAppRT2._0.BackgroundTasks;   // DEBUG: 测试 BGTask 用
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

            // Windows 10 Mobile: 尝试隐藏状态栏，让应用全屏显示
            this.Loaded += OnPageLoaded;
            this.Unloaded += OnPageUnloaded;

            System.Diagnostics.Debug.WriteLine("[MainPage] Constructor 完成");
        }

        private void OnPageUnloaded(object sender, RoutedEventArgs e)
        {
            HardwareButtons.BackPressed -= OnBackPressed;
            this.Loaded -= OnPageLoaded;
            this.Unloaded -= OnPageUnloaded;
        }

        private async void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var statusBar = StatusBar.GetForCurrentView();
                if (statusBar != null)
                {
                    // 隐藏状态栏，WP8.1 应用在 Win10 Mobile 上全屏显示
                    await statusBar.HideAsync();
                    System.Diagnostics.Debug.WriteLine("[MainPage] StatusBar hidden successfully");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[MainPage] StatusBar hide failed: " + ex.Message);
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            HardwareButtons.BackPressed += OnBackPressed;  // 导航到页面时订阅
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

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            HardwareButtons.BackPressed -= OnBackPressed;
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

        private void OnSettingsClick(object sender, RoutedEventArgs e)
        {
            var frame = Window.Current.Content as Frame;
            if (frame != null)
                frame.Navigate(typeof(SettingsPage));
        }

        private void OnDismissError(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            _vm.HasError = false;
            _vm.ErrorMessage = "";
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
                // PinCityTile 需要完整的 WeatherData，从 ViewModel 重新获取
                await TileService.PinCityTile(new Models.WeatherData
                {
                    City = new Models.CityInfo { Name = _vm.CityName },
                    Current = _vm.CurrentWeatherData
                });
            }
            catch (Exception ex)
            {
                var dialog = new MessageDialog("固定磁贴失败: " + ex.Message);
                await dialog.ShowAsync();
            }
        }

        /* DEBUG: 手动触发后台任务测试（已注释）
        private async void OnTestBgTaskClick(object sender, RoutedEventArgs e)
        {
            // ... 测试代码已注释 ...
        }
        */

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
                    "3. 找到\"MoooWeatherRT2.0\"并勾选\n" +
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

        // 缓存画刷，避免每次 DrawTempChart 重复创建
        private static readonly SolidColorBrush ChartFillBrush = new SolidColorBrush(Color.FromArgb(50, 108, 99, 255));
        private static readonly SolidColorBrush ChartLineBrush = new SolidColorBrush(Color.FromArgb(255, 157, 151, 255));
        private static readonly SolidColorBrush ChartDotBrush = new SolidColorBrush(Color.FromArgb(255, 157, 151, 255));
        private static readonly SolidColorBrush ChartTempLabelBrush = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255));
        private static readonly SolidColorBrush ChartTimeLabelBrush = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255));
        private static readonly SolidColorBrush ChartMaxLabelBrush = new SolidColorBrush(Color.FromArgb(255, 255, 109, 0));   // Orange
        private static readonly SolidColorBrush ChartMidLabelBrush = new SolidColorBrush(Color.FromArgb(102, 116, 122, 255));  // Hint
        private static readonly SolidColorBrush ChartMinLabelBrush = new SolidColorBrush(Color.FromArgb(255, 0, 229, 255));    // Cyan

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

            // 上下各留 30px 给标签，左右留 10px
            double marginTop = 30;
            double marginBottom = 30;
            double marginLeft = 10;
            double marginRight = 10;

            double chartW = w - marginLeft - marginRight;
            double chartH = h - marginTop - marginBottom;

            var points = new PointCollection();
            for (int i = 0; i < temps.Count; i++)
            {
                double x = marginLeft + (chartW / (temps.Count - 1)) * i;
                double y = marginTop + chartH - ((temps[i] - minT) / range * chartH);
                points.Add(new Point(x, y));
            }

            // 填充面积
            var fillPoints = new PointCollection();
            fillPoints.Add(new Point(points[0].X, marginTop + chartH));
            foreach (var pt in points) fillPoints.Add(pt);
            fillPoints.Add(new Point(points[points.Count - 1].X, marginTop + chartH));

            var fillPoly = new Polygon { Points = fillPoints, Fill = ChartFillBrush };
            TempChartCanvas.Children.Add(fillPoly);

            // 折线
            var polyline = new Polyline { Points = points, Stroke = ChartLineBrush, StrokeThickness = 3 };
            TempChartCanvas.Children.Add(polyline);

            // 数据点圆点 + 温度标签
            for (int i = 0; i < points.Count; i++)
            {
                var pt = points[i];
                var dot = new Ellipse { Width = 6, Height = 6, Fill = ChartDotBrush };
                Canvas.SetLeft(dot, pt.X - 2.5);
                Canvas.SetTop(dot, pt.Y - 2.5);
                TempChartCanvas.Children.Add(dot);

                bool showLabel = i == 0 || i == points.Count - 1;
                if (!showLabel && i > 0 && i < points.Count - 1)
                {
                    bool isPeak = (temps[i] > temps[i - 1] && temps[i] > temps[i + 1]);
                    bool isValley = (temps[i] < temps[i - 1] && temps[i] < temps[i + 1]);
                    showLabel = isPeak || isValley;
                }
                if (points.Count <= 8) showLabel = true;

                if (showLabel)
                {
                    var label = new TextBlock
                    {
                        Text = string.Format("{0}°", (int)temps[i]),
                        FontSize = 9,
                        Foreground = ChartTempLabelBrush
                    };
                    Canvas.SetLeft(label, pt.X - 11);
                    Canvas.SetTop(label, pt.Y - 14);
                    TempChartCanvas.Children.Add(label);
                }
            }

            // X 轴时间标注
            var items = _vm.HourlyItems;
            int[] labelIndices;
            if (items.Count <= 8)
                labelIndices = Enumerable.Range(0, items.Count).ToArray();
            else
                labelIndices = new[] { 0, items.Count / 3, items.Count * 2 / 3, items.Count - 1 };

            foreach (var idx in labelIndices)
            {
                if (idx >= points.Count) continue;
                var pt = points[idx];
                var timeLabel = new TextBlock
                {
                    Text = items[idx].Time.ToString("HH:mm"),
                    FontSize = 9,
                    Foreground = ChartTimeLabelBrush
                };
                Canvas.SetLeft(timeLabel, pt.X - 12);
                Canvas.SetTop(timeLabel, h - marginBottom + 6);
                TempChartCanvas.Children.Add(timeLabel);
            }

            // Y 轴温度标签
            double midT = (minT + maxT) / 2;
            double midY = marginTop + chartH / 2;

            AddChartLabel(0, marginTop - 6, string.Format("{0}°", (int)maxT), ChartMaxLabelBrush);
            AddChartLabel(0, midY - 5, string.Format("{0}°", (int)midT), ChartMidLabelBrush);
            AddChartLabel(0, h - marginBottom + 2, string.Format("{0}°", (int)minT), ChartMinLabelBrush);
        }

        private void AddChartLabel(double x, double y, string text, SolidColorBrush brush)
        {
            var tb = new TextBlock { Text = text, FontSize = 10, Foreground = brush };
            Canvas.SetLeft(tb, x + 2);
            Canvas.SetTop(tb, y - 6);
            TempChartCanvas.Children.Add(tb);
        }

        #endregion

        #region 逐日详情 Popup

        // 缓存 Popup 行画刷
        private static readonly SolidColorBrush PopupLabelBrush = new SolidColorBrush(Color.FromArgb(255, 100, 100, 100));
        private static readonly SolidColorBrush PopupValueBrush = new SolidColorBrush(Color.FromArgb(255, 26, 35, 126));

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
            grid.Margin = new Thickness(0, 6, 0, 6);

            var lbl = new TextBlock { Text = label, FontSize = 13, Foreground = PopupLabelBrush };
            Grid.SetColumn(lbl, 0);
            grid.Children.Add(lbl);

            var val = new TextBlock { Text = value, FontSize = 13, FontWeight = Windows.UI.Text.FontWeights.SemiLight,
                                      Foreground = PopupValueBrush };
            Grid.SetColumn(val, 1);
            grid.Children.Add(val);

            PopupDetailPanel.Children.Add(grid);
        }

        #endregion

        /// <summary>页面数据加载后绘制温度折线图</summary>
        public void RefreshChart()
        {
            DrawTempChart();
            ScrollHourlyToNow();
        }

        /// <summary>滚动逐时卡片到当前小时位置</summary>
        private void ScrollHourlyToNow()
        {
            if (HourlyScroller == null || _vm.HourlyItems == null || _vm.HourlyItems.Count == 0)
                return;

            var now = DateTime.Now;
            int nowIndex = -1;
            for (int i = 0; i < _vm.HourlyItems.Count; i++)
            {
                if (_vm.HourlyItems[i].Time.Date == now.Date &&
                    _vm.HourlyItems[i].Time.Hour >= now.Hour)
                {
                    nowIndex = i;
                    break;
                }
            }

            if (nowIndex < 0) return;

            // 每张卡片约 54px 宽（52 + 1*2 margin），滚动到当前小时略靠左
            double cardWidth = 54;
            double targetOffset = nowIndex * cardWidth - 60;
            if (targetOffset < 0) targetOffset = 0;

            HourlyScroller.ChangeView(targetOffset, null, null);
        }
    }

    /// <summary>城市搜索页 → 主页 导航参数</summary>
    public class CityNavigateParam
    {
        public string CityId { get; set; }
        public string CityName { get; set; }
    }
}
