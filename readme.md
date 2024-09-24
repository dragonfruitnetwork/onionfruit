<div align="center">

# OnionFruit™
Connect to the Tor network with minimal effort

</div>

## Overview
OnionFruit is a Tor Proxy Gateway that effectively bootstraps Tor and updates the apropriate system settings to allow a wide range of applications to use the network (primarily Browsers) with a range of customisation and features through a clean, modern interface.

This is an open-source rewrite of the original OnionFruit Connect, originally published in late 2016 (with the last major redesign in 2020).

## Status
Currently the program is still in development but is in a usable state. We encourage users to download and use the program, and report any bugs they encounter/provide feedback.

We are also exploring the possibility for extending support to macOS, but have no set timeframe for this.

## Features
🌍 Entry/Exit Location selection (with regular database updates)  
🌉 Bridge support including plain, obfs4, snowflake and conjure transports  
🧱 Set allowed ports on restrictive firewalls  
🌐 Custom launch pages  
🛡️ No administrator privileges required to install and use (on Windows)  
🎮 Optional Discord status  
✨ Based on Windows 11 Fluent 2 Design  
⚖️ Fully open source

## Developing
To get started developing, you'll need the .NET 8 SDK and an IDE (Visual Studio or JetBrains Rider are recommended).

Clone the repo then open the solution file. NuGet packages _should_ automatically download, but if not `dotnet restore` will resolve that)

```bash
git clone https://github.com/dragonfruitnetwork/onionfruit

.\onionfruit\DragonFruit.OnionFruit.sln
```

If you intend to work on a new feature/large change, it's recommended to open a new issue stating what you'd like to change to get an idea of what needs to be done/whether it's in scope, as we don't want to waste effort.

## License
> [!NOTE]
> This does not apply to dependencies used by OnionFruit (such as Tor) as they are licensed under different terms.

OnionFruit is licensed under LGPL-3. See [licence.md](licence.md) or contact us at inbox@dragonfruit.network for more info.
