﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using PieceManager;
using ServerSync;
using UnityEngine;
using Waypoints.Behaviors;
using Waypoints.Managers;

namespace Waypoints
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class WaypointsPlugin : BaseUnityPlugin
    {
        internal const string ModName = "Waypoints";
        internal const string ModVersion = "1.1.1";
        internal const string Author = "RustyMods";
        private const string ModGUID = Author + "." + ModName;
        private static readonly string ConfigFileName = ModGUID + ".cfg";
        private static readonly string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        internal static string ConnectionError = "";
        private readonly Harmony _harmony = new(ModGUID);
        public static readonly ManualLogSource WaypointsLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);
        public static readonly ConfigSync ConfigSync = new(ModGUID) { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };
        public enum Toggle { On = 1, Off = 0 }

        public static WaypointsPlugin _Plugin = null!;
        public static readonly AssetBundle _assetBundle = GetAssetBundle("waypointbundle");
        public static AssetLoaderManager m_assetLoaderManager = null!;

        private static ConfigEntry<Toggle> _serverConfigLocked = null!;
        public static ConfigEntry<Toggle> _generateLocations = null!;
        public static ConfigEntry<Toggle> _usesCharges = null!;
        public static ConfigEntry<string> _chargeItem = null!;
        public static ConfigEntry<int> _chargeMax = null!;
        public static ConfigEntry<int> _chargeDecay = null!;
        public static ConfigEntry<Toggle> _Decays = null!;
        public static ConfigEntry<int> _cost = null!;
        public static ConfigEntry<Toggle> _TeleportAnything = null!;
        public static ConfigEntry<Toggle> _UseKeys = null!;
        public static ConfigEntry<Toggle> _TeleportTames = null!;
        public static ConfigEntry<Toggle> _onlyAdminRenames = null!;
        public static ConfigEntry<Toggle> _teleportToBed = null!;
        public static ConfigEntry<Toggle> _teleportToLocations = null!;
        public static ConfigEntry<float> _distancePerCharge = null!;
        public static ConfigEntry<Toggle> _useDistanceCharge = null!;
        public static ConfigEntry<int> _locationAmount = null!;
        private static ConfigEntry<string> _separator = null!;
        public static ConfigEntry<Toggle> _showConnectionTrails = null!;
        public static ConfigEntry<float> _connectionMaxRange = null!;
        public static readonly Dictionary<string, ConfigEntry<string>> keyConfigs = new();

        public void LoadConfigs()
        {
            _serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On, "If on, the configuration is locked and can be changed by server admins only.");
            _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);
            _generateLocations = config("2 - Settings", "0 - Locations", Toggle.On, "If on, waypoints will generate along side game locations");
            _locationAmount = config("2 - Settings", "1 - Amount", 200, "Set amount of waypoint locations to attempt to generate");
            _onlyAdminRenames = config("2 - Settings", "2 - Only Admin Renames", Toggle.Off, "If on, only admins can rename waypoints");
            _TeleportAnything = config("2 - Settings", "3 - Teleport Anything", Toggle.Off, "If on, player can teleport non-teleportable items");
            _UseKeys = config("2 - Settings", "4 - Use Keys", Toggle.Off, "If on, portal checks if game has global key to allow teleportation of non-teleportable items");
            _TeleportTames = config("2 - Settings", "5 - Teleport Tames", Toggle.Off, "If on, portal can teleport tames that are following player");
            _teleportToBed = config("2 - Settings", "6 - Teleport To Bed", Toggle.On, "If on, players can teleport to their beds");
            _teleportToLocations = config("2 - Settings", "7 - Teleport To Locations", Toggle.On, "If on, players can teleport to locations");
            
            _usesCharges = config("3 - Charge System", "0 - Enabled", Toggle.Off, "If on, waypoints use charge system");
            _chargeItem = config("3 - Charge System", "1 - Charge Item", "GreydwarfEye", "Set charge item");
            _chargeMax = config("3 - Charge System", "2 - Charge Max", 50, "Set max charge");
            _Decays = config("3 - Charge System", "3 - Charge Decays", Toggle.On, "If on, portal charge decays over time");
            _chargeDecay = config("3 - Charge System", "4 - Minute between decay", 5, "Set loss of charge time in minutes");
            _cost = config("3 - Charge System", "5 - Cost", 1, "Set base charge cost to teleport");
            _useDistanceCharge = config("3 - Charge System", "6 - Dynamic Cost", Toggle.Off, "If on, waypoints calculate distance of destination to evaluate cost");
            _distancePerCharge = config("3 - Charge System", "7 - Distance Units Per Charge", 100f, "Units of distance per charge, higher number reduces cost");
            _separator = config("1 - General", "Vector Separator", ",", "Set the separator used to parse vectors");
            _showConnectionTrails = config("4 - Trails", "_Enable", Toggle.On, "If on, trails appear when players get near unknown waypoint");
            _connectionMaxRange = config("4 - Trails", "Max Range", 100f, "Set max range for trails to start appearing");
        }

        public static char GetSeparator()
        {
            return char.TryParse(_separator.Value, out char separator) ? separator : ',';
        }
        private static AssetBundle GetAssetBundle(string fileName)
        {
            Assembly execAssembly = Assembly.GetExecutingAssembly();
            string resourceName = execAssembly.GetManifestResourceNames().Single(str => str.EndsWith(fileName));
            using Stream? stream = execAssembly.GetManifestResourceStream(resourceName);
            return AssetBundle.LoadFromStream(stream);
        }

        private void LoadPieces()
        {
            BuildPiece Waypoint = new BuildPiece(_assetBundle, "WaypointShrine");
            Waypoint.Name.English("Waypoint Shrine");
            Waypoint.Description.English("Use charge item on waypoint to charge it, requires to touch to learn location");
            Waypoint.Category.Set(BuildPieceCategory.Misc);
            Waypoint.RequiredItems.Add("SwordCheat", 1, false);
            Waypoint.Prefab.AddComponent<Waypoint>();
            Waypoint.Prefab.AddComponent<GlowTrails>();
            WaypointManager.AddPrefabToSearch(Waypoint.Prefab.name);
            MaterialReplacer.RegisterGameObjectForShaderSwap(Waypoint.Prefab, MaterialReplacer.ShaderType.PieceShader);
            Waypoint.PlaceEffects = new() { "vfx_Place_workbench", "sfx_build_hammer_stone" };
            Waypoint.DestroyedEffects = new() { "vfx_RockDestroyed", "sfx_rock_destroyed" };
            Waypoint.HitEffects = new() { "vfx_RockHit" };
            Waypoint.SwitchEffects = new() { "vfx_Place_throne02" };
            Waypoint.SpecialProperties = new SpecialProperties()
            {
                AdminOnly = true,
            };
            
            BuildPiece WaypointPortal = new BuildPiece(_assetBundle, "WaypointShrinePortal");
            WaypointPortal.Name.English("Waypoint Portal");
            WaypointPortal.Description.English("Bypass need to know it to use");
            WaypointPortal.Category.Set(BuildPieceCategory.Misc);
            WaypointPortal.RequiredItems.Add("SwordCheat", 1, false);
            Waypoint component = WaypointPortal.Prefab.AddComponent<Waypoint>();
            WaypointPortal.Prefab.AddComponent<GlowTrails>();
            component.m_poi = true;
            WaypointManager.AddPrefabToSearch(WaypointPortal.Prefab.name);
            MaterialReplacer.RegisterGameObjectForShaderSwap(WaypointPortal.Prefab, MaterialReplacer.ShaderType.PieceShader);
            WaypointPortal.PlaceEffects = new() { "vfx_Place_workbench", "sfx_build_hammer_stone" };
            WaypointPortal.DestroyedEffects = new() { "vfx_RockDestroyed", "sfx_rock_destroyed" };
            WaypointPortal.HitEffects = new() { "vfx_RockHit" };
            WaypointPortal.SwitchEffects = new() { "vfx_Place_throne02" };
        }
        public void Awake()
        {
            _Plugin = this;
            Localizer.Load();
            m_assetLoaderManager = new AssetLoaderManager(_Plugin.Info.Metadata);
            LoadConfigs();
            LoadPieces();
            LocationManager.SetupWayShrineLocation();
            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
        }

        public void DelayedGenerateNotice()
        {
            LocationManager.GenerateSync.Value = $"Generated:{LocationManager.GeneratedCount}";
            Invoke(nameof(DelayedLocationCountUpdate), 1f);
        }

        public void DelayedRemovedNotice()
        {
            LocationManager.GenerateSync.Value = $"Removed:{LocationManager.RemovedCount}";
            Invoke(nameof(DelayedLocationCountUpdate), 1f);
        }

        public void DelayedLocationCountUpdate()
        {
            WaypointManager.UpdateServerLocationData(ZoneSystem.instance);
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
                WaypointsLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                WaypointsLogger.LogError($"There was an issue loading your {ConfigFileName}");
                WaypointsLogger.LogError("Please check your config entries for spelling and format!");
            }
        }


        public ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description,
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

        public ConfigEntry<T> config<T>(string group, string name, T value, string description,
            bool synchronizedSetting = true)
        {
            return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
        }

        private class ConfigurationManagerAttributes
        {
            [UsedImplicitly] public int? Order;
            [UsedImplicitly] public bool? Browsable;
            [UsedImplicitly] public string? Category;
            [UsedImplicitly] public Action<ConfigEntryBase>? CustomDrawer;
        }
    }
}