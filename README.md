[![Build Status](https://travis-ci.org/RobinKa/netprints.svg)](https://travis-ci.org/RobinKa/netprints)

# Description
NetPrints is a visual programming language inspired by Unreal Engine 4's Blueprints which compiles into .NET binaries. These can be used from any other .NET language (eg. C#) or used as standalone programs. Furthermore any .NET binaries (both .NET Framework and .NET Core, and ideally .NET Standard) can be referenced and used. Its goal is to support using anything that is made in C#. Currently there are several limitations, the most major ones being delegate and generics support not being 100% complete.

# Download
Initial editor binaries can be found [here](https://github.com/RobinKa/netprints/releases/tag/InitialMaster). You can also download the source code and compile the binaries (requires .NET Core 3).

# Requirements
The editor itself requires .NET Core 3 (since this is the first version to support WPF), although we provide self-contained binaries for Windows x86. Binaries compiled by the editor require any dependencies you added as references.

# Guide
Any .NET binaries can be used with this editor. The recommended way to add new assembly references is installing them with NuGet (eg. from within Visual Studio or the command line) and referencing their .NET Standard reference libraries at `%UserProfile%/.nuget`. The hints for the included references should then appear within the editor.

# Screenshots

## Using External Libraries
### SFML
![](http://i.imgur.com/BXLHSE3.png)
### TensorFlowSharp
![](https://i.imgur.com/DjRuPeR.png)

## Generics
![](http://i.imgur.com/OnjPw36.png)

## Suggestions
![](https://i.imgur.com/ZuStkEJ.png)

## Overloads & Control Flow
![](https://i.imgur.com/ZADmF3t.png)

## Delegates
![](http://i.imgur.com/9GjrV49.png)

## Project Menu
![](http://i.imgur.com/umAjDX5.png)