using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace WiseHorror.Veinmine
{
    [BepInPlugin(MOD_ID, MOD_NAME, VERSION)]
    [BepInProcess("valheim.exe")]
    [BepInProcess("valheim_server.exe")]
    [HarmonyPatch]
    public class VeinMine : BaseUnityPlugin
    {
        private const string MOD_ID = "com.wisehorror.Veinmine";
        private const string MOD_NAME = "Veinmine";
        private const string VERSION = "1.2.2";

        public static ManualLogSource logger = BepInEx.Logging.Logger.CreateLogSource("Veinmine");

        public enum spreadTypes
        {
            level,
            distance
        }

        void Awake()
        {
            veinMineKey = Config.Bind("General",
                "Veinmine",
                KeyCode.LeftAlt,
                "Key (or button) to hold for veinmining. Refer to https://docs.unity3d.com/Manual/class-InputManager.html");

            veinMineDurability = Config.Bind("General",
                "Durability",
                true,
                "Veinmining takes durability as if you mined every section manually.\nDoesn't do anything in Progressive mode, as durability is always removed there.");

            removeEffects = Config.Bind("Visual",
                "Remove Effects",
                false,
                "Remove mining visual effects to try to *possibly* reduce fps lag ");

            progressiveMode = Config.Bind("Progressive",
                "Enable Progressive",
                false,
                "Progressive mode scales the number of sections mined according to the player's Pickaxes level.");

            progressiveMult = Config.Bind("Progressive",
                "Radius Multiplier",
                0.1f,
                "Radius around your hit area to veinmine. This value is multiplied by the player's Pickaxes level.\n" +
                "Keep in mind 1 is the same length as the 1x1 wood floor.\n" +
                "So if you have Lvl 30 and set this value to 0.1, you will veinmine rocks in a radius of 3 (0.1 * 30 = 3).");

            durabilityMult = Config.Bind("Progressive",
                "Durability Multiplier",
                1f,
                "Determines durability lost when veinmining.\n" +
                "The formula is (120 - Pickaxes level) / (20 * multiplier) where \"multiplier\" is this value.");

            xpMult = Config.Bind("Progressive",
                "XP Multiplier",
                0.2f,
                "Multiplier for XP gained per rock section veinmined.");

            enableSpreadDamage = Config.Bind("Progressive",
                "Enable Spread Damage",
                false,
                "Spreads your hit damage throughout all rock sections mined, as opposed to hitting every section for the total amount of damage.");

            spreadDamageType = Config.Bind("Progressive",
                "Spread Damage Type",
                spreadTypes.distance,
                "Level: Scales damage done to each section based on your Pickaxes level.\n" +
                "Distance: Calculates damage done to each section based on your distance from it, the farther away, the less damage you do.");

            Harmony harmony = new Harmony(MOD_ID);
            harmony.PatchAll();
        }

        public static ConfigEntry<KeyCode> veinMineKey;
        public static ConfigEntry<bool> veinMineDurability;
        public static ConfigEntry<bool> removeEffects;
        public static ConfigEntry<bool> progressiveMode;
        public static ConfigEntry<bool> enableSpreadDamage;
        public static ConfigEntry<spreadTypes> spreadDamageType;
        public static ConfigEntry<float> progressiveMult;
        public static ConfigEntry<float> durabilityMult;
        public static ConfigEntry<float> xpMult;


    }
}