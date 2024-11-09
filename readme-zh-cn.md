<p align="center"> 	<a href="./readme.md"> 	English 	</a> 	/ 	 <a href="./readme-ru.md"> Русский </a>  /  <a href="./readme-zh-cn.md"> 	简体中文 	</a>  /  <a href="./readme-ar.md"> 	اَلْعَرَبِيَّةُ 	</a>  /  <a href="./readme-fa.md"> 	فارسی 	</a> </p>

<div align="center">

<img src="DragonFruit.OnionFruit/Assets/onionfruit.svg" width="100"/>

# OnionFruit™
以最小的努力连接到Tor网络

</div>

## 概述
OnionFruit™ 是一个Tor代理网关，它引导Tor并更新适当的系统设置，以允许广泛的应用程序（主要是浏览器）使用该网络，并通过干净、现代的界面提供一系列自定义和功能。

这是对2016年末发布的传统OnionFruit™ Connect的开源重写（最后一次重大 redesign 在2020年）。

## 状态
目前，该程序仍在开发中，但处于可用状态。
鼓励用户下载并使用该程序（可以与传统版本并行使用或替代），并报告他们遇到的任何错误/提供反馈。

## 运行OnionFruit™
> [!WARNING]
> 这是OnionFruit™的预发布版本，可能包含错误。请报告您遇到的任何问题。
> 想要稳定版本？请查看[传统信息页面](https://github.com/dragonfruitnetwork/onionfruit/tree/onionfruit-connect-legacy-info)。

OnionFruit™的构建版本适用于以下平台。点击链接下载最新版本：

- [Windows 10+ (x64)](https://github.com/dragonfruitnetwork/onionfruit/releases)

**注意：install.exe是传统版本OnionFruit™ Connect的安装程序。**

## 特性
🌍 入口/出口位置选择（定期数据库更新）  
🌉 支持桥接: webtunnel/snowflake/meek/conjure/plain(vanilla)/scramblesuit/obfs3/obfs4  
🧱 在限制性防火墙上设置允许的端口  
🌐 自定义启动页面  
🛡️ 安装和使用不需要管理员权限  
🎮 可选的Discord状态  
✨ 基于Windows 11 Fluent 2设计  
⚖️ 完全开源

## 开发
您需要.NET 8 SDK和一个IDE（推荐使用Visual Studio或JetBrains Rider）。
如果要处理UI，请熟悉[Avalonia UI](https://avaloniaui.net/)和[ReactiveUI](https://www.reactiveui.net/)，因为它们在各处使用。

要开始，请克隆代码库，然后打开解决方案文件`DragonFruit.OnionFruit.sln`。

```bash
git clone https://github.com/dragonfruitnetwork/onionfruit
cd onionfruit
```

### 从IDE构建
要构建项目，请使用IDE提供的构建/运行/调试功能。

### 从CLI构建
在您选择的终端中使用`dotnet run`构建并运行项目：

```bash
dotnet run --project DragonFruit.OnionFruit.Windows
```

如果您打算处理新功能/大改动，建议打开一个Issues，说明您想要更改的内容，以了解需要做什么/是否在范围内，因为我们不想浪费精力。

## 许可证
> [!NOTE]
> 这不适用于OnionFruit使用的依赖项（如Tor），因为它们在不同的条款下获得许可。

OnionFruit根据LGPL-3许可。有关更多信息，请参阅[licence.md](licence.md)或通过inbox@dragonfruit.network联系。