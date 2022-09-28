# Introduction to OCTOPUS RAW Player
OCTOPUS RAW Player is a multi-platform (Windows/macOS) lightweight video playback application for reviewing RAW video footage from professional cameras. Currently the player supports CinemaDNG RAW ```.dng``` sequences. The OCTOPUS RAW Player leverages native C++ code for low-level decoding and implements our unique sophisticated colour science processing pipeline in GLSL (OpenGL shading language).

&nbsp;
<p align="center">
<img src="http://www.octopuscinema.com/wiki/images/thumb/0/0b/Octopus-raw-player-hero.png/1600px-Octopus-raw-player-hero.png" width="80%">
</p>
&nbsp;

**Features**
- Modern minimal interface
- Efficient real-time CinemaDNG playback for 8,12,14,16-bit bayer or monochrome, uncompressed or lossless compressed ```.dng``` sequences.
- Professional RAW controls (White balance, exposure)
- Advanced colour science control (Highlight recovery/roll-off, SDR Tone-Mapping)
- Timecode and metadata display
- Support for real-time 8K playback

**Tested Cameras**
- Sigma Fp
- DJI Xenmuse X5,X5R,X7
- Blackmagic Cinema Camera 2.5K
- Blackmagic Pocket Cinema Camera (Original)
- Blackmagic Micro Cinema Camera
- Blackmagic Ursa Mini 4K
- OCTOPUSCAMERA 8K/4K/MONO

For more information, please visit the wiki:
http://www.octopuscinema.com/wiki/index.php?title=OCTOPUS_RAW_Player

# Getting started for Users

## Basic system requirements
- Windows 10 / macOS 10.10 or newer
- GPU with support for OpenGL 3.2 or greater (Native Apple Metal API support in progress)

## Installation
OCTOPUS RAW Player releases are usually installed by launching the Windows ```.msi``` or macOS ```.pkg``` installer file. Alternatively, OCTOPUS RAW Player can be run as a portable single-file executable without installation.

### User Guide
Please see the user guide wiki page:
http://www.octopuscinema.com/wiki/index.php?title=OCTOPUS_RAW_Player#User_Guide

# Included in this repository
Cross platform (Windows/macOS) C# and C++ source code and projects/solutions/workspaces including OpenGL GPU kernel source (GLSL)

# License
The contents of this repository are licensed under the MIT License (https://mit-license.org)

## Contributions
Unless explicitly stated otherwise, any contribution intentionally submitted for inclusion in the work by you, as defined in the MIT license, shall be licensed as above, without any additional terms or conditions.

# Getting started for Developers

## Building for Windows
### Dependencies
Building OCTOPUS RAW Player requires Visual Studio 2022 with the C# desktop development workload. The OCTOPUS RAW Player project references several NuGet packages which should be restored (https://docs.microsoft.com/en-us/nuget/consume-packages/package-restore) prior to building.

### Building the solution
The OCTOPUS RAW Player executable for Windows can be built from the ```raw-player/Player.Windows.sln``` solution file. The solution contains both C# and C++ projects (mixed managed/unmanaged code). C++ project dependencies are linked dynamically at run-time - please ensure the C++ projects ```raw-player/Decoders``` are built prior to publishing or launching the debug/release executable.

#### Deployment
The ```raw-player/Player.Windows.sln``` solution includes a publish profile to build a standalone single-file executable with an embedded .net runtime. This executable can be run on systems without .net runtimes installed.

## Building for macOS
### Dependencies
Building OCTOPUS RAW Player requires Visual Studio 2022 for Mac (with Xamarin.Mac version 8.12 or newer) and Xcode 13 or newer. The OCTOPUS RAW Player project references several NuGet packages which are normally automatically restored prior to building. (See https://learn.microsoft.com/en-us/visualstudio/mac/nuget-walkthrough?view=vsmac-2022 for more information)

### Building the solution
The OCTOPUS RAW Player application for macOS is built from the ```raw-player/Player.macOS.sln``` C# solution file. Visual Studio for Mac does not support C++ projects - native library dependencies must be built manually from the ```raw-player/Decoders/Decoders.macOS.xcworkspace``` Xcode workspace prior to building the C# solution.
