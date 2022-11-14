# Stacklands LessLag Mod

Reduces lag. Atm it seems to improve FPS by around 25%-33% with default settings.

Specifically:
- Avoids re-checking whether something can be crafted if a stack hasn't changed
- Makes card-push-physics more efficient (at the cost of some jank)
- Avoids updating unchanging equipment descriptions every frame
- (Disabled by default) Updates booster shop less often


## Manual Installation
This mod requires BepInEx to work. BepInEx is a modding framework which allows multiple mods to be loaded.

1. Download and install BepInEx from the [Thunderstore](https://stacklands.thunderstore.io/package/BepInEx/BepInExPack_Stacklands/).
4. Download this mod and extract it into the `BepInEx` directory
5. Launch the game

## Links
- Github: https://github.com/benediktwerner/Stacklands-LessLag-Mod
- Thunderstore: https://stacklands.thunderstore.io/package/benediktwerner/LessLag

## Generate patched assembly

```
dotnet build src_patcher/LessLag.Patcher.csproj -c Release_RUN
cp <bepinex dir>/core/Mono.Cecil.* src_patcher/bin/Release_RUN/net35/
./src_patcher/bin/Release_RUN/net35/LessLag.Patcher.exe
```

## Changelog

- v1.0.2:
  - Fixed a bug where selling card in a crafting stack wouldn't stop the crafting
  - Fixed a bug where taking a valid craftable stack from another stack wouldn't start crafting
  - Fixed a bug where dropping a card and immediately picking up the new parent in the next frame wouldn't start crafting
- v1.0.1: Fix bugs
- v1.0: Initial release
