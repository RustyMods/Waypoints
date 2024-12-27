using System.Collections.Generic;
using System.Linq;
using BepInEx;
using HarmonyLib;
using ServerSync;
using SoftReferenceableAssets;
using UnityEngine;
using Waypoints.Behaviors;

namespace Waypoints.Managers;

public class LocationManager
{
    public static readonly CustomSyncedValue<string> GenerateSync = new(WaypointsPlugin.ConfigSync, "WaypointGenerateCommand", "");
    public static int GeneratedCount = 0;
    public static int RemovedCount = 0;
    private static LocationData WaypointLocation = null!;
    static LocationManager()
    {
        Harmony harmony = new Harmony("org.bepinex.helpers.RustyLocationManager");
        harmony.Patch(AccessTools.DeclaredMethod(typeof(ZoneSystem), nameof(ZoneSystem.SetupLocations)), prefix: new HarmonyMethod(AccessTools.DeclaredMethod(typeof(LocationManager), nameof(SetupLocations_Prefix))));
        harmony.Patch(AccessTools.DeclaredMethod(typeof(Piece), nameof(Piece.Awake)), postfix: new HarmonyMethod(AccessTools.DeclaredMethod(typeof(LocationManager), nameof(PieceAwakePatch))));
    }

    internal static void PieceAwakePatch(Piece __instance)
    {
        if (!__instance || __instance.name.Replace("(Clone)", string.Empty) != "WaypointShrine" || __instance.GetComponent<Waypoint>()) return;
        __instance.gameObject.AddComponent<Waypoint>();
        __instance.gameObject.AddComponent<GlowTrails>();
    }
    private static void SetupLocations_Prefix(ZoneSystem __instance)
    {
        if (WaypointsPlugin._generateLocations.Value is WaypointsPlugin.Toggle.Off) return;
        var data = WaypointLocation.GetLocation();
        if (data.m_prefab.IsValid) __instance.m_locations.Add(data);
    }
    public static int Generate(int quantity)
    {
        if (!ZNet.instance || !ZoneSystem.instance) return 0;
        if (Player.m_localPlayer && !Player.m_localPlayer.NoCostCheat())
        {
            WaypointsPlugin.WaypointsLogger.LogInfo("Admin only, activate no cost mode");
            return 0;
        }
        if (!ZNet.instance.IsServer())
        {
            WaypointsPlugin.WaypointsLogger.LogInfo("Host Only, sending message to server to generate");
            GenerateSync.Value = "Generate:" + quantity;
            return 0;
        }
        int count = 0;
        ZoneSystem.ZoneLocation location = WaypointLocation.GetLocation();
        for (int index = 0; index < quantity; ++index)
        {
            Vector2i zoneID = ZoneSystem.GetRandomZone(location.m_maxDistance);
            if (ZoneSystem.instance.m_locationInstances.ContainsKey(zoneID)) continue;
            Vector3 zonePos = ZoneSystem.GetZonePos(zoneID);
            if (!location.m_biome.HasFlag(WorldGenerator.instance.GetBiome(zonePos))) continue;
            if (!location.m_biomeArea.HasFlag(WorldGenerator.instance.GetBiomeArea(zonePos))) continue;
            var randomPointInZone = ZoneSystem.GetRandomPointInZone(zoneID, Mathf.Max(location.m_exteriorRadius, location.m_interiorRadius));
            randomPointInZone.y = WorldGenerator.instance.GetHeight(randomPointInZone.x, randomPointInZone.z, out Color mask1);
            float num1 = randomPointInZone.y - 30f;
            if (num1 < location.m_minAltitude || num1 > location.m_maxAltitude) continue;
            if (location.m_inForest)
            {
                float forestFactor = WorldGenerator.GetForestFactor(randomPointInZone);
                if (forestFactor < location.m_forestTresholdMin ||
                    forestFactor > location.m_forestTresholdMax) continue;
            }
            WorldGenerator.instance.GetTerrainDelta(randomPointInZone, location.m_exteriorRadius, out float delta, out Vector3 _);
            if (delta > location.m_maxTerrainDelta || delta < location.m_minTerrainDelta) continue;
            if (location.m_minDistanceFromSimilar > 0.0 &&
                ZoneSystem.instance.HaveLocationInRange(location.m_prefab.Name, location.m_group, randomPointInZone,
                    location.m_minDistance)) continue;
            if (location.m_maxDistanceFromSimilar > 0.0 && !ZoneSystem.instance.HaveLocationInRange(
                    location.m_prefabName, location.m_groupMax, randomPointInZone,
                    location.m_maxDistanceFromSimilar)) continue;
            float a = mask1.a;
            if (location.m_minimumVegetation > 0.0 && a <= location.m_minimumVegetation) continue;
            if (location.m_maximumVegetation < 1.0 && a >= location.m_maximumVegetation) continue;
            
            ZoneSystem.instance.RegisterLocation(location, randomPointInZone, false);
            Debug.Log("Added waypoint shrine location at: " + $"{randomPointInZone.x} , {randomPointInZone.z}");
            ++count;
        }
        WaypointsPlugin.WaypointsLogger.LogInfo($"Generated {count} new locations");
        return count;
    }

