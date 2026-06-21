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

            HardwareButtons.BackPressed += OnBackPressed;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _settings = await CacheManager.LoadSettingsAsync();
            RefreshSavedCitiesList();
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

                // 标记已收藏
                var savedIds = new HashSet<string>(
                    (_settings?.SavedCities ?? new List<CityInfo>()).Select(c => c.Id));

                foreach (var r in results)
                    r.IsSaved = savedIds.Contains(r.Id);

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

        #region 搜索结果点击

        private async void OnSearchResultClick(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;
            var city = btn.DataContext as CitySearchResult;
            if (city == null) return;

            // 防重复
            if (city.IsSaved)
            {
                var dlg = new MessageDialog(string.Format("{0} 已在收藏列表中", city.Name));
                await dlg.ShowAsync();
                return;
            }

            // 添加到收藏
            var cityInfo = new CityInfo
            {
                Id = city.Id,
                Name = city.Name,
                Adm1 = city.Adm1,
                Country = city.Country,
                Latitude = city.Latitude,
                Longitude = city.Longitude,
                IsCurrentLocation = false
            };

            if (_settings.SavedCities == null)
                _settings.SavedCities = new List<CityInfo>();

            // 去重检查
            if (!_settings.SavedCities.Any(c => c.Id == city.Id))
                _settings.SavedCities.Add(cityInfo);

            // 设为默认城市
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

        #endregion

        #region 已收藏城市

        private void RefreshSavedCitiesList()
        {
            SavedCitiesList.ItemsSource = _settings?.SavedCities ?? new List<CityInfo>();
        }

        private async void OnSavedCityClick(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;
            var city = btn.DataContext as CityInfo;
            if (city == null) return;

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
            if (btn == null) return;
            var city = btn.DataContext as CityInfo;
            if (city == null) return;

            // 确认删除
            var dialog = new MessageDialog(
                string.Format("确定删除 {0}？", city.Name), "删除城市");
            dialog.Commands.Add(new UICommand("删除") { Id = 0 });
            dialog.Commands.Add(new UICommand("取消") { Id = 1 });
            dialog.DefaultCommandIndex = 1;

            var result = await dialog.ShowAsync();
            if ((int)result.Id == 0)
            {
                _settings.SavedCities.RemoveAll(c => c.Id == city.Id);
                await CacheManager.SaveSettingsAsync(_settings);
                RefreshSavedCitiesList();
            }
        }

        #endregion
    }
}
