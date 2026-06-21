using System;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Background;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

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

            // 检查是否已注册
            foreach (var task in BackgroundTaskRegistration.AllTasks)
            {
                if (task.Value.Name == taskName)
                    return;
            }

            // 请求后台运行权限
            try
            {
                var access = await BackgroundExecutionManager.RequestAccessAsync();
                if (access == BackgroundAccessStatus.Denied)
                    return;
            }
            catch
            {
                return;
            }

            var builder = new BackgroundTaskBuilder
            {
                Name = taskName,
                TaskEntryPoint = taskEntryPoint
            };

            builder.SetTrigger(new TimeTrigger(15, false));
            builder.AddCondition(new SystemCondition(SystemConditionType.InternetAvailable));
            builder.Register();
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            deferral.Complete();
        }
    }
}
