using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Phone.UI.Input;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using WeatherAppRT2._0.ApiClient;
using WeatherAppRT2._0.Cache;
using WeatherAppRT2._0.Models;

namespace WeatherAppRT2._0
{
    public sealed partial class CitySearchPage : Page
    {
        private readonly QWeatherClient _apiClient;
        private AppSettings _settings;
        private DispatcherTimer _debounceTimer;
        private CancellationTokenSource _searchCts;

        public CitySearchPage()
        {
            this.InitializeComponent();
            _apiClient = new QWeatherClient();

            _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _debounceTimer.Tick += OnDebounceTick;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            HardwareButtons.BackPressed += OnBackPressed;
            _settings = await CacheManager.LoadSettingsAsync();
            System.Diagnostics.Debug.WriteLine("[SearchPage] OnNavigatedTo, 已加载设置, 搜索历史={0}", _settings?.SearchHistory?.Count ?? 0);
            RefreshSavedCitiesList();
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

        #region 搜索 — 300ms debounce

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        private async void OnDebounceTick(object sender, object e)
        {
            _debounceTimer.Stop();
            var keyword = SearchBox.Text?.Trim();
            if (string.IsNullOrEmpty(keyword))
            {
                SearchResultsList.ItemsSource = null;
                SearchResultHeader.Visibility = Visibility.Collapsed;
                return;
            }

            // 取消前一个搜索
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;

            try
            {
                var results = await _apiClient.SearchCityAsync(keyword);
                if (token.IsCancellationRequested) return;

                // 标记是否在搜索历史中
                var historyIds = new HashSet<string>(
                    (_settings?.SearchHistory ?? new List<CityInfo>()).Select(c => c.Id));

                foreach (var r in results)
                    r.IsSaved = historyIds.Contains(r.Id);

                SearchResultsList.ItemsSource = results;
                SearchResultHeader.Visibility = results.Count > 0
                    ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                    System.Diagnostics.Debug.WriteLine("Search error: " + ex.Message);
            }
        }

        #endregion

        #region 搜索结果点击 — 自动加入搜索历史

        private async void OnSearchResultClick(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;
            var city = btn.DataContext as CitySearchResult;
            if (city == null) return;

            System.Diagnostics.Debug.WriteLine("[Search] 点击城市: {0}, Id={1}", city.Name, city.Id);

            // 初始化搜索历史
            if (_settings.SearchHistory == null)
                _settings.SearchHistory = new List<CityInfo>();

            // 去重：如果已存在，移到最前面
            _settings.SearchHistory.RemoveAll(c => c.Id == city.Id);

            // 限制最多10条
            while (_settings.SearchHistory.Count >= 10)
            {
                _settings.SearchHistory.RemoveAt(_settings.SearchHistory.Count - 1);
            }

            // 插入到最前面
            _settings.SearchHistory.Insert(0, new CityInfo
            {
                Id = city.Id,
                Name = city.Name,
                Adm1 = city.Adm1,
                Country = city.Country,
                Latitude = city.Latitude,
                Longitude = city.Longitude,
                IsCurrentLocation = false
            });

            System.Diagnostics.Debug.WriteLine("[Search] 搜索历史更新, 共{0}条", _settings.SearchHistory.Count);

            // 设为默认城市
            _settings.DefaultCityId = city.Id;
            _settings.DefaultCityName = city.Name;
            await CacheManager.SaveSettingsAsync(_settings);
            System.Diagnostics.Debug.WriteLine("[Search] 设置已保存, DefaultCity={0}", _settings.DefaultCityName);

            // 通过静态属性传递参数，然后 GoBack 返回
            MainPage.PendingCityParam = new CityNavigateParam
            {
                CityId = city.Id,
                CityName = city.Name
            };

            var frame = Window.Current.Content as Frame;
            if (frame != null && frame.CanGoBack)
                frame.GoBack();
        }

        #endregion

        #region 搜索历史

        private async void RefreshSavedCitiesList()
        {
            var source = _settings?.SearchHistory;
            var count = source?.Count ?? 0;

            // 先清空
            SavedCitiesList.ItemsSource = null;

            if (count > 0)
            {
                // 延迟一帧再绑定新列表（新建 List 避免 ItemsControl 认为是同一引用而跳过刷新）
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    SavedCitiesList.ItemsSource = new List<CityInfo>(source);
                });
            }

            HistoryHeader.Visibility = count > 0
                ? Visibility.Visible : Visibility.Collapsed;
            System.Diagnostics.Debug.WriteLine("[Search] RefreshSavedCitiesList: history count={0}", count);
        }

        private async void OnSavedCityClick(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;
            var city = btn.DataContext as CityInfo;
            if (city == null) return;

            // 点击后提到最前面
            if (_settings.SearchHistory != null)
            {
                _settings.SearchHistory.RemoveAll(c => c.Id == city.Id);
                _settings.SearchHistory.Insert(0, city);
            }

            // 设为默认城市并返回
            _settings.DefaultCityId = city.Id;
            _settings.DefaultCityName = city.Name;
            await CacheManager.SaveSettingsAsync(_settings);

            // 通过静态属性传递参数，然后 GoBack 返回
            MainPage.PendingCityParam = new CityNavigateParam
            {
                CityId = city.Id,
                CityName = city.Name
            };

            var frame = Window.Current.Content as Frame;
            if (frame != null && frame.CanGoBack)
                frame.GoBack();
        }

        private async void OnDeleteCityClick(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            System.Diagnostics.Debug.WriteLine("[Search] OnDeleteCityClick: btn={0}", btn != null);
            if (btn == null) return;
            var city = btn.DataContext as CityInfo;
            System.Diagnostics.Debug.WriteLine("[Search] OnDeleteCityClick: city={0}, Id={1}", city?.Name, city?.Id);
            if (city == null) return;

            // 直接从搜索历史中删除（无需确认弹窗）
            var removed = _settings.SearchHistory?.RemoveAll(c => c.Id == city.Id) ?? 0;
            System.Diagnostics.Debug.WriteLine("[Search] 删除了{0}条, 剩余{1}条", removed, _settings.SearchHistory?.Count ?? 0);
            await CacheManager.SaveSettingsAsync(_settings);
            RefreshSavedCitiesList();
        }

        #endregion
    }
}
