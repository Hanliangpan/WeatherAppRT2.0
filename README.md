# 晴雨表 WeatherAppRT 2.0

一款为 Windows Phone 8.1 (WinRT) 打造的天气应用，采用 MVVM 架构，接入 Open-Meteo 免费天气 API，支持当前天气、逐时/逐日预报、城市搜索、磁贴更新和锁屏通知。

## 核心功能

- **当前天气** — 温度、体感温度、湿度、风力风向、气压、能见度、日出时间
- **逐时预报** — 24 小时温度趋势 + 横向滑动卡片 + 温度折线图 + 降雨概率提示
- **逐日预报** — 7 天天气预报 + 温度条可视化 + 点击查看详情弹窗
- **城市搜索** — 支持中英文城市名搜索，收藏城市，GPS 定位城市
- **动态磁贴** — 主磁贴循环展示天气数据，二级磁贴固定收藏城市
- **锁屏通知** — Badge 显示温度数字，宽磁贴第一行显示详细状态文字
- **离线可用** — 本地缓存天气数据，无网络时展示过期缓存或 Mock 兜底数据
- **后台刷新** — 注册 BackgroundTask 定期拉取天气并更新磁贴

## 技术架构

```
┌─────────────────────────────────────────────────┐
│                   MainPage.xaml                  │
│   Pivot: 当前天气 | 逐时预报 | 逐日预报          │
│   CommandBar: 刷新 / 搜索 / 固定磁贴             │
├─────────────────────────────────────────────────┤
│              MainViewModel (MVVM)                │
│  INotifyPropertyChanged + ObservableCollection    │
│  RefreshCommand / SearchCityCommand              │
├──────────────┬──────────────┬────────────────────┤
│ QWeatherClient│ CacheManager │    TileService     │
│ (Open-Meteo) │ (JSON 本地)  │ (磁贴+Badge+锁屏)  │
├──────────────┴──────────────┴────────────────────┤
│         Windows Phone 8.1 WinRT Runtime          │
│  HttpClient / ApplicationData / TileNotification │
└─────────────────────────────────────────────────┘
```

## 技术栈

| 技术 | 说明 |
|------|------|
| **平台** | Windows Phone 8.1 WinRT (WPA81) |
| **语言** | C# |
| **UI** | XAML + Pivot + CommandBar |
| **架构** | MVVM (手写 ViewModel + INPC) |
| **API** | [Open-Meteo](https://open-meteo.com/) (免费，无需 Key) |
| **地理编码** | Open-Meteo Geocoding API + Nominatim (反向) |
| **JSON** | Newtonsoft.Json 6.0.8 |
| **缓存** | Windows.Storage + JSON 本地文件 |
| **后台任务** | IBackgroundTask (定时天气刷新) |
| **构建** | MSBuild 12.0 / VS2015 |

## 项目结构

```
WeatherAppRT2.0/
├── ApiClient/
│   ├── IWeatherApiClient.cs      # 天气 API 接口定义
│   └── QWeatherClient.cs        # Open-Meteo 实现 + 地理编码
├── BackgroundTasks/
│   └── WeatherRefreshTask.cs    # 后台定时刷新任务
├── Cache/
│   └── CacheManager.cs          # JSON 文件缓存 (天气+设置)
├── Converters/
│   └── WeatherConverters.cs     # XAML 值转换器
├── Helpers/
│   └── GeoHelper.cs             # GPS 定位辅助
├── Models/
│   ├── WeatherData.cs           # 数据模型 (当前/逐时/逐日/城市)
│   └── WeatherIconMapper.cs     # 天气代码→Emoji 映射
├── Services/
│   └── TileService.cs           # 磁贴/Badge/锁屏通知服务
├── ViewModels/
│   └── MainViewModel.cs         # 主 ViewModel (MVVM 核心)
├── App.xaml / App.xaml.cs       # 应用入口
├── MainPage.xaml / .cs          # 主页面 (Pivot 三页)
├── CitySearchPage.xaml / .cs    # 城市搜索页面
├── AppConfig.cs                 # 全局配置
└── Package.appxmanifest         # 应用清单
```

## 构建与部署

### 环境要求

- Visual Studio 2015 (含 Windows Phone 8.1 开发工具)
- Windows Phone 8.1 SDK

### 编译

```bash
# 命令行编译 (ARM Debug)
msbuild WeatherAppRT2.0.sln /p:Configuration=Debug /p:Platform=ARM /t:Rebuild
```

### 部署到真机

```bash
# 通过 AppDeployCmd.exe 部署 (需连接设备)
AppDeploy.exe /installlaunch WeatherAppRT2.0_1.0.0.0_ARM_Debug_Test\WeatherAppRT2.0_1.0.0.0_ARM_Debug.appx /targetdevice:0
```

## 配置说明

在 `AppConfig.cs` 中修改全局配置：

```csharp
public const int CacheMaxAgeMinutes = 15;    // 缓存有效期 (分钟)
public const int MaxCities = 10;             // 最大收藏城市数
public const string DefaultCityName = "北京"; // 默认城市
```

Open-Meteo API 免费开放，**无需注册 API Key**。

## 开发历程中的关键问题与解决方案

### 1. WP8.1 HttpClient 无限挂起
**问题**：`HttpClient.GetStringAsync` 在网络异常时不会超时，导致 UI 永久卡住。  
**方案**：使用 `Task.WhenAny(apiTask, Task.Delay(30000))` 手动实现 30 秒超时保护。

### 2. 页面加载阻塞导致白屏
**问题**：在 `OnNavigatedTo` 中 `await RefreshAsync()` 阻塞了 UI 渲染线程，页面无法显示。  
**方案**：先用 Mock 数据填充 UI → 关闭 Loading → 再用 fire-and-forget 后台刷新。

### 3. 防重入刷新锁
**问题**：CommandBar 刷新按钮在 UI 布局期间会重复触发 Click 事件。  
**方案**：`Interlocked.CompareExchange` 原子锁 + 2 秒 debounce 冷却期。

### 4. Newtonsoft.Json 6.0.8 兼容性
**问题**：WP8.1 平台仅支持 Newtonsoft.Json 6.0.8，部分 API 调用方式不同。  
**方案**：使用手写 `JToken` 解析辅助方法 `D()`/`I()`/`S()`，避免依赖 `Value<T>()` 的新签名。

### 5. 磁贴 + 锁屏通知集成
**问题**：WP8.1 WinRT 应用的锁屏详细状态文字需要绑定宽磁贴第一行文本。  
**方案**：`TileWide310x150Text05` 模板的 `text1` 字段承载锁屏状态文字，manifest 中声明 `Notification="badgeAndTileText"`。

## 页面预览

| 当前天气 | 逐时预报 | 逐日预报 | 城市搜索 |
|---------|---------|---------|---------|
| 温度+图标+6格详情 | 24h卡片+温度折线图 | 7天列表+温度条 | 搜索+收藏管理 |

## License

MIT
