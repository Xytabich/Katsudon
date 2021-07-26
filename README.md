***IN DEVELOPMENT***

Compiler of IL assemblies (including plugins) directly into VRChat's UDON program.

The utility was created for more convenient work with the unity, as close as possible to the usual one. Whenever possible, many standard methods of working with objects and other things will be implemented here.

To add an assembly to the build list, you need to mark it with an attribute.
```csharp
[assembly: UdonAsm]
```
Or you can create an assembly folder via the menu: `Create/UDON Assembly Folder`

### So far implemented
- Interfaces
- Abstract and generic classes
- Calls to unity and vrchat api
- Unty and VRChat events: Update, OnEnabled, OnDisabled, etc.

At the moment, this utility is under development, so many things may not be implemented, as well as optimizations are not implemented everywhere
