***IN DEVELOPMENT***

Compiler of IL assemblies (including plugins) directly into VRChat's UDON program.

The utility was created for more convenient work with the unity, as close as possible to the usual one. Whenever possible, many standard methods of working with objects and other things will be implemented here.

To add an assembly to the build list, you need to mark it with an attribute.
```csharp
[assembly: UdonAsm]
```
Or you can create an assembly folder via the menu: `Create/UDON Assembly Folder`

All classes are compiled and stored directly in SerializedUdonProgramAsset, bypassing UdonAssemblyProgramAsset. This creates less garbage in the project, and the program is used to link the script and the UdonBehaviour on scene. Therefore, be careful not to delete this asset, otherwise the links on the scene will be broken.

### Notes
- At the moment, this utility is under development, so many things may not be implemented, as well as optimizations are not implemented everywhere
- All default values declared for the class fields are assigned at the time of the build, for example: `private DateTime startTime = DateTime.Now;` will have a value at the time of the build, and not at the time of loading the UdonBehaviour, so such things should be assigned in Start or other methods.

[![Trello](https://img.shields.io/badge/Trello-Katsudon%20Board-yellow?style=flat&logo=trello)](https://trello.com/b/jyjguAFA) - What has been done and what is planned
