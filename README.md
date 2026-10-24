# 肥鱼压缩器（Feiyu Compressor）

一个 Windows 桌面小工具：把文件或文件夹做**两层 AES-256 加密压缩**，中间把后缀改成 `.mp4` 做一层伪装，最终得到 `原名.mp4.7z`。解压时自动反向操作，还原原始文件。

适合需要给个人文件加一层隐私保护再异地备份 / 上传的场景。

## 界面

| 压缩模式 | 解压模式 |
|---|---|
| ![压缩界面](screenshots/压缩界面.png) | ![解压界面](screenshots/解压界面.png) |

## 功能

- 压缩 / 解压两种模式一键切换
- 支持拖拽文件、文件夹，也可以手动选择
- 两层加密：第一层 7z（AES-256 + 加密文件名），改名 `.mp4` 后再整体加密一层
- 解压全自动：解第一层 → 把 `.mp4` 改回 `.7z` → 再解第二层
- 站酷快乐体界面，500×400 无边框圆角窗口，毛玻璃风背景
- 单文件 exe，双击即用，不需要安装 Python / 7-Zip

## 使用

详见 [使用说明.txt](使用说明.txt)。

**压缩**：输入密码 → 拖入文件/文件夹（密码已填好时会自动开始）→ 原文件旁生成 `原名.mp4.7z`。

**解压**：切到「解压」→ 输入密码 → 拖入 `原名.mp4.7z` → 自动还原到压缩包所在位置。

> 密码务必记牢，没有后门，忘记无法恢复。

## 构建

需要 Windows 自带的 .NET Framework 4.8 编译器（`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`）：

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

## 技术栈

- C# / WinForms（.NET Framework 4.8），无第三方运行时依赖
- 内置 7-Zip 官方命令行引擎 `7zr.exe` 完成实际压缩 / 解压（会在首次运行时释放到 `%LOCALAPPDATA%\FeiyuZip\bin\`）
- 字体、背景图、图标全部嵌入 exe

## 兼容性

- 产物是标准 7z 格式（AES-256），可用 7-Zip / WinRAR / Bandizip 直接打开（第一层解开得到 `.mp4`，改回 `.7z` 再解开即可）。
- 系统要求：Windows 10 / 11（自带 .NET Framework 4.8）。

## 第三方组件

- 7-Zip（7zr.exe）— https://www.7-zip.org/ ，LGPL 许可
- 站酷快乐体（ZCOOL KuaiLe）— SIL Open Font License

## 免责声明

本项目仅用于个人文件的加密与备份。请遵守你使用的网盘 / 存储服务的用户协议以及当地法律法规。
