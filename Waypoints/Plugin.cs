using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using Waypoints.Managers;

namespace Waypoints;

[BepInPlugin(ModGUID, ModName, ModVersion)]
public class WaypointsPlugin : BaseUnityPlugin
{
    internal const string ModName = "Waypoints";
    internal const string ModVersion = "1.2.3";
    internal const string Author = "RustyMods";
    public const string ModGUID = Author + "." + ModName;

    internal static string ConnectionError = "";
    public readonly Harmony _harmony = new(ModGUID);

    public static WaypointsPlugin instance = null!;
    public static readonly AssetBundle _assetBundle = GetAssetBundle("waypointbundle_rs");

    public void Awake()
    {
        instance = this;
        Localizer.Load();
        Configs.Load();
        Commands.Init();
        Pieces.Load();
        LocationManager.SetupWayShrineLocation();
        var assembly = Assembly.GetExecutingAssembly();
        _harmony.PatchAll(assembly);
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

    public static void LogDebug(string msg)
    {
        instance.Logger.LogDebug(msg);
    }

    public static void LogWarning(string msg)
    {
        instance.Logger.LogWarning(msg);
    }

    public static void LogError(string msg)
    {
        instance.Logger.LogError(msg);
    }

    public static void LogInfo(string msg)
    {
        instance.Logger.LogInfo(msg);
    }

    private static AssetBundle GetAssetBundle(string fileName)
    {
        var execAssembly = Assembly.GetExecutingAssembly();
        var resourceName = execAssembly.GetManifestResourceNames().Single(str => str.EndsWith(fileName));
        using var stream = execAssembly.GetManifestResourceStream(resourceName);
        return AssetBundle.LoadFromStream(stream);
    }
}