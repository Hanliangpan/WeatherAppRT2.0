using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.UI.Xaml;
using WeatherAppRT2._0.ApiClient;
using WeatherAppRT2._0.Cache;
using WeatherAppRT2._0.Converters;
using WeatherAppRT2._0.Models;
using WeatherAppRT2._0.Services;

namespace WeatherAppRT2._0.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly QWeatherClient _apiClient;
        private AppSettings _settings;
        private CancellationTokenSource _refreshCts;
        private int _isRefreshing = 0; // 0=空闲, 1=正在刷新
        private DateTime _lastManualRefresh = DateTime.MinValue;

        /// <summary>距上次手动刷新已过分钟数</summary>
        public double CooldownMinutesRemaining
        {
            get
            {
                var elapsed = (DateTime.Now - _lastManualRefresh).TotalMinutes;
                return Math.Max(0, AppConfig.ManualRefreshCooldownMinutes - elapsed);
            }
        }

        public MainViewModel()
        {
            _apiClient = new QWeatherClient();
            HourlyItems = new ObservableCollection<HourlyForecast>();
            DailyItems = new ObservableCollection<DailyForecast>();
            DetailCards = new ObservableCollection<DetailCard>();
            RefreshCommand = new RelayCommand(async () => await RefreshAsync());
            SearchCityCommand = new RelayCommand(() =>
            {
                var frame = Windows.UI.Xaml.Window.Current.Content as Windows.UI.Xaml.Controls.Frame;
                if (frame != null) frame.Navigate(typeof(CitySearchPage));
            });
        }

        #region 绑定属性 — 当前天气

        private string _cityName = "加载中...";
        public string CityName { get { return _cityName; } set { Set(ref _cityName, value); } }

        private double _currentTemp;
        public double CurrentTemp { get { return _currentTemp; } set { Set(ref _currentTemp, value); } }

        private string _weatherIcon = "999";
        public string WeatherIcon { get { return _weatherIcon; } set { Set(ref _weatherIcon, value); } }

        private string _weatherDesc = "—";
        public string WeatherDesc { get { return _weatherDesc; } set { Set(ref _weatherDesc, value); } }

        private CurrentWeather _currentWeather;
        public CurrentWeather CurrentWeatherData
        {
            get { return _currentWeather; }
            set { Set(ref _currentWeather, value); }
        }

        private double _feelsLike;
        public double FeelsLike { get { return _feelsLike; } set { Set(ref _feelsLike, value); } }

        private int _humidity;
        public int Humidity { get { return _humidity; } set { Set(ref _humidity, value); } }

        private string _windInfo = "—";
        public string WindInfo { get { return _windInfo; } set { Set(ref _windInfo, value); } }

        private int _pressure;
        public int Pressure { get { return _pressure; } set { Set(ref _pressure, value); } }

        private double _visibility;
        public double Visibility { get { return _visibility; } set { Set(ref _visibility, value); } }

        private string _sunrise = "—";
        public string Sunrise { get { return _sunrise; } set { Set(ref _sunrise, value); } }

        private double _tempHigh;
        public double TempHigh { get { return _tempHigh; } set { Set(ref _tempHigh, value); } }

        private double _tempLow;
        public double TempLow { get { return _tempLow; } set { Set(ref _tempLow, value); } }

        private DateTime _fetchedAt = DateTime.Now;
        public DateTime FetchedAt { get { return _fetchedAt; } set { Set(ref _fetchedAt, value); } }

        private bool _isLoading = true;
        public bool IsLoading { get { return _isLoading; } set { Set(ref _isLoading, value); } }

        private bool _hasError;
        public bool HasError { get { return _hasError; } set { Set(ref _hasError, value); } }

        private string _errorMessage;
        public string ErrorMessage { get { return _errorMessage; } set { Set(ref _errorMessage, value); } }

        #endregion

        #region 集合

        private ObservableCollection<HourlyForecast> _hourlyItems;
        public ObservableCollection<HourlyForecast> HourlyItems
        {
            get { return _hourlyItems; }
            set { Set(ref _hourlyItems, value); }
        }

        private ObservableCollection<DailyForecast> _dailyItems;
        public ObservableCollection<DailyForecast> DailyItems
        {
            get { return _dailyItems; }
            set { Set(ref _dailyItems, value); }
        }

        private ObservableCollection<DetailCard> _detailCards;
        public ObservableCollection<DetailCard> DetailCards
        {
            get { return _detailCards; }
            set { Set(ref _detailCards, value); }
        }

        #endregion

        #region 逐时降雨提示

        private RainInfo _rainInfo;
        public RainInfo RainInfo { get { return _rainInfo; } set { Set(ref _rainInfo, value); } }

        #endregion

        #region 每周摘要

        private string _weekSummary = "本周天气概览";
        public string WeekSummary { get { return _weekSummary; } set { Set(ref _weekSummary, value); } }

        #endregion

        #region 命令

        public ICommand RefreshCommand { get; set; }
        public ICommand SearchCityCommand { get; set; }

        #endregion

        #region 初始化与刷新

        public async Task InitializeAsync()
        {
            System.Diagnostics.Debug.WriteLine("[VM] InitializeAsync 开始");

            _settings = await CacheManager.LoadSettingsAsync();
            System.Diagnostics.Debug.WriteLine("[VM] Settings loaded: DefaultCity={0} ({1})",
                _settings?.DefaultCityName, _settings?.DefaultCityId);

            // Task 5: 确保默认城市在收藏列表中
            if (_settings != null)
            {
                if (_settings.SavedCities == null)
                    _settings.SavedCities = new List<CityInfo>();

                // 如果收藏列表为空，自动添加默认城市
                if (_settings.SavedCities.Count == 0)
                {
                    _settings.SavedCities.Add(new CityInfo
                    {
                        Id = AppConfig.DefaultCityId,
                        Name = AppConfig.DefaultCityName,
                        Latitude = AppConfig.DefaultLatitude,
                        Longitude = AppConfig.DefaultLongitude,
                        IsCurrentLocation = false
                    });
                    await CacheManager.SaveSettingsAsync(_settings);
                    System.Diagnostics.Debug.WriteLine("[VM] 已将默认城市 {0} 添加到收藏列表", AppConfig.DefaultCityName);
                }
            }

            // 先显示缓存
            bool hasCachedData = false;
            var cached = await CacheManager.GetStaleCachedWeatherAsync();
            if (cached != null)
            {
                System.Diagnostics.Debug.WriteLine("[VM] 显示缓存数据: {0}", cached.City?.Name);
                UpdateFromWeatherData(cached);
                hasCachedData = true;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[VM] 无缓存数据");
            }

            // 无缓存时立即填充 Mock 数据，确保 UI 先渲染
            if (!hasCachedData)
            {
                System.Diagnostics.Debug.WriteLine("[VM] 填充 Mock 兜底数据");
                UpdateFromWeatherData(GetMockWeatherData());
            }

            // 关键修复：Mock 数据已填充，立即关闭 Loading 遮罩
            // 否则 IsLoading=true 会导致 Loading overlay 一直显示
            IsLoading = false;
            System.Diagnostics.Debug.WriteLine("[VM] IsLoading=false, UI 应已可见");

            // 关键修复：fire-and-forget 异步刷新，不阻塞 OnNavigatedTo 和 UI 渲染
            // 之前 await RefreshAsync() 会阻塞整个页面加载流程，
            // 导致即使有 Mock 数据，UI 也无法渲染
            System.Diagnostics.Debug.WriteLine("[VM] 启动后台刷新...");
            var _ = RefreshAsync().ContinueWith(t =>
            {
                if (t.Exception != null)
                    System.Diagnostics.Debug.WriteLine("[VM] 后台刷新异常: " + t.Exception.InnerException?.Message);
            });
            System.Diagnostics.Debug.WriteLine("[VM] InitializeAsync 完成（Mock 已显示，API 后台刷新中）");
        }

        public async Task RefreshAsync([System.Runtime.CompilerServices.CallerMemberName] string caller = "")
        {
            // 防重入：如果已经在刷新，跳过
            if (Interlocked.CompareExchange(ref _isRefreshing, 1, 0) != 0)
            {
                System.Diagnostics.Debug.WriteLine("[VM] RefreshAsync 跳过（已有刷新在进行中） caller={0}", caller);
                return;
            }

            // Task 4: 手动刷新冷却检查（非初始化调用时）
            bool isManual = caller != "InitializeAsync";
            if (isManual && _lastManualRefresh != DateTime.MinValue)
            {
                var elapsed = (DateTime.Now - _lastManualRefresh).TotalMinutes;
                if (elapsed < AppConfig.ManualRefreshCooldownMinutes)
                {
                    int remaining = (int)Math.Ceiling(AppConfig.ManualRefreshCooldownMinutes - elapsed);
                    System.Diagnostics.Debug.WriteLine("[VM] 冷却中，剩余 {0} 分钟", remaining);
                    HasError = true;
                    ErrorMessage = string.Format("请等待 {0} 分钟后再刷新", remaining);
                    Interlocked.Exchange(ref _isRefreshing, 0);
                    return;
                }
            }

            System.Diagnostics.Debug.WriteLine("[VM] RefreshAsync 开始 caller={0}", caller);
            IsLoading = true;
            HasError = false;

            // 取消之前的 RefreshAsync（防重复调用）
            _refreshCts?.Cancel();
            _refreshCts = new CancellationTokenSource();
            var token = _refreshCts.Token;

            try
            {
                // 直接使用默认/收藏城市（跳过 GPS）
                var cityId = _settings?.DefaultCityId ?? AppConfig.DefaultCityId;
                var cityName = _settings?.DefaultCityName ?? AppConfig.DefaultCityName;
                System.Diagnostics.Debug.WriteLine("[VM] 请求天气: {0} ({1})", cityName, cityId);

                // 加 15 秒超时保护
                System.Diagnostics.Debug.WriteLine("[VM] 发起 API 请求...");
                var apiTask = _apiClient.GetFullWeatherByCityIdAsync(cityId, cityName);
                var timeoutTask = Task.Delay(15000);
                var completed = await Task.WhenAny(apiTask, timeoutTask);

                if (token.IsCancellationRequested)
                {
                    System.Diagnostics.Debug.WriteLine("[VM] RefreshAsync 被取消（新请求已覆盖）");
                    Interlocked.Exchange(ref _isRefreshing, 0);
                    return;
                }

                System.Diagnostics.Debug.WriteLine("[VM] Task.WhenAny 返回: completed==timeoutTask? {0}", completed == timeoutTask);

                if (completed == timeoutTask)
                {
                    System.Diagnostics.Debug.WriteLine("[VM] API 请求超时 (15s)");
                    throw new TimeoutException("网络请求超时，请检查网络连接");
                }

                var weather = apiTask.Result;
                System.Diagnostics.Debug.WriteLine("[VM] 天气数据获取成功: temp={0}", weather?.Current?.Temperature);

                if (weather != null)
                {
                    System.Diagnostics.Debug.WriteLine("[VM] 更新 UI 数据...");
                    UpdateFromWeatherData(weather);
                    System.Diagnostics.Debug.WriteLine("[VM] 保存缓存...");
                    await CacheManager.SaveToCacheAsync(weather);
                    System.Diagnostics.Debug.WriteLine("[VM] 数据已缓存");

                    // 同步更新磁贴和锁屏（Badge + 详细状态文字）
                    TileService.UpdatePrimaryTile(weather);
                }
            }
            catch (Exception ex)
            {
                if (token.IsCancellationRequested)
                {
                    System.Diagnostics.Debug.WriteLine("[VM] RefreshAsync 取消后异常被忽略");
                    Interlocked.Exchange(ref _isRefreshing, 0);
                    return;
                }
                System.Diagnostics.Debug.WriteLine("[VM] Refresh error: " + ex.GetType().Name + ": " + ex.Message);
                if (ex.InnerException != null)
                    System.Diagnostics.Debug.WriteLine("[VM] Inner: " + ex.InnerException.GetType().Name + ": " + ex.InnerException.Message);
                HasError = true;
                ErrorMessage = "加载失败: " + ex.Message;

                // 尝试加载过期缓存
                System.Diagnostics.Debug.WriteLine("[VM] 尝试加载过期缓存...");
                var stale = await CacheManager.GetStaleCachedWeatherAsync();
                if (stale != null)
                {
                    System.Diagnostics.Debug.WriteLine("[VM] 使用过期缓存");
                    UpdateFromWeatherData(stale);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[VM] 无过期缓存可用");
                }
            }
            finally
            {
                IsLoading = false;
                if (isManual)
                    _lastManualRefresh = DateTime.Now;
                System.Diagnostics.Debug.WriteLine("[VM] RefreshAsync 结束, IsLoading=false");
            }

            // 延迟释放锁：防止 CommandBar 在 UI 布局期间重复触发 Click 事件
            // 配合 MainPage.OnRefreshClick 的 debounce（也是 2 秒）
            // 注意：CancellationToken.None 确保即使 refreshCts 被取消，延迟也不中断
            await Task.Delay(2000, CancellationToken.None);
            Interlocked.Exchange(ref _isRefreshing, 0);
            System.Diagnostics.Debug.WriteLine("[VM] RefreshAsync 锁已释放");
        }

        /// <summary>从城市搜索页面切换城市后调用</summary>
        public async Task SwitchToCityAsync(string cityId, string cityName)
        {
            System.Diagnostics.Debug.WriteLine("[VM] SwitchToCityAsync: {0} ({1})", cityName, cityId);
            IsLoading = true;
            HasError = false;

            // 取消正在进行的 RefreshAsync，避免重复请求
            _refreshCts?.Cancel();

            try
            {
                // CitySearchPage 已保存过 settings（含 SearchHistory），这里只更新默认城市 ID/Name 并保存一次
                var weather = await _apiClient.GetFullWeatherByCityIdAsync(cityId, cityName);
                if (weather != null)
                {
                    System.Diagnostics.Debug.WriteLine("[VM] SwitchToCity: 更新 UI...");
                    UpdateFromWeatherData(weather);
                    await CacheManager.SaveToCacheAsync(weather);

                    // 同步更新磁贴和锁屏
                    TileService.UpdatePrimaryTile(weather);

                    // 合并 settings 更新 + 保存为一次 IO（不再重复 LoadSettingsAsync）
                    _settings.DefaultCityId = cityId;
                    _settings.DefaultCityName = cityName;
                    await CacheManager.SaveSettingsAsync(_settings);
                    System.Diagnostics.Debug.WriteLine("[VM] SwitchToCity: 完成");
                }
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = "切换城市失败";
                System.Diagnostics.Debug.WriteLine("[VM] SwitchCity error: " + ex.GetType().Name + ": " + ex.Message);
                if (ex.InnerException != null)
                    System.Diagnostics.Debug.WriteLine("[VM] SwitchCity inner: " + ex.InnerException.GetType().Name + ": " + ex.InnerException.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion

        #region 数据映射

        private void UpdateFromWeatherData(WeatherData data)
        {
            if (data == null) return;

            // 成功加载数据后清除错误状态
            HasError = false;
            ErrorMessage = "";

            CityName = data.City?.Name ?? "未知";
            FetchedAt = data.FetchedAt;

            if (data.Current != null)
            {
                CurrentTemp = data.Current.Temperature;
                WeatherIcon = data.Current.WeatherIcon;
                WeatherDesc = data.Current.WeatherDesc;
                FeelsLike = data.Current.FeelsLike;
                Humidity = data.Current.Humidity;
                WindInfo = string.Format("{0} {1}级", data.Current.WindDirection, data.Current.WindScale);
                Pressure = data.Current.Pressure;
                Visibility = data.Current.Visibility;
                CurrentWeatherData = data.Current;
            }

            // 逐日预报 — 批量替换减少 UI 刷新次数
            if (data.Daily != null && data.Daily.Count > 0)
            {
                double allMin = data.Daily.Min(d => d.TempMin);
                double allMax = data.Daily.Max(d => d.TempMax);
                double allRange = allMax - allMin;
                if (allRange < 1) allRange = 1;

                foreach (var d in data.Daily)
                {
                    d.TempBarLeftRatio = (d.TempMin - allMin) / allRange;
                    d.TempBarRatio = (d.TempMax - d.TempMin) / allRange;
                }

                var newDaily = new ObservableCollection<DailyForecast>(data.Daily);
                DailyItems = newDaily;
                OnPropertyChanged(nameof(DailyItems));

                TempHigh = data.Daily[0].TempMax;
                TempLow = data.Daily[0].TempMin;
                Sunrise = data.Daily[0].Sunrise ?? "—";
                UpdateWeekSummary(data.Daily);
            }

            // 逐时预报 — 批量替换
            if (data.Hourly != null)
            {
                HourlyItems = new ObservableCollection<HourlyForecast>(data.Hourly);
                OnPropertyChanged(nameof(HourlyItems));
                CalcRainInfo(data.Hourly);
            }

            // 详情卡片
            UpdateDetailCards();
        }

        private void CalcRainInfo(List<HourlyForecast> hourly)
        {
            if (hourly == null || hourly.Count == 0)
            {
                RainInfo = new RainInfo { MaxPop = 0 };
                return;
            }

            var max = hourly.OrderByDescending(h => h.PrecipitationProbability).First();
            RainInfo = new RainInfo
            {
                MaxPop = max.PrecipitationProbability,
                MaxTime = max.Time.ToString("HH:mm")
            };
        }

        private void UpdateDetailCards()
        {
            var cards = new List<DetailCard>
            {
                new DetailCard { Title = "体感温度", Value = string.Format("{0}°", (int)FeelsLike), Icon = "\u2600" },
                new DetailCard { Title = "湿度", Value = string.Format("{0}%", Humidity), Icon = "\u2662" },
                new DetailCard { Title = "风力", Value = WindInfo, Icon = "\u263E" },
                new DetailCard { Title = "气压", Value = string.Format("{0} hPa", Pressure), Icon = "\u2195" },
                new DetailCard { Title = "能见度", Value = string.Format("{0} km", Visibility), Icon = "\u25C9" },
                new DetailCard { Title = "日出", Value = Sunrise, Icon = "\u263C" }
            };
            DetailCards = new ObservableCollection<DetailCard>(cards);
            OnPropertyChanged(nameof(DetailCards));
        }

        private void UpdateWeekSummary(List<DailyForecast> daily)
        {
            if (daily == null || daily.Count == 0)
            {
                WeekSummary = "本周天气概览";
                return;
            }

            double weekHigh = daily.Max(d => d.TempMax);
            double weekLow = daily.Min(d => d.TempMin);
            int rainDays = daily.Count(d => d.PrecipitationProbability > 50);
            var mainWeather = daily.GroupBy(d => d.WeatherDesc)
                                   .OrderByDescending(g => g.Count())
                                   .First().Key;

            string summary;
            if (rainDays > 0)
                summary = string.Format("未来{0}天 {1}为主，{2}天有雨 | {3}°~{4}°",
                    daily.Count, mainWeather, rainDays, (int)weekLow, (int)weekHigh);
            else
                summary = string.Format("未来{0}天 {1}为主 | {2}°~{3}°",
                    daily.Count, mainWeather, (int)weekLow, (int)weekHigh);

            WeekSummary = summary;
        }

        #endregion

        /// <summary>生成 Mock 天气数据，用于网络不可用时兜底显示 UI</summary>
        private WeatherData GetMockWeatherData()
        {
            var now = DateTime.Now;
            var data = new WeatherData
            {
                FetchedAt = now,
                City = new CityInfo
                {
                    Id = _settings?.DefaultCityId ?? AppConfig.DefaultCityId,
                    Name = _settings?.DefaultCityName ?? AppConfig.DefaultCityName,
                    Latitude = AppConfig.DefaultLatitude,
                    Longitude = AppConfig.DefaultLongitude
                },
                Current = new CurrentWeather
                {
                    Temperature = 25.0,
                    FeelsLike = 26.0,
                    WeatherIcon = "100",
                    WeatherDesc = "加载中…",
                    Humidity = 60,
                    WindDirection = "北",
                    WindSpeed = 10.0,
                    WindScale = 2,
                    Pressure = 1013,
                    Visibility = 10.0,
                },
                Hourly = new List<HourlyForecast>(),
                Daily = new List<DailyForecast>()
            };

            // 生成 24 小时逐时 Mock
            for (int i = 0; i < 24; i++)
            {
                var t = now.Date.AddHours(now.Hour + i);
                data.Hourly.Add(new HourlyForecast
                {
                    Time = t,
                    Temperature = 22 + Math.Sin(i * Math.PI / 12) * 6,
                    WeatherIcon = "100",
                    PrecipitationProbability = i > 12 && i < 18 ? 20 : 0,
                });
            }

            // 生成 7 天逐日 Mock
            double mockAllMin = 18, mockAllMax = 31.5, mockAllRange = mockAllMax - mockAllMin;
            for (int i = 0; i < 7; i++)
            {
                var date = now.Date.AddDays(i);
                double tMin = 18 + i * 0.3;
                double tMax = 28 + i * 0.5;
                data.Daily.Add(new DailyForecast
                {
                    Date = date,
                    DayOfWeek = GetDayOfWeek(date),
                    TempMax = tMax,
                    TempMin = tMin,
                    TempBarLeftRatio = (tMin - mockAllMin) / mockAllRange,
                    TempBarRatio = (tMax - tMin) / mockAllRange,
                    IconDay = "100",
                    IconNight = "100",
                    WeatherDesc = "晴天",
                    Sunrise = "05:30",
                    Sunset = "19:00",
                    UvIndex = 5,
                    WindDirection = "南",
                    WindScale = 2,
                    Humidity = 55,
                    PrecipitationProbability = i == 3 ? 40 : 0,
                });
            }

            return data;
        }

        private static string GetDayOfWeek(DateTime date)
        {
            if (date.Date == DateTime.Today) return "今天";
            if (date.Date == DateTime.Today.AddDays(1)) return "明天";
            if (date.Date == DateTime.Today.AddDays(2)) return "后天";
            var days = new[] { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };
            return days[(int)date.DayOfWeek];
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        private bool Set<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            RaisePropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(propertyName));
        }

        private void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            OnPropertyChanged(propertyName);
        }

        #endregion
    }

    /// <summary>详情卡片数据模型</summary>
    public class DetailCard
    {
        public string Icon { get; set; }
        public string Title { get; set; }
        public string Value { get; set; }
    }

    /// <summary>简易 RelayCommand</summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _action;

        public RelayCommand(Action action) { _action = action; }

        public bool CanExecute(object parameter) { return true; }
#pragma warning disable CS0067
        public event EventHandler CanExecuteChanged;
#pragma warning restore CS0067
        public void Execute(object parameter) { _action(); }
    }
}
