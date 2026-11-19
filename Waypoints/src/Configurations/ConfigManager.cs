using System;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using JetBrains.Annotations;
using ServerSync;
using UnityEngine;
using Waypoints;

namespace Waypoints;

public enum Toggle
{
    On = 1,
    Off = 0
}

public static class KeyboardExtensions
{
    public static bool IsKeyDown(this KeyboardShortcut shortcut)
    {
        return shortcut.MainKey != KeyCode.None && Input.GetKeyDown(shortcut.MainKey) &&
               shortcut.Modifiers.All(Input.GetKey);
    }

    public static bool IsKeyHeld(this KeyboardShortcut shortcut)
    {
        return shortcut.MainKey != KeyCode.None && Input.GetKey(shortcut.MainKey) &&
               shortcut.Modifiers.All(Input.GetKey);
    }
}

public class AcceptableShortcuts : AcceptableValueBase
{
    public AcceptableShortcuts() : base(typeof(KeyboardShortcut))
    {
    }

    public override object Clamp(object value) => value;
    public override bool IsValid(object value) => true;

    public override string ToDescriptionString() =>
        "# Acceptable values: " + string.Join(", ", UnityInput.Current.SupportedKeyCodes);
}

public class ConfigurationManagerAttributes
{
    [UsedImplicitly] public int? Order = null!;
    [UsedImplicitly] public bool? Browsable = null!;
    [UsedImplicitly] public string? Category = null!;
    [UsedImplicitly] public Action<ConfigEntryBase>? CustomDrawer = null!;
}

public static class ConfigManager
{
    private static string ConfigFileName;
    private static string ConfigFileFullPath;
    public static readonly ConfigSync ConfigSync;

    private static ConfigEntry<Toggle> _serverConfigLocked;


    static ConfigManager()
    {
        ConfigFileName = WaypointsPlugin.ModGUID + ".cfg";
        ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        ConfigSync = new ConfigSync(WaypointsPlugin.ModGUID)
        {
            DisplayName = WaypointsPlugin.ModName,
            CurrentVersion = WaypointsPlugin.ModVersion,
            MinimumRequiredVersion = WaypointsPlugin.ModVersion
        };

        _serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On,
            "If on, the configuration is locked and can be changed by server admins only.");
        _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);

        SetupWatcher();
    }


    private static void SetupWatcher()
    {
        FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
        watcher.Changed += ReadConfigValues;
        watcher.Created += ReadConfigValues;
        watcher.Renamed += ReadConfigValues;
        watcher.IncludeSubdirectories = true;
        watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
        watcher.EnableRaisingEvents = true;
    }

    public static ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description,
        bool synchronizedSetting = true)
    {
        ConfigDescription extendedDescription =
            new(description.Description + (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"),
                description.AcceptableValues, description.Tags);
        ConfigEntry<T> configEntry = WaypointsPlugin.instance.Config.Bind(group, name, value, extendedDescription);
        //var configEntry = Config.Bind(group, name, value, description);

        SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
        syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

        return configEntry;
    }

    public static ConfigEntry<T> config<T>(string group, string name, T value, string description,
        bool synchronizedSetting = true)
    {
        return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
    }

    private static void ReadConfigValues(object sender, FileSystemEventArgs e)
    {
        if (!File.Exists(ConfigFileFullPath)) return;
        try
        {
            WaypointsPlugin.LogDebug("ReadConfigValues called");
            WaypointsPlugin.instance.Config.Reload();
        }
        catch
        {
            WaypointsPlugin.LogError($"There was an issue loading your {ConfigFileName}");
            WaypointsPlugin.LogError("Please check your config entries for spelling and format!");
        }
    }
}