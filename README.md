# Caps 输入法切换

Windows 10/11 托盘小程序：在选定输入法下，短按 Caps Lock 发送该输入法的中英文切换快捷键；长按（默认 450 毫秒）切换大写锁定。未选定的输入法、全屏窗口和指定进程保持 Caps Lock 原行为。

## 安装与设置

1. 双击 `dist/WeTypeCaps.exe`，在安装提示中点“是”。程序会复制到 `%LOCALAPPDATA%\WeTypeCaps`，设置当前用户登录后启动并立即运行。不需要管理员权限或单独安装 .NET。
2. 在系统托盘右键程序图标，点“设置…”。勾选要启用的输入法，并为每个输入法选择 `Ctrl+Space` 或 `Shift`。保存后立即生效。
3. 可在设置中调整长按阈值、全屏绕过、始终绕过的进程和开机启动。

配置保存在 `%LOCALAPPDATA%\WeTypeCaps\config.json`。从旧版程序目录读取到的 `config.json` 会在首次安装时复制过去。安装源文件可以在安装后删除。

如需自行构建，安装 .NET 10 SDK 后运行 `powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1`。生成的 `dist/WeTypeCaps.exe` 是 Windows x64 自包含单文件程序。

## 输入法支持的范围

设置界面读取 Windows TSF 注册信息，列出本机注册的 Profile，并提供搜索。这个列表可能包含尚未添加到语言栏的系统内置输入法和语音项目；勾选前请确认它是自己正在使用的键盘输入法。程序在运行时比较活动 Profile 的 CLSID 和 Profile GUID；因此可分别配置微信输入法、微软拼音及其他已注册的 TSF 输入法，而不把同属简体中文的输入法混为一谈。默认只启用微信输入法的已知 Profile。安装的输入法版本如果换了 GUID，请在设置中勾选新发现的条目。

“支持”指能识别 Profile 并发送所选按键，不保证每种输入法都使用同一个切换快捷键。请先在输入法设置中确认快捷键：例如有的输入法用 `Ctrl+Space`，有的可设为单按 `Shift`。当前没有针对每家输入法的私有 API，也不读取输入法内部的中英文状态；如果某输入法只提供未列出的切换方式，此版本不能可靠切换。Windows 传统键盘布局或没有注册 TSF Profile 的输入法，也不会显示为可选条目。

## 设计取舍与边界

- 使用低级键盘钩子拦截 Caps Lock，并用 `SendInput` 发快捷键；不注入输入法进程，不记录普通键盘输入。用户按住 Ctrl、Alt、Shift 或 Win 时不拦截 Caps Lock。
- TSF 查询每 120 毫秒刷新一次，切换输入法和按下 Caps Lock 几乎同时发生时，可能遇到短暂状态延迟。按键期间前台窗口发生变化时，不会把短按动作发送到新窗口。
- 默认绕过所有全屏窗口。这包括视频、演示文稿，也包括游戏；Windows 没有可靠的通用“这是游戏”判定。可在配置文件的 `FullscreenProcessNames` 中限定全屏绕过的进程。
- 普通权限运行的程序无法可靠向管理员权限窗口发送模拟按键；安全桌面、部分游戏和反作弊环境也可能屏蔽钩子或注入。
- Windows 用户级安装不会出现在系统“已安装的应用”列表。卸载时退出托盘程序，删除 `%LOCALAPPDATA%\WeTypeCaps` 目录，并删除 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 下的 `WeTypeCaps` 值；仓库的 `uninstall.ps1` 可执行这些步骤。

## 诊断

`WeTypeCaps.exe --self-test` 运行基础自检。`WeTypeCaps.exe --diagnose` 会把活动输入法、前台窗口和匹配结果写入 `%LOCALAPPDATA%\WeTypeCaps\WeTypeCaps.log`。托盘菜单也可手动写入诊断日志。
