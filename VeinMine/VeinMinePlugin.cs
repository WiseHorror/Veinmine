using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using ServerSync;
using UnityEngine;

namespace Veinmine
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class VeinMinePlugin : BaseUnityPlugin
    {
        internal const string ModName = "Veinmine";
        internal const string ModVersion = "1.2.8";
        internal const string Author = "wisehorror";
        private const string ModGUID = $"com.{Author}.{ModName}";
        private static string ConfigFileName = $"{ModGUID}.cfg";
        private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        internal static string ConnectionError = "";
        private readonly Harmony _harmony = new(ModGUID);
        public static ManualLogSource logger = BepInEx.Logging.Logger.CreateLogSource(ModName);

        private static readonly ConfigSync ConfigSync = new(ModGUID) { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };

        public enum SpreadTypes
        {
            Level,
            Distance
        }

        public enum Toggle
        {
            On = 1,
            Off = 0
        }

        void Awake()
        {
            _serverConfigLocked = config("1 - General",
                "Lock Configuration",
                Toggle.On,
                "If on, the configuration is locked and can be changed by server admins only.");
            _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);


            veinMineKey = config("2 - General",
                "Veinmine",
                new KeyboardShortcut(KeyCode.LeftAlt),
                "Key (or button) to hold for veinmining. Refer to https://docs.unity3d.com/Manual/class-InputManager.html", false);

            veinMineDurability = config("2 - General",
                "Durability",
                Toggle.On,
                "Veinmining takes durability as if you mined every section manually.");

            removeEffects = config("3 - Visual",
                "Remove Effects",
                Toggle.Off,
                "Remove mining visual effects to try to *possibly* reduce fps lag", false);

            progressiveMode = config("4 - Progressive",
                "Enable Progressive",
                Toggle.Off,
                "Progressive mode scales the number of sections mined according to the player's Pickaxes level.");

            progressiveMult = config("4 - Progressive",
                "Radius Multiplier",
                0.1f,
                "Radius around your hit area to veinmine. This value is multiplied by the player's Pickaxes level.\n" +
                "Keep in mind 1 is the same length as the 1x1 wood floor.\n" +
                "So if you have Lvl 30 and set this value to 0.1, you will veinmine rocks in a radius of 3 (0.1 * 30 = 3).");

            durabilityMult = config("4 - Progressive",
                "Durability Multiplier",
                1f,
                "Determines durability lost when veinmining.\n" +
                "The formula is (120 - Pickaxes level) / (20 * multiplier) where \"multiplier\" is this value.");

            xpMult = config("4 - Progressive",
                "XP Multiplier",
                0.2f,
                "Multiplier for XP gained per rock section veinmined.");

            enableSpreadDamage = config("4 - Progressive",
                "Enable Spread Damage",
                Toggle.Off,
                "Spreads your hit damage throughout all rock sections mined, as opposed to hitting every section for the total amount of damage.");

            spreadDamageType = config("4 - Progressive",
                "Spread Damage Type",
                SpreadTypes.Distance,
                "Level: Scales damage done to each section based on your Pickaxes level.\n" +
                "Distance: Calculates damage done to each section based on your distance from it, the farther away, the less damage you do.");

            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
        }

        private void OnDestroy()
        {
            Config.Save();
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                logger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                logger.LogError($"There was an issue loading your {ConfigFileName}");
                logger.LogError("Please check your config entries for spelling and format!");
            }
        }

        #region ConfigOptions

        private static ConfigEntry<Toggle> _serverConfigLocked = null!;
        public static ConfigEntry<KeyboardShortcut> veinMineKey = null!;
        public static ConfigEntry<Toggle> veinMineDurability = null!;
        public static ConfigEntry<Toggle> removeEffects = null!;
        public static ConfigEntry<Toggle> progressiveMode = null!;
        public static ConfigEntry<Toggle> enableSpreadDamage = null!;
        public static ConfigEntry<SpreadTypes> spreadDamageType = null!;
        public static ConfigEntry<float> progressiveMult = null!;
        public static ConfigEntry<float> durabilityMult = null!;
        public static ConfigEntry<float> xpMult = null!;

        private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description,
            bool synchronizedSetting = true)
        {
            ConfigDescription extendedDescription =
                new(
                    description.Description +
                    (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"),
                    description.AcceptableValues, description.Tags);
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);
            //var configEntry = Config.Bind(group, name, value, description);

            SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        private ConfigEntry<T> config<T>(string group, string name, T value, string description,
            bool synchronizedSetting = true)
        {
            return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
        }

        private class ConfigurationManagerAttributes
        {
            [UsedImplicitly] public int? Order = null!;
            [UsedImplicitly] public bool? Browsable = null!;
            [UsedImplicitly] public string Category = null!;
            [UsedImplicitly] public Action<ConfigEntryBase> CustomDrawer = null!;
        }

        class AcceptableShortcuts : AcceptableValueBase
        {
            public AcceptableShortcuts() : base(typeof(KeyboardShortcut))
            {
            }

            public override object Clamp(object value) => value;
            public override bool IsValid(object value) => true;

            public override string ToDescriptionString() =>
                $"# Acceptable values: {string.Join(", ", UnityInput.Current.SupportedKeyCodes)}";
        }

        #endregion
    }

    public static class KeyboardExtensions
    {
        public static bool IsKeyDown(this KeyboardShortcut shortcut)
        {
            return shortcut.MainKey != KeyCode.None && Input.GetKeyDown(shortcut.MainKey) && shortcut.Modifiers.All(Input.GetKey);
        }

        public static bool IsKeyHeld(this KeyboardShortcut shortcut)
        {
            return shortcut.MainKey != KeyCode.None && Input.GetKey(shortcut.MainKey) && shortcut.Modifiers.All(Input.GetKey);
        }
    }
}