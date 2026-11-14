# Cloud Game

一个轻量的 Windows 本地游戏库与启动器。使用原生 **WPF / C#** 编写，不依赖 Electron、WebView 或第三方运行时。

`CloudGame.exe` 单文件约 **288 KiB（294 KB）**，当前版本 **2.0.0**。

[English README](README.en.md)

![Cloud Game 游戏库](docs/screenshots/library-dark.png)

## 功能

- **本地游戏库**：添加 `.exe` / `.bat` / `.cmd`，也可以直接把文件拖进窗口。
- **游玩时长**：记录每次启动、单次会话、总时长、最近游玩和启动次数；运行中的游戏会实时显示。
- **自定义封面**：为每款游戏选择封面，或自动生成程序化艺术图。
- **启动器背景**：在设置中选择任意图片作为启动器背景，并调整暗化程度。
- **主题**：暗色 / 亮色模式，6 种强调色，切换时带整体过渡。
- **导出 / 导入**：支持只导出游玩数据，或连同封面一起导出为单个 JSON 文件。
- **托盘与快捷键**：关闭到托盘后，右键托盘图标可直接打开、添加游戏、进入设置或退出；`Ctrl+F` 聚焦搜索，`Esc` 关闭详情/弹窗。
- **动画**：封面飞入详情、页面淡入、列表重排、筛选切换、侧栏滑块、Toast 和主题过渡。
- **管理员游戏**：启动需要提升权限的游戏时，会自动通过 UAC 重试。

![游戏详情](docs/screenshots/game-detail.png)

## 发布包

免安装版位于 `release\CloudGame-2.0.0-win64.zip`，解压后直接运行 `CloudGame.exe`。压缩包内含中英文 README 和文档截图。

## 环境

- Windows 10 / 11 x64
- .NET Framework 4.8（Windows 10 1903+ 与 Windows 11 默认包含）
- 无需安装，直接运行 `CloudGame.exe`

## 数据位置

所有数据保存在本地：

```
%APPDATA%\CloudGame\
├── library.json      # 游戏、游玩记录、设置
├── media\            # 封面、启动器背景
└── error.log         # 仅在出现异常时生成
```

导入/导出格式为 JSON，`format` 为 `cloudgame.library`。旧版本的 LUDENS 数据不会自动迁移。

## 使用

1. 点击“添加游戏”或把 `.exe` 拖进窗口。
2. 填写游戏名称、可执行文件，选择封面（也可以留空自动生成）。
3. 点击游戏卡片进入详情，或点击封面/列表右侧的播放按钮直接启动；启动器会记录本次游玩时长。
4. 在“设置”中切换主题、强调色、启动器背景，或导出/导入数据。

### 快捷键

| 快捷键 | 行为 |
| --- | --- |
| `Ctrl+F` | 打开游戏库并聚焦搜索 |
| `Esc` | 关闭详情 / 编辑弹窗 / 确认框 |

## 截图

| 暗色 | 亮色 |
| --- | --- |
| ![暗色游戏库](docs/screenshots/library-dark.png) | ![亮色游戏库](docs/screenshots/library-light.png) |

| 统计 | 设置 |
| --- | --- |
| ![统计](docs/screenshots/stats.png) | ![设置](docs/screenshots/settings-dark.png) |

| 添加游戏 | 横向列表 |
| --- | --- |
| ![编辑游戏](docs/screenshots/editor.png) | ![列表视图](docs/screenshots/list-view.png) |

## 从源码构建

项目使用 .NET Framework 4.0 目标，可直接用系统自带的 MSBuild 编译（无需安装 .NET SDK）：

```powershell
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe `
  D:\Code\cloud-game\Ludens.csproj `
  /p:Configuration=Release `
  /p:TargetFrameworkDirectory="C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
```

输出：`bin\Release\CloudGame.exe`

## 结构

```
Ludens.csproj           WPF 项目文件
App.xaml / App.xaml.cs  启动、主题、异常日志
MainWindow.xaml         窗口外壳、侧栏、页面切换、弹窗宿主
Views/                  Library / Detail / Stats / Settings / GameEditor / Confirm
Src/                    AppStore、LauncherService、MediaService、ThemeManager、
                        Motion、SmoothScroll、CoverArt、数据模型
Resources/Styles.xaml   颜色、字体、按钮、输入框、滚动条、菜单样式
Assets/                 图标与品牌图
docs/screenshots/       README 截图
```

## 说明

- 游戏计时基于 Windows Job Object 跟踪进程树；如果游戏本身通过代理进程启动并很快退出，计时可能不准确。
- 自动生成的封面是程序化艺术图，不包含版权素材。
- 当前版本不包含云同步；数据只保存在本机。
