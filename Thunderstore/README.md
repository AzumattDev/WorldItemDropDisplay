# WorldItemDropDisplay

![https://github.com/AzumattDev/WorldItemDropDisplay/blob/master/Thunderstore/icon.png?raw=true](https://github.com/AzumattDev/WorldItemDropDisplay/blob/master/Thunderstore/icon.png?raw=true)

## A **client-side** Valheim mod that shows floating UI markers above dropped items in the world, giving you at-a-glance info (for items in range) on:

- **Stack size**
- **Quality level**
- **Durability** (with a bar)
- **Teleport-lock** icon
- **Food effect** color coding

`Client only mod, not needed on server.`

`This mod uses a file watcher. If the configuration file is not changed with BepInEx Configuration manager, but changed in the file directly, upon file save, it will live update the changes.`
---

## Configuration

All settings live in `BepInEx/config/Azumatt.WorldItemDropDisplay.cfg` (or use BepInEx’s in-game Config Manager). Edit
by hand or via UI, either way, changes reload on the fly.

| Setting                    | Description                                                                                                      | Default       |
|----------------------------|------------------------------------------------------------------------------------------------------------------|---------------|
| **Position Interval**      | How often (seconds) to refresh UI positions                                                                      | `0.01`        |
| **Data Interval**          | How often (seconds) to refresh stack/quality data                                                                | `0.01`        |
| **Max Display Distance**   | Maximum distance (meters) at which markers appear                                                                | `5.0`         |
| **World Item Offset**      | Offset `(X, Y, Z)` relative to each item                                                                         | `(0, 1.2, 0)` |
| **Subtract Camera Offset** | Subtract the camera offset from the world item display position, might help it look more centered on the object. | `On`          |
| **Show Background**        | Show the background behind the item, in the item drop display                                                    | `On`          |
| **Show Amount**            | Show the stack-count text for stackable items, in the item drop display                                          | `On`          |
| **Show Quality**           | Show the quality number, in the item drop display                                                                | `On`          |
| **Show Durability**        | Show the durability bar when applicable, in the item drop display                                                | `On`          |
| **Show No Teleport Icon**  | Show icon when item cannot be teleported, in the item drop display                                               | `On`          |
| **Show Food Icon**         | Show the food icon for consumables (eitr, health, stamina forks), in the item drop display                       | `On`          |

## Features

- **Real-time data**: Stack, quality, durability, teleport status, food effect, and equip status update automatically.
- **Low GC & smooth**: Uses object pooling, caching, and early-exit checks to minimize garbage collection and per-frame
  work.
- **Live config reload**: Change your `.cfg` by hand and see it apply immediately—no restart required.

## Installation Instructions

***You must have BepInEx installed correctly! I can not stress this enough.***

### Manual Installation

`Note: (Manual installation is likely how you have to do this on a server, make sure BepInEx is installed on the server correctly)`

1. **Download the latest release of BepInEx.**
2. **Extract the contents of the zip file to your game's root folder.**
3. **Download the latest release of WorldItemDisplay from Thunderstore.io.**
4. **Extract the contents of the zip file to the `BepInEx/plugins` folder.**
5. **Launch the game.**

### Installation through r2modman or Thunderstore Mod Manager

1. **Install [r2modman](https://valheim.thunderstore.io/package/ebkr/r2modman/)
   or [Thunderstore Mod Manager](https://www.overwolf.com/app/Thunderstore-Thunderstore_Mod_Manager).**

   > For r2modman, you can also install it through the Thunderstore site.
   ![](https://i.imgur.com/s4X4rEs.png "r2modman Download")

   > For Thunderstore Mod Manager, you can also install it through the Overwolf app store
   ![](https://i.imgur.com/HQLZFp4.png "Thunderstore Mod Manager Download")
2. **Open the Mod Manager and search for "WorldItemDisplay" under the Online
   tab. `Note: You can also search for "Azumatt" to find all my mods.`**

   `The image below shows VikingShip as an example, but it was easier to reuse the image.`

   ![](https://i.imgur.com/5CR5XKu.png)

3. **Click the Download button to install the mod.**
4. **Launch the game.**

<br>
<br>

`Feel free to reach out to me on discord if you need manual download assistance.`

# Author Information

### Azumatt

`DISCORD:` Azumatt#2625

`STEAM:` https://steamcommunity.com/id/azumatt/

For Questions or Comments, find me in the Odin Plus Team Discord or in mine:

[![https://i.imgur.com/XXP6HCU.png](https://i.imgur.com/XXP6HCU.png)](https://discord.gg/qhr2dWNEYq)
<a href="https://discord.gg/pdHgy6Bsng"><img src="https://i.imgur.com/Xlcbmm9.png" href="https://discord.gg/pdHgy6Bsng" width="175" height="175"></a>