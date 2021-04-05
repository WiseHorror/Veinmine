![enter image description here](https://i.imgur.com/HrLIv3J.png)

### Building
Make sure to reference unstripped/publicized DLLs when building.

# Changelog
## 1.1.9
 - Fixed incompatibility with Rocky Ore mod.
### 1.1.8
 - Fixed a bug that was causing no ores to be dropped (hopefully)
### 1.1.7
 - Fixed a bug that caused ores to drop every single pickaxe swing when mining manually.
### 1.1.6
 - Fixed a bug that was causing no drops to be received when veinmining.
### 1.1.5
#### Please delete your previous config when updating to this version.
 - Damage text when veinmining should now display on top of each rock section
 - Veinmining when Progressive mode is disabled is now instant
 - Spread damage now has two options: Even and Distance
	 - Even is the same as previous versions
	 - Distance makes it so the damage dealt to a rock section is based on distance. The farther away a section is, the less damage is dealt.

### 1.1.4
 - Fixed a bug where if you had spread damage enabled, mining damage would also be reduced when mining manually.
### 1.1.3
 - Added config option for spreading damage in progressive mode (divides your mining damage equally between all rock sections affected, as opposed to doing full damage to all sections)
 - This is disabled by default.
### 1.1.2
 - Fixed incompatibility with mods that alter the pickaxe's durability drain values. The value modified by such mods (m_useDurabilityDrain) is now multiplied by the result of the previous formula (which is shown in the config). Take this into account when editing this mod's durability modifier.
### 1.1.1
 - Added config options for durability and xp multipliers for Progressive mode.
### 1.1.0
 - Durability taken is now scaled to the player's Pickaxes level when Progressive mode is enabled. This is meant to (somewhat) balance veinmining so it fits in with vanilla.
 - The formula is (120 - Level) / 20, so if you have level 50: (120 - 50) / 20 = 3.5 durability taken **per rock section veinmined**
### 1.0.1
 - Removed log messages to avoid spam.
### 1.0.0
 - Added Progressive mode.
#### 0.0.1.4
 - Fixed a bug that allowed monsters to veinmine and/or give XP to the    player.
#### 0.0.1.3
 -   Indestructible items no longer lose durability. (such as items from Epic Loot)
 -   Added config option to disable mining visual effects, which *might* help reduce fps lag. (disabled by default)
#### 0.0.1.2
 -   Added config option for veinmining to take pickaxe durability for each section mined (default = true).
 -   XP is now only awarded if the section mined wasn't already destroyed.
 -   Fixed a NullReferenceException thrown when there were more than 128 rock sections.
#### 0.0.1.1
 - Mining xp is now awarded for every rock section mined.
#### 0.0.1.0
 - Initial release

