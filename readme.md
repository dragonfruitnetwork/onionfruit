<div align="center">

<img src="DragonFruit.OnionFruit/Assets/onionfruit.svg" width="100"/>

# OnionFruit™
Connect to the Tor network with minimal effort

</div>

## Overview
OnionFruit™ is a Tor Proxy Gateway that bootstraps Tor and updates the appropriate system settings to allow a wide range of applications to use the network (primarily Browsers) with a range of customisation and features through a clean, modern interface.

This is an open-source rewrite of the legacy OnionFruit™ Connect, originally published in late 2016 (with the last major redesign in 2020).

## Status
Currently, the program is still in development but is in a usable state.
Users are encouraged to download and use the program (either side-by-side or as a replacement for the legacy version) and report any bugs they encounter/provide feedback.

## Running OnionFruit™
> [!WARNING]
> This is a pre-release version of OnionFruit™ and may contain bugs. Please report any issues you encounter.
> Want the stable version? Check out the [legacy info page](https://github.com/dragonfruitnetwork/onionfruit/tree/onionfruit-connect-legacy-info).

OnionFruit™ builds are provided for the platforms below. Click the links to download the latest version:

- [Windows 10+ (x64)](https://github.com/dragonfruitnetwork/onionfruit/releases)
- macOS 13.5+ (x64) - Coming soon...
- macOS 13.5+ (Apple Silicon) - Coming soon...

**NOTE: install.exe is the installer for the legacy version of OnionFruit™ Connect.**

## Features
🌍 Entry/Exit Location selection (with regular database updates)  
🌉 Bridge support including plain, obfs4, snowflake and conjure transports  
🧱 Set allowed ports to access through restrictive firewalls  
🧭 DNS proxying to allow applications to resolve .onion addresses (currently only available on Windows)  
🌐 Custom launch pages  
🛡️ No administrator privileges required to install and use most features  
🎮 Optional Discord status  
✨ Based on [Fluent 2 Design](https://fluent2.microsoft.design/)  
⚖️ Fully open source

## Developing
You'll need the .NET 9 SDK and an IDE (Visual Studio or JetBrains Rider are recommended).
If working on the UI, familiarise yourself with [Avalonia UI](https://avaloniaui.net/) and [ReactiveUI](https://www.reactiveui.net/) as they're used everywhere.

To get started, clone the repo then open the solution file `DragonFruit.OnionFruit.sln`.

```bash
git clone https://github.com/dragonfruitnetwork/onionfruit
cd onionfruit

# macOS requires a couple additional commands to copy/build a native library and install a launch daemon - see below for more details
```

#### Additional setup for macOS
> [!NOTE]
> Prebuilt files can be found at [aspriddell/onionfruit-macos-development](https://github.com/aspriddell/onionfruit-macos-development) to get started without using Xcode

macOS uses a helper service, `onionfruitd`, and a native library, `libonionfruit`, to grant access to change required system settings.

These following targets are of interest and can be built from the source code in the `macos-native` directory:

- `onionfruit` - Native library that allows OnionFruit™ to communicate with `onionfruitd` (and `onionfruitd-dev`).
- `onionfruitd` - XPC-based helper service that allows OnionFruit™ to change system settings.
- `onionfruitd-dev` - Development version of `onionfruitd` that is less secure than `onionfruitd`, allowing development builds to work.
- `daemonloader` - ServiceLoader program that can be used to install a copy of `onionfruitd-dev` to the current machine.

Note: when building the `onionfruit`/`onionfruitd` target, the output files are automatically copied to the correct location for use with the desktop client.

### Building from IDE
To build the project, use the provided build/run/debug functions provided by your IDE.

### Building from CLI
Build and run the project using `dotnet run` in a terminal of your choice:

```bash
# on Windows
dotnet run --project DragonFruit.OnionFruit.Windows

# on macOS
dotnet run --project DragonFruit.OnionFruit.MacOS
```

If you intend to work on a new feature/large change, it's recommended to open a new issue stating what you'd like to change to get an idea of what needs to be done/whether it's in scope, as we don't want to waste effort.

## License
> [!NOTE]
> This does not apply to dependencies used by OnionFruit™ (such as Tor) as they are licensed under different terms.

OnionFruit™ is licensed under LGPL-3. See [licence.md](licence.md) or reach out via inbox@dragonfruit.network for more info.
