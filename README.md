# 轻录 0.3

适用于 Windows 10 2004 及更新版本、Windows 11 的 x64 轻量录屏工具。支持全屏/窗口/区域、电脑声音与麦克风、暂停继续、倒计时、快捷键和录制排除的悬浮控制条。详细操作见 [使用说明](使用说明.txt)。

## 使用

GitHub 下载入口：[最新发布](https://github.com/Aventa12/light-recorder/releases/latest)，提供安装包、便携包及完整工程文档压缩包。本地构建后，发布文件生成在 `release/`。

- 安装版：`release/轻录-Win10-x64-安装包.exe`，自带微软 C++ 运行库。
- 便携版：解压 `release/轻录-Windows-x64.zip` 后运行，或直接打开 `dist/轻录.exe`。
- 安装版设置位于 `%LOCALAPPDATA%/LightRecorder`，视频默认位于用户“视频/轻录”；便携版使用程序目录。卸载保留用户视频与设置。

## 目录

```text
src/                     应用源码、版本信息、清单和运行配置
tests/                   录制交互及视频解码验证源码
assets/                  用户原始图标、多尺寸 ICO、安装器中文语言文件
packaging/               安装脚本和安装模式标记
tools/BuildIcon.cs       图标格式转换工具源码
tools/inno-compiler/     必需的 Inno Setup 命令行编译文件和许可
vendor/                 录制核心及离线 C++ 运行库依赖
docs/validation.md       验证结果与范围
dist/                   编译后的可运行程序
release/                安装包、便携包及 SHA-256 校验文件
```

## 构建

在 PowerShell 运行：

```powershell
./build.ps1             # 编译应用，并生成 release 中的便携包
./build-installer.ps1   # 编译应用、打包安装版，并更新两份发布包的校验值
```

使用系统 .NET Framework C# 编译器，不需要完整 IDE 或独立 .NET SDK。图标转换的临时程序在完成后自动删除。安装版还需要 `tools/inno-compiler/ISCC.exe` 及 `vendor/vc_redist.x64.exe`，构建时校验运行库的微软数字签名。最终发布包只包含应用及运行所需文件。

录制核心已固定为 ScreenRecorderLib 7.0.1。需要重新获取时执行 `python fetch-dependency.py`，脚本会校验固定包的 SHA-256。

## 验证与依赖

验证源码保留在 `tests/`。主动运行 `dist/轻录.exe --self-test` 或 `--overlay-test` 会进行短时屏幕/音频录制，并生成临时 `verification/` 目录；这些测试不会自动运行。验证结论及限制见 [验证记录](docs/validation.md)。

录制核心使用 [ScreenRecorderLib](https://github.com/sskodje/ScreenRecorderLib)，MIT 许可原文随应用附带。原始图标保留透明背景，只做尺寸和格式转换。安装器使用 Inno Setup，许可保留在 `tools/inno-compiler/license.txt`。

安装版检查 Windows 自带的 .NET Framework 4.8，缺少 C++ 运行库时使用内置安装程序；N/KN 缺少媒体组件时需要网络及管理员许可来补齐。系统依赖说明见 [Microsoft C++ 运行库](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist) 和 [Windows 自带 .NET 版本](https://learn.microsoft.com/en-us/dotnet/framework/install/on-windows-and-server)。
