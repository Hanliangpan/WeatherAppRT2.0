using System;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Background;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using WeatherAppRT2._0.Cache;

namespace WeatherAppRT2._0
{
    public sealed partial class App : Application
    {
        private TransitionCollection transitions;

        public App()
        {
            this.InitializeComponent();
            this.Suspending += this.OnSuspending;
            this.UnhandledException += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine("UnhandledException: " + e.Message);
                e.Handled = true;
            };
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[App] OnLaunched 开始");
#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
            {
                this.DebugSettings.EnableFrameRateCounter = true;
            }
#endif

            Frame rootFrame = Window.Current.Content as Frame;

            if (rootFrame == null)
            {
                rootFrame = new Frame();
                rootFrame.CacheSize = 1;
                rootFrame.Language = Windows.Globalization.ApplicationLanguages.Languages[0];

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    // TODO: 从之前挂起的应用程序加载状态
                }

                Window.Current.Content = rootFrame;
            }

            if (rootFrame.Content == null)
            {
                if (rootFrame.ContentTransitions != null)
                {
                    this.transitions = new TransitionCollection();
                    foreach (var c in rootFrame.ContentTransitions)
                    {
                        this.transitions.Add(c);
                    }
                }

                rootFrame.ContentTransitions = null;
                rootFrame.Navigated += this.RootFrame_FirstNavigated;

                System.Diagnostics.Debug.WriteLine("[App] Navigate to MainPage...");
                var navigated = rootFrame.Navigate(typeof(MainPage), e.Arguments);
                System.Diagnostics.Debug.WriteLine("[App] Navigate 返回: {0}", navigated);
                if (!navigated)
                {
                    throw new Exception("Failed to create initial page");
                }
                System.Diagnostics.Debug.WriteLine("[App] MainPage 导航成功");
            }

            System.Diagnostics.Debug.WriteLine("[App] 即将调用 Window.Activate...");
            Window.Current.Activate();
            System.Diagnostics.Debug.WriteLine("[App] Window.Activate 完成");

            // 后台任务注册（不阻塞 UI）
            RegisterBackgroundTaskAsync().ContinueWith(t =>
            {
                if (t.Exception != null)
                    System.Diagnostics.Debug.WriteLine("BackgroundTask registration error: " + t.Exception);
            });
        }

        private void RootFrame_FirstNavigated(object sender, NavigationEventArgs e)
        {
            var rootFrame = sender as Frame;
            rootFrame.ContentTransitions = this.transitions ?? new TransitionCollection() { new NavigationThemeTransition() };
            rootFrame.Navigated -= this.RootFrame_FirstNavigated;
        }

        private async Task RegisterBackgroundTaskAsync()
        {
            const string taskName = "WeatherRefreshTask";
            const string taskEntryPoint = "WeatherAppRT2._0.BackgroundTasks.WeatherRefreshTask";

            System.Diagnostics.Debug.WriteLine("[App] RegisterBackgroundTaskAsync 开始...");

            // Debug 模式下先注销旧任务（确保代码更新后重新注册）
            bool needReRegister = false;
            foreach (var task in BackgroundTaskRegistration.AllTasks)
            {
                if (task.Value.Name == taskName)
                {
                    System.Diagnostics.Debug.WriteLine("[App] 后台任务已注册: " + taskName);
#if DEBUG
                    System.Diagnostics.Debug.WriteLine("[App] [DEBUG] 注销旧任务以重新注册最新代码...");
                    task.Value.Unregister(true);
                    needReRegister = true;
#else
                    return;
#endif
                }
            }

            if (!needReRegister)
            {
                System.Diagnostics.Debug.WriteLine("[App] 后台任务未注册，准备注册...");
            }

            // 请求后台运行权限
            try
            {
                var access = await BackgroundExecutionManager.RequestAccessAsync();
                System.Diagnostics.Debug.WriteLine("[App] BackgroundAccess: " + access);
                if (access == BackgroundAccessStatus.Denied)
                {
                    System.Diagnostics.Debug.WriteLine("[App] 后台任务权限被拒绝！");
                    return;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[App] RequestAccess 异常: " + ex.GetType().Name + ": " + ex.Message);
                return;
            }

            System.Diagnostics.Debug.WriteLine("[App] 构建 BackgroundTaskBuilder...");
            var builder = new BackgroundTaskBuilder
            {
                Name = taskName,
                TaskEntryPoint = taskEntryPoint
            };

            // 从用户设置读取刷新间隔（没有则使用 AppConfig 默认值）
            var settings = await CacheManager.LoadSettingsAsync();
            uint interval = (uint)settings.GetRefreshInterval();
            System.Diagnostics.Debug.WriteLine(string.Format("[App] 用户设置间隔={0}min, AppConfig默认={1}min",
                settings.RefreshIntervalMinutes > 0 ? settings.RefreshIntervalMinutes.ToString() : "未设置",
                AppConfig.BackgroundRefreshMinutes));

            if (interval < 15)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("[App] 间隔 {0}min 小于最小 15min，强制设为 15min", interval));
                interval = 15;
            }
            System.Diagnostics.Debug.WriteLine(string.Format("[App] TimeTrigger interval={0}min", interval));
            builder.SetTrigger(new TimeTrigger(interval, false));
            builder.AddCondition(new SystemCondition(SystemConditionType.InternetAvailable));

            try
            {
                var registration = builder.Register();
                System.Diagnostics.Debug.WriteLine(string.Format(
                    "[App] 后台任务注册成功: {0}, 间隔={1}min, DebugMode={2}",
                    taskName, AppConfig.BackgroundRefreshMinutes, AppConfig.DebugMode));
                System.Diagnostics.Debug.WriteLine("[App] >>> 后台任务已注册，等待 TimeTrigger 触发...");
                System.Diagnostics.Debug.WriteLine("[App] >>> 提示: 退到后台/挂起应用后，TimeTrigger 才会触发");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[App] 后台任务注册失败: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            deferral.Complete();
        }
    }
}
