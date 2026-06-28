using System;
using Windows.Phone.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using WeatherAppRT2._0.Cache;
using WeatherAppRT2._0.Models;

namespace WeatherAppRT2._0
{
    public sealed partial class SettingsPage : Page
    {
        private AppSettings _settings;
        private bool _initializing = true;  // 初始化期间不触发保存
        private DispatcherTimer _saveDebounceTimer;

        public SettingsPage()
        {
            this.InitializeComponent();
            // Slider 保存去抖：拖动停止 600ms 后才保存，避免连续 I/O
            _saveDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            _saveDebounceTimer.Tick += OnSaveDebounceTick;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            HardwareButtons.BackPressed += OnBackPressed;
            _settings = await CacheManager.LoadSettingsAsync();
            LoadSettingsToUI();
            _initializing = false;
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

        /// <summary>将 AppSettings 的值加载到 UI 控件</summary>
        private void LoadSettingsToUI()
        {
            // 刷新间隔
            int refreshInterval = _settings.GetRefreshInterval();
            RefreshIntervalSlider.Value = refreshInterval;
            UpdateRefreshIntervalLabel(refreshInterval);

            // 通知开关
            NotifyToggle.IsOn = _settings.EnableNotifications;

            // 通知间隔
            int notifyInterval = _settings.GetNotifyMinInterval();
            NotifyIntervalSlider.Value = notifyInterval;
            UpdateNotifyIntervalLabel(notifyInterval);

            // 温度阈值
            double tempThreshold = _settings.GetNotifyTempThreshold();
            TempThresholdSlider.Value = tempThreshold;
            UpdateTempThresholdLabel(tempThreshold);

            // 降水阈值
            int rainThreshold = _settings.GetNotifyRainThreshold();
            RainThresholdSlider.Value = rainThreshold;
            UpdateRainThresholdLabel(rainThreshold);

            // 风速阈值
            double windThreshold = _settings.GetNotifyWindThreshold();
            WindThresholdSlider.Value = windThreshold;
            UpdateWindThresholdLabel(windThreshold);

            // 通知相关控件可见性
            UpdateNotifyPanelVisibility();
        }

        #region 事件处理

        /// <summary>Slider 值改变时仅更新 UI label，延迟保存（去抖）</summary>
        private void ScheduleSave()
        {
            if (_initializing) return;
            _saveDebounceTimer.Stop();
            _saveDebounceTimer.Start();
        }

        /// <summary>去抖定时器触发：执行一次保存</summary>
        private async void OnSaveDebounceTick(object sender, object e)
        {
            _saveDebounceTimer.Stop();
            if (_settings != null)
                await CacheManager.SaveSettingsAsync(_settings);
        }

        private void OnRefreshIntervalChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_initializing || _settings == null) return;
            int val = (int)e.NewValue;
            _settings.RefreshIntervalMinutes = val;
            UpdateRefreshIntervalLabel(val);
            ScheduleSave();
        }

        private async void OnNotifyToggled(object sender, RoutedEventArgs e)
        {
            if (_initializing || _settings == null) return;
            _settings.EnableNotifications = NotifyToggle.IsOn;
            UpdateNotifyPanelVisibility();
            await CacheManager.SaveSettingsAsync(_settings);  // 开关即时保存（非连续操作）
        }

        private void OnNotifyIntervalChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_initializing || _settings == null) return;
            int val = (int)e.NewValue;
            _settings.NotifyMinIntervalMinutes = val;
            UpdateNotifyIntervalLabel(val);
            ScheduleSave();
        }

        private void OnTempThresholdChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_initializing || _settings == null) return;
            double val = Math.Round(e.NewValue);
            _settings.NotifyTempThreshold = val;
            UpdateTempThresholdLabel(val);
            ScheduleSave();
        }

        private void OnRainThresholdChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_initializing || _settings == null) return;
            int val = (int)e.NewValue;
            _settings.NotifyRainThreshold = val;
            UpdateRainThresholdLabel(val);
            ScheduleSave();
        }

        private void OnWindThresholdChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_initializing || _settings == null) return;
            double val = Math.Round(e.NewValue);
            _settings.NotifyWindThreshold = val;
            UpdateWindThresholdLabel(val);
            ScheduleSave();
        }

        #endregion

        #region UI 更新辅助

        private void UpdateRefreshIntervalLabel(int minutes)
        {
            RefreshIntervalLabel.Text = string.Format("每 {0} 分钟自动刷新天气（后台任务触发）", minutes);
        }

        private void UpdateNotifyIntervalLabel(int minutes)
        {
            if (minutes >= 60)
                NotifyIntervalLabel.Text = string.Format("两次通知之间至少间隔 {0} 小时 {1} 分钟", minutes / 60, minutes % 60);
            else
                NotifyIntervalLabel.Text = string.Format("两次通知之间至少间隔 {0} 分钟", minutes);
        }

        private void UpdateTempThresholdLabel(double val)
        {
            TempThresholdLabel.Text = string.Format("{0}°C", (int)val);
        }

        private void UpdateRainThresholdLabel(int val)
        {
            RainThresholdLabel.Text = string.Format("{0}%", val);
        }

        private void UpdateWindThresholdLabel(double val)
        {
            WindThresholdLabel.Text = string.Format("{0} km/h", (int)val);
        }

        private void UpdateNotifyPanelVisibility()
        {
            var visible = NotifyToggle.IsOn ? Visibility.Visible : Visibility.Collapsed;
            NotifyIntervalPanel.Visibility = visible;
            NotifyThresholdTitle.Visibility = visible;
            TempThresholdPanel.Visibility = visible;
            RainThresholdPanel.Visibility = visible;
            WindThresholdPanel.Visibility = visible;
        }

        #endregion
    }
}