    public static int Remove(int quantity, bool all = false)
    {
        if (!ZNet.instance || !ZoneSystem.instance) return 0;
        if (Player.m_localPlayer && !Player.m_localPlayer.NoCostCheat())
        {
            WaypointsPlugin.WaypointsLogger.LogInfo("Admin only, activate no cost mode");
            return 0;
        }

        if (!ZNet.instance.IsServer())
        {
            WaypointsPlugin.WaypointsLogger.LogInfo("Host Only, sending message to server to remove");
            GenerateSync.Value = $"Remove:{quantity}:{all}";
            return 0;
        }

        var locations = ZoneSystem.instance.GetLocationList()
            .Where(location => location.m_location.m_prefab.Name.ToLower().Contains("waypoint")).ToList();
        List<Vector2i> KeysToRemove = new();
        if (all)
        {
            foreach (var kvp in ZoneSystem.instance.m_locationInstances)
            {
                if (locations.Contains(kvp.Value))
                {
                    KeysToRemove.Add(kvp.Key);
                }
            }
        }
        else
        {
            int count = 0;
            List<KeyValuePair<Vector2i, ZoneSystem.LocationInstance>> locationInstances =
                ZoneSystem.instance.m_locationInstances.ToList();
            while (count < quantity)
            {
                try
                {
                    var kvp = locationInstances[count];
                    if (kvp.Value.m_placed) continue;
                    if (!kvp.Value.m_location.m_prefab.Name.ToLower().Contains("waypoint")) continue;
                    KeysToRemove.Add(kvp.Key);
                    ++count;
                }
                catch
                {
                    break;
                }
            }
        }
        foreach (var key in KeysToRemove)
        {
            ZoneSystem.instance.m_locationInstances.Remove(key);
        }
        WaypointsPlugin.WaypointsLogger.LogInfo($"Removed {KeysToRemove.Count} waypoint shrine locations");
        return KeysToRemove.Count;
    }
    
    public static void SetupWayShrineLocation()
    {
        WaypointLocation = new("WaypointLocation", WaypointsPlugin._assetBundle, "WaypointShrine");
        WaypointLocation.m_data.m_biome = Heightmap.Biome.All;
        WaypointLocation.m_data.m_quantity = WaypointsPlugin._locationAmount.Value;
        WaypointLocation.m_data.m_group = "Waypoints";
        WaypointLocation.m_data.m_prefabName = "WaypointLocation";
        WaypointLocation.m_data.m_prioritized = false;
        WaypointLocation.m_data.m_minDistanceFromSimilar = 1000f;
        WaypointLocation.m_data.m_surroundCheckVegetation = true;
        WaypointLocation.m_data.m_surroundCheckDistance = 10f;
        
        GenerateSync.ValueChanged += () =>
        {
            if (!ZNet.instance || !ZoneSystem.instance) return;
            if (GenerateSync.Value.IsNullOrWhiteSpace()) return;
            var parts = GenerateSync.Value.Split(':');
            var quantity = int.TryParse(parts[1], out int amount) ? amount : 0;
            if (ZNet.instance.IsServer())
            {
                if (quantity <= 0) return;
                switch (parts[0])
                {
                    case "Generate":
                        GeneratedCount = Generate(quantity);
                        WaypointsPlugin._Plugin.Invoke(nameof(WaypointsPlugin.DelayedGenerateNotice), 1f);
                        break;
                    case "Remove":
                        RemovedCount = Remove(quantity);
                        WaypointsPlugin._Plugin.Invoke(nameof(WaypointsPlugin.DelayedRemovedNotice), 1f);
                        break;
                    default: return;
                }
            }
            else
            {
                switch (parts[0])
                {
                    case "Generated":
                        WaypointsPlugin.WaypointsLogger.LogInfo($"Server generated {quantity} new waypoint shrines");
                        break;
                    case "Removed":
                        WaypointsPlugin.WaypointsLogger.LogInfo($"Server removed {quantity} waypoint shrines");
                        break;
                    default: return;
                }
            }
        };
    }

