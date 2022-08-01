# Introduction to OCTOPUS RAW Player


# Getting started for Users

## Basic system requirements
- Windows 10 / macOS
- GPU with support for OpenGL 3.2 or greater (Native Apple Metal API support in progress)

## Installation
The OCTOPUS RAW Player can be installed via a standard Windows .msi or macOS package installer file. Alternatively (Windows only), the OCTOPUS RAW Player can be run as a portable single-file executable without installation.

### Standalone executable (Windows only)
The OCTOPUS RAW Player is built into a single-file ```.exe``` with no depednancies executable which can be run without installation.

### User Guide
For more information about the OCTOPUS RAW Player, please see the wiki:
http://www.octopuscinema.com/wiki/index.php?title=OCTOPUS_RAW_Player

## Sample CinemaDNG Files
Sample CinemaDNG sequences for testing with OCTOPUS RAW Player are downloadable from:
www.octopuscinema.com/cinema-dng-samples

# Included in this repository
Cross platform (Windows/macOS) C# and C++ source code and projects/solutions/workspaces including OpenGL GPU kernel source (GLSL)

# License
The contents of this repository are licensed under the MIT License (https://mit-license.org)

## Contributions
Unless explicitly stated otherwise, any contribution intentionally submitted for inclusion in the work by you, as defined in the MIT license, shall be licensed as above, without any additional terms or conditions.

# Getting started for Developers

## Building for Windows
### Dependancies
Building OCTOPUS RAW Player requires Visual Studio 2022 with the C# desktop development workload. The OCTOPUS RAW Player project references a handful of NuGet packages which should be restored (https://docs.microsoft.com/en-us/nuget/consume-packages/package-restore) prior to building.

### Building the solution
The OCTOPUS RAW Player executable for Windows can be built from the ```raw-player/Player.Windows.sln``` solution file. The solution contains both C# and C++ projects (mixed managed/unmanaged code). C++ project dependancies are linked dynamically at run-time - please ensure the C++ projects ```raw-player/Decoders``` are built prior to publishing or lauching the debug/release executable.

#### Deployment
The ```raw-player/Player.Windows.sln``` solution includes a publish profile to build a standalone single-file executable with an embedded .net runtime. This executable can be run on systems without .net runtimes installed.

## Building for macOS
TBD
