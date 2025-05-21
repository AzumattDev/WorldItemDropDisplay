using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace WorldItemDropDisplay
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class WorldItemDropDisplayPlugin : BaseUnityPlugin
    {
        internal const string ModName = "WorldItemDropDisplay";
        internal const string ModVersion = "1.0.5";
        internal const string Author = "Azumatt";
        private const string ModGUID = $"{Author}.{ModName}";
        private static string ConfigFileName = $"{ModGUID}.cfg";
        private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        internal static string ConnectionError = "";
        private readonly Harmony _harmony = new(ModGUID);
        public static readonly ManualLogSource WorldItemDropDisplayLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);
        private FileSystemWatcher _watcher = null!;
        private readonly object _reloadLock = new();
        private DateTime _lastConfigReloadTime;
        private const long RELOAD_DELAY = 10000000; // One second

        public enum Toggle
        {
            Off,
            On
        }

        public void Awake()
        {
            bool saveOnSet = Config.SaveOnConfigSet;
            Config.SaveOnConfigSet = false;

            ItemPositionInterval = config("1 - General", "Position Interval", 0.01f, "How often (seconds) to refresh position");
            ItemDataInterval = config("1 - General", "Data Interval", 0.01f, "How often (seconds) to refresh data on the item");
            ItemMaxDisplayDistance = config("1 - General", "Max Display Distance", 5f, "Maximum distance to show the world item display");
            ItemWorldOffset = config("1 - General", "World Item Offset", new Vector3(0, 1.2f, 0), "Offset that the world item display will be relative to the item");
            SubtractCamOffset = config("1 - General", "Subtract Camera Offset", Toggle.Off, "Subtract the camera offset from the world item display position, might help it look more centered on the object.");


            ItemPositionInterval.SettingChanged += (sender, args) => { ItemDropDisplayManager.Instance.UpdatePositionInterval(ItemPositionInterval.Value); };
            ItemDataInterval.SettingChanged += (sender, args) => { ItemDropDisplayManager.Instance.UpdateDataInterval(ItemDataInterval.Value); };
            ItemMaxDisplayDistance.SettingChanged += (sender, args) => { ItemDropDisplayManager.Instance.UpdateMaxDisplayDistance(ItemMaxDisplayDistance.Value); };
            ItemWorldOffset.SettingChanged += (sender, args) => { ItemDropDisplayManager.Instance.UpdateWorldOffset(ItemWorldOffset.Value); };


            ShowUIBackground = config("1 - UI", "Show Background", Toggle.On, "Show the background behind the item, in the item drop display");
            ShowAmount = config("1 - UI", "Show Amount", Toggle.On, "Show the stack-count text for stackable items, in the item drop display");
            ShowQuality = config("1 - UI", "Show Quality", Toggle.On, "Show the quality number, in the item drop display");
            ShowDurability = config("1 - UI", "Show Durability", Toggle.On, "Show the durability bar when applicable, in the item drop display");
            ShowNoTeleport = config("1 - UI", "Show No Teleport Icon", Toggle.On, "Show icon when item cannot be teleported, in the item drop display");
            ShowFoodIcon = config("1 - UI", "Show Food Icon", Toggle.On, "Show the food icon for consumables (eitr, health, stamina forks), in the item drop display");

            EventHandler onConfigChanged = (sender, args) => ItemDropDisplayManager.Instance?.ReloadAllConfigs();
            ShowUIBackground.SettingChanged += onConfigChanged;
            ShowAmount.SettingChanged += onConfigChanged;
            ShowQuality.SettingChanged += onConfigChanged;
            ShowDurability.SettingChanged += onConfigChanged;
            ShowNoTeleport.SettingChanged += onConfigChanged;
            ShowFoodIcon.SettingChanged += onConfigChanged;


            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();

            Config.Save();
            if (saveOnSet)
            {
                Config.SaveOnConfigSet = saveOnSet;
            }
        }

        private void OnDestroy()
        {
            SaveWithRespectToConfigSet();
            _watcher?.Dispose();
        }

        private void SetupWatcher()
        {
            _watcher = new FileSystemWatcher(Paths.ConfigPath, ConfigFileName);
            _watcher.Changed += ReadConfigValues;
            _watcher.Created += ReadConfigValues;
            _watcher.Renamed += ReadConfigValues;
            _watcher.IncludeSubdirectories = true;
            _watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            _watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            DateTime now = DateTime.Now;
            long time = now.Ticks - _lastConfigReloadTime.Ticks;
            if (time < RELOAD_DELAY)
            {
                return;
            }

            lock (_reloadLock)
            {
                if (!File.Exists(ConfigFileFullPath))
                {
                    WorldItemDropDisplayLogger.LogWarning("Config file does not exist. Skipping reload.");
                    return;
                }

                try
                {
                    WorldItemDropDisplayLogger.LogDebug("Reloading configuration...");
                    SaveWithRespectToConfigSet(true);
                    WorldItemDropDisplayLogger.LogInfo("Configuration reload complete.");
                }
                catch (Exception ex)
                {
                    WorldItemDropDisplayLogger.LogError($"Error reloading configuration: {ex.Message}");
                }
            }

            _lastConfigReloadTime = now;
        }

        private void SaveWithRespectToConfigSet(bool reload = false)
        {
            bool originalSaveOnSet = Config.SaveOnConfigSet;
            Config.SaveOnConfigSet = false;
            if (reload)
                Config.Reload();
            Config.Save();
            if (originalSaveOnSet)
            {
                Config.SaveOnConfigSet = originalSaveOnSet;
            }
        }


        #region ConfigOptions

        internal static ConfigEntry<float> ItemPositionInterval = null!;
        internal static ConfigEntry<float> ItemDataInterval = null!;
        internal static ConfigEntry<float> ItemMaxDisplayDistance = null!;
        internal static ConfigEntry<Vector3> ItemWorldOffset = null!;
        internal static ConfigEntry<Toggle> SubtractCamOffset = null!;
        internal static ConfigEntry<Toggle> ShowUIBackground = null!;
        internal static ConfigEntry<Toggle> ShowAmount = null!;
        internal static ConfigEntry<Toggle> ShowQuality = null!;
        internal static ConfigEntry<Toggle> ShowDurability = null!;
        internal static ConfigEntry<Toggle> ShowNoTeleport = null!;
        internal static ConfigEntry<Toggle> ShowFoodIcon = null!;

        private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description)
        {
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, description);
            return configEntry;
        }

        private ConfigEntry<T> config<T>(string group, string name, T value, string description)
        {
            return config(group, name, value, new ConfigDescription(description));
        }

        #endregion
    }

    public static class ToggleExtensions
    {
        public static bool IsOn(this WorldItemDropDisplayPlugin.Toggle toggle)
        {
            return toggle == WorldItemDropDisplayPlugin.Toggle.On;
        }

        public static bool IsOff(this WorldItemDropDisplayPlugin.Toggle toggle)
        {
            return toggle == WorldItemDropDisplayPlugin.Toggle.Off;
        }
    }
}