# WeType Caps

仅在“微信输入法 / WeType”是当前活动 TSF 输入法时改变 Caps Lock：

- 短按 Caps Lock：发送 `Ctrl+Space`，切换中英文。
- 长按 Caps Lock（默认 450 ms）：切换系统大写锁定。
- 微信输入法未激活：不拦截，Caps Lock 保持系统原行为。
- 前台应用全屏：默认不拦截，避免影响全屏游戏。

程序只处理 Caps Lock 事件，不记录普通按键内容，不注入微信输入法进程，也不修改微信输入法文件。

## 现有方案调研（截至 2026-07-27）

- [TapCaps](https://github.com/honue/TapCaps)：交互最接近，支持 Caps Lock 短按切换中英文、长按锁定大写，但 README 只说明在微软拼音上测试；没有按指定 TSF Profile 生效或全屏绕过。仓库当前也没有可识别的开源许可证，因此没有直接复制其代码。
- [Caps IME Switcher](https://github.com/ramensoftware/windhawk-mods/blob/main/mods/caps-ime-switcher.wh.cpp)：Windhawk 模组，支持短按切换下一输入语言、长按大写；需要 Windhawk，且不识别微信输入法 Profile，也没有全屏条件。
- [capslock-layout.ahk](https://gist.github.com/reclaimed/4a18d88b445bb80759d85188db2f5db4)：AutoHotkey 脚本，短按发送 `Win+Space`、长按 Caps Lock；没有输入法类型和前台窗口条件。
- [CCaps](https://github.com/holgertkey/ccaps)：MIT 许可的 Rust 工具，使用低级键盘钩子循环切换键盘布局；大写是 `Shift+Caps Lock`，按 HKL/LangID 识别，无法区分同属简体中文的微信输入法和微软拼音。
- [Vonng/Capslock](https://github.com/Vonng/Capslock)：成熟的 Caps Lock/Hyper 键增强方案，但 Windows 版已归档，目标不是本需求的输入法绑定。

因此本项目从零实现了缺失的两层条件：TSF Profile 精确匹配，以及前台全屏绕过；没有复制上述项目源码。

## 为什么是独立托盘程序

现有 AutoHotkey / PowerToys 类方案可以做短按、长按映射，但不能可靠区分同为 `0x0804` 的微软拼音、微信输入法等 TSF Profile。WeType Caps 使用 Windows 的 `ITfInputProcessorProfileMgr::GetActiveProfile`，同时比对微信输入法的 CLSID 和 Profile GUID。

本机检测到的微信输入法 2.1.1.6 标识：

- CLSID：`{86598FB9-66A2-463E-B9C2-AEB906D477AD}`
- Profile：`{607FDF85-FCC8-4DBD-A365-41296F980C9C}`

程序启动时会先从注册表按 `WeType` 描述自动发现标识；配置中的 GUID 是发现失败时的后备值。

## 构建与运行

需要 Windows 10/11 和 .NET 10 SDK。在 PowerShell 执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
.\publish\WeTypeCaps.exe
```

安装为开机启动并立即运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1
```

卸载只会移除开机启动并停止本目录中的程序，不删除文件：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\uninstall.ps1
```

## 配置

首次运行会在程序旁生成 `config.json`。托盘菜单可打开或重新加载配置。

- `HoldThresholdMilliseconds`：长按阈值，范围 200–2000 ms。
- `StateRefreshMilliseconds`：输入法/前台窗口刷新间隔，范围 50–2000 ms。
- `BypassFullscreen`：是否绕过全屏窗口。
- `FullscreenProcessNames`：空数组表示绕过所有全屏窗口；填入进程名后只绕过匹配的全屏进程，支持 `*` 和 `?`。
- `AlwaysBypassProcessNames`：无论是否全屏都绕过的进程名，支持通配符。

例如，只在指定游戏全屏时绕过：

```json
"FullscreenProcessNames": [
  "game.exe",
  "steam_app_*"
]
```

默认选择“所有全屏窗口都绕过”，因为 Windows 没有可信的通用 API 能判断任意进程是否为游戏；这也会绕过全屏视频和幻灯片，是较保守的安全边界。独占全屏、反作弊和管理员权限游戏可能直接屏蔽普通桌面键盘钩子，此时程序同样不会改变游戏输入。

## 诊断

```powershell
dotnet .\publish\WeTypeCaps.dll --self-test
dotnet .\publish\WeTypeCaps.dll --diagnose
```

底层 `SendInput` 注入测试会切换一次中英文状态；连续执行两次可测试并恢复原状态：

```powershell
dotnet .\publish\WeTypeCaps.dll --sendinput-test
dotnet .\publish\WeTypeCaps.dll --sendinput-test
```

托盘菜单“写入诊断日志”会写入：

```text
%LOCALAPPDATA%\WeTypeCaps\WeTypeCaps.log
```

## 已知边界

- `Ctrl+Space` 必须在微信输入法设置中仍然是中英文切换键。
- 短按 Caps Lock 时不建议同时按住 Shift、Alt 或 Win，这些修饰键会和 `Ctrl+Space` 一起到达前台应用。
- 低权限程序无法向管理员权限窗口可靠注入按键；若办公软件以管理员身份运行，需要让本程序以相同权限运行。