    public class LocationData
    {
        private readonly AssetID AssetID;
        public LocationData(string name, AssetBundle bundle, string waypoint = "")
        {
            if (bundle.LoadAsset<GameObject>(name) is not { } prefab)
            {
                Debug.LogWarning(name + " is null");
                return;
            }
            if (!waypoint.IsNullOrWhiteSpace()) WaypointManager.AddPrefabToSearch(waypoint);
            WaypointsPlugin.m_assetLoaderManager.AddAsset(prefab, out AssetID assetID);
            AssetID = assetID;
            m_data.m_prefabName = prefab.name;
            
            WaypointsPlugin.WaypointsLogger.LogDebug("Registered location: " + name);
        }

        public readonly ZoneData m_data = new();
        public class ZoneData
        {
            public Heightmap.Biome m_biome = Heightmap.Biome.DeepNorth;
            public bool m_enabled = true;
            public string m_prefabName = null!;
            public Heightmap.BiomeArea m_biomeArea = Heightmap.BiomeArea.Everything;
            public int m_quantity = 100;
            public bool m_prioritized = true;
            public bool m_centerFirst = false;
            public bool m_unique = false;
            public string m_group = "";
            public float m_minDistanceFromSimilar = 0f;
            public string m_groupMax = "";
            public float m_maxDistanceFromSimilar = 0f;
            public bool m_iconAlways = false;
            public bool m_iconPlaced = false;
            public bool m_randomRotation = false;
            public bool m_slopeRotation = false;
            public bool m_snapToWater = false;
            public float m_interiorRadius = 0f;
            public float m_exteriorRadius = 50f;
            public bool m_clearArea = false;
            public float m_minTerrainDelta = 0f; 
            public float m_maxTerrainDelta = 100f;
            public float m_minimumVegetation;
            public float m_maximumVegetation = 1f;
            public bool m_surroundCheckVegetation = false;
            public float m_surroundCheckDistance = 20f;
            public int m_surroundCheckLayers = 2;
            public float m_surroundBetterThanAverage;
            public bool m_inForest = false;
            public float m_forestThresholdMin = 0f;
            public float m_forestThresholdMax = 1f;
            public float m_minDistance = 0f;
            public float m_maxDistance = 10000f;
            public float m_minAltitude = 0f;
            public float m_maxAltitude = 1000f;
            public bool m_foldout = false;
        }
        
        public ZoneSystem.ZoneLocation GetLocation()
        {
            return new ZoneSystem.ZoneLocation()
            {
                m_enable = m_data.m_enabled,
                m_prefabName = m_data.m_prefabName,
                m_prefab = WaypointsPlugin.m_assetLoaderManager.GetSoftReference(AssetID),
                m_biome = m_data.m_biome,
                m_biomeArea = m_data.m_biomeArea,
                m_quantity = m_data.m_quantity,
                m_prioritized = m_data.m_prioritized,
                m_centerFirst = m_data.m_centerFirst,
                m_unique = m_data.m_unique,
                m_group = m_data.m_group,
                m_minDistanceFromSimilar = m_data.m_minDistanceFromSimilar,
                m_groupMax = m_data.m_groupMax,
                m_maxDistanceFromSimilar = m_data.m_maxDistanceFromSimilar,
                m_iconAlways = m_data.m_iconAlways,
                m_iconPlaced = m_data.m_iconPlaced,
                m_randomRotation = m_data.m_randomRotation,
                m_slopeRotation = m_data.m_slopeRotation,
                m_snapToWater = m_data.m_snapToWater,
                m_interiorRadius = m_data.m_interiorRadius,
                m_exteriorRadius = m_data.m_exteriorRadius,
                m_clearArea = m_data.m_clearArea,
                m_minTerrainDelta = m_data.m_minTerrainDelta,
                m_maxTerrainDelta = m_data.m_maxTerrainDelta,
                m_minimumVegetation = m_data.m_minimumVegetation,
                m_maximumVegetation = m_data.m_maximumVegetation,
                m_surroundCheckVegetation = m_data.m_surroundCheckVegetation,
                m_surroundCheckDistance = m_data.m_surroundCheckDistance,
                m_surroundCheckLayers = m_data.m_surroundCheckLayers,
                m_surroundBetterThanAverage = m_data.m_surroundBetterThanAverage,
                m_inForest = m_data.m_inForest,
                m_forestTresholdMin = m_data.m_forestThresholdMin,
                m_forestTresholdMax = m_data.m_forestThresholdMax,
                m_minDistance = m_data.m_minDistance,
                m_maxDistance = m_data.m_maxDistance,
                m_minAltitude = m_data.m_minAltitude,
                m_maxAltitude = m_data.m_maxAltitude,
                m_foldout = m_data.m_foldout
            };
        }
    }
}

