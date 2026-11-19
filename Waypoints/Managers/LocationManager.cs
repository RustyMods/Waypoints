using System.Collections.Generic;
using System.Linq;
using BepInEx;
using HarmonyLib;
using ServerSync;
using SoftReferenceableAssets;
using UnityEngine;

namespace Waypoints;

public static class LocationManager
{
    public static readonly CustomSyncedValue<string> GenerateSync = new(ConfigManager.ConfigSync, "WaypointGenerateCommand", "");
    public static int GeneratedCount = 0;
    public static int RemovedCount = 0;
    public static readonly List<LocationData> _locations = [];

    [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.SetupLocations))]
    private static class Register_Waypoint_Locations
    {
        private static void Prefix(ZoneSystem __instance)
        {
            if (!Configs.GenerateLocations) return;

            for (int i = 0; i < _locations.Count; ++i)
            {
                var data = _locations[i].GetLocation();
                if (data.m_prefab.IsValid)
                {
                    __instance.m_locations.Add(data);
                }
            }
        }
    }

    [HarmonyPatch(typeof(Piece), nameof(Piece.Awake))]
    private static class Verify_Waypoint_Components
    {
        private static void Postfix(Piece __instance)
        {
            if (!__instance || __instance.name.Replace("(Clone)", string.Empty) != "WaypointShrine" || __instance.GetComponent<Waypoint>()) return;
            __instance.gameObject.AddComponent<Waypoint>();
            __instance.gameObject.AddComponent<GlowTrails>();
        }
    }
    
    public static int Generate(int quantity, Terminal? context = null)
    {
        if (!ZNet.instance || !ZoneSystem.instance) return 0;
        if (!Terminal.m_cheat)
        {
            context?.LogError("Admin only, activate devcommands");
            return 0;
        }
        if (!ZNet.instance.IsServer())
        {
            context?.LogInfo("Host Only, sending message to server to generate");
            GenerateSync.Value = "Generate:" + quantity;
            return 0;
        }
        int count = 0;
        for (int index = 0; index < quantity; ++index)
        {
            foreach (var data in _locations)
            {
                var location = data.GetLocation();
                Vector2s zoneID = ZoneSystem.GetRandomZone(location.m_maxDistance);
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
                    ZoneSystem.instance.HaveLocationInRange(location.m_prefab.m_assetID, location.m_group, randomPointInZone,
                        location.m_minDistance)) continue;
                if (location.m_maxDistanceFromSimilar > 0.0 && !ZoneSystem.instance.HaveLocationInRange(
                        location.m_prefab.m_assetID, location.m_groupMax, randomPointInZone,
                        location.m_maxDistanceFromSimilar)) continue;
                float a = mask1.a;
                if (location.m_minimumVegetation > 0.0 && a <= location.m_minimumVegetation) continue;
                if (location.m_maximumVegetation < 1.0 && a >= location.m_maximumVegetation) continue;
                
                ZoneSystem.instance.RegisterLocation(location, randomPointInZone, false);
                context?.LogDebug("Added waypoint shrine location at: " + $"{randomPointInZone.x} , {randomPointInZone.z}");
                ++count;
            }
        }
        context?.LogInfo($"Generated {count} new locations");
        return count;
    }

    public static int Remove(int quantity, bool all = false)
    {
        if (!ZNet.instance || !ZoneSystem.instance) return 0;
        if (Player.m_localPlayer && !Player.m_localPlayer.NoCostCheat())
        {
            Console.instance.LogError("Admin only, activate no cost mode");
            return 0;
        }

        if (!ZNet.instance.IsServer())
        {
            Console.instance.LogInfo("Host Only, sending message to server to remove");
            GenerateSync.Value = $"Remove:{quantity}:{all}";
            return 0;
        }

        List<Vector2s> KeysToRemove = [];
        if (all)
        {
            KeysToRemove.AddRange(from kvp in ZoneSystem.instance.m_locationInstances where kvp.Value.m_location.m_prefab.Name.ToLower().Contains("waypoint") select kvp.Key);
        }
        else
        {
            int count = 0;
            List<KeyValuePair<Vector2s, ZoneSystem.LocationInstance>> locationInstances =
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
        WaypointsPlugin.LogInfo($"Removed {KeysToRemove.Count} waypoint shrine locations");
        return KeysToRemove.Count;
    }
    
    public static void SetupWayShrineLocation()
    {
        var prefab = WaypointsPlugin._assetBundle.LoadAsset<GameObject>("WaypointLocation");
        AssetLoaderManager.Instance.AddAsset(prefab, out AssetID id);
        WaypointManager.AddPrefabToSearch("WaypointShrine");

        var meadows = new LocationData(prefab.name, "Meadows_WaypointShrine", id)
        {
            m_data =
            {
                m_biome = Heightmap.Biome.Meadows,
                m_quantity = Configs.LocationQuantity,
                m_group = "Waypoints",
                m_prioritized = false,
                m_minDistanceFromSimilar = Configs.MinDistanceFromSimilar,
                m_surroundCheckVegetation = true,
                m_surroundCheckDistance = 10f,
            }
        };

        var blackforest = new LocationData(prefab.name , "Blackforest_WaypointShrine", id)
        {
            m_data =
            {
                m_biome = Heightmap.Biome.BlackForest,
                m_quantity = Configs.LocationQuantity,
                m_group = "Waypoints",
                m_prioritized = false,
                m_minDistanceFromSimilar = Configs.MinDistanceFromSimilar,
                m_surroundCheckVegetation = true,
                m_surroundCheckDistance = 10f,
            }
        };

        var swamp = new LocationData(prefab.name, "Swamp_WaypointShrine", id)
        {
            m_data =
            {
                m_biome = Heightmap.Biome.Swamp,
                m_quantity = Configs.LocationQuantity,
                m_group = "Waypoints",
                m_prioritized = false,
                m_minDistanceFromSimilar = Configs.MinDistanceFromSimilar,
                m_surroundCheckVegetation = true,
                m_surroundCheckDistance = 10f,
            }
        };

        var mountains = new LocationData(prefab.name, "Nountain_WaypointShrine", id)
        {
            m_data =
            {
                m_biome = Heightmap.Biome.Mountain,
                m_quantity = Configs.LocationQuantity,
                m_group = "Waypoints",
                m_prioritized = false,
                m_minDistanceFromSimilar = Configs.MinDistanceFromSimilar,
                m_surroundCheckVegetation = true,
                m_surroundCheckDistance = 10f,
            }
        };

        var plains = new LocationData(prefab.name, "Plains_WaypointShrine", id)
        {
            m_data =
            {
                m_biome = Heightmap.Biome.Swamp,
                m_quantity = Configs.LocationQuantity,
                m_group = "Waypoints",
                m_prioritized = false,
                m_minDistanceFromSimilar = Configs.MinDistanceFromSimilar,
                m_surroundCheckVegetation = true,
                m_surroundCheckDistance = 10f,
            }
        };

        var mistlands = new LocationData(prefab.name,"Mistlands_WaypointShrine",id)
        {
            m_data =
            {
                m_biome = Heightmap.Biome.Mistlands,
                m_quantity = Configs.LocationQuantity,
                m_group = "Waypoints",
                m_prioritized = false,
                m_minDistanceFromSimilar = Configs.MinDistanceFromSimilar,
                m_surroundCheckVegetation = false,
                m_surroundCheckDistance = 20f,
                m_maximumVegetation = 1f,
            }
        };

        var ashlands = new LocationData(prefab.name,"Ashlands_WaypointShrine", id)
        {
            m_data =
            {
                m_biome = Heightmap.Biome.AshLands,
                m_quantity = Configs.LocationQuantity,
                m_group = "Waypoints",
                m_prioritized = false,
                m_minDistanceFromSimilar = Configs.MinDistanceFromSimilar,
                m_surroundCheckVegetation = false,
                m_surroundCheckDistance = 20f,
                m_maximumVegetation = 0.1f,
                m_minAltitude = 20f,
                m_maxAltitude = 1000f,
            }
        };

        var deepnorth = new LocationData(prefab.name, "DeepNorth_WaypointShrine", id)
        {
            m_data =
            {
                m_biome = Heightmap.Biome.DeepNorth,
                m_biomeArea = Heightmap.BiomeArea.Median,
                m_quantity = Configs.LocationQuantity,
                m_group = "Waypoints",
                m_prioritized = false,
                m_minDistanceFromSimilar = Configs.MinDistanceFromSimilar,
                m_surroundCheckVegetation = false,
                m_surroundCheckDistance = 20f,
                m_minAltitude = 2f,
                m_maxTerrainDelta = 3f,
                m_maxAltitude = 1000f,
                m_maximumVegetation = 1f
            }
        };

        GenerateSync.ValueChanged += () =>
        {
            if (!ZNet.instance || !ZoneSystem.instance) return;
            if (GenerateSync.Value.IsNullOrWhiteSpace()) return;
            var parts = GenerateSync.Value.Split(':');
            var quantity = parts.GetInt(1);
            if (ZNet.instance.IsServer())
            {
                if (quantity <= 0) return;
                switch (parts.GetString(0))
                {
                    case "Generate":
                        GeneratedCount = Generate(quantity);
                        WaypointsPlugin.instance.Invoke(nameof(WaypointsPlugin.DelayedGenerateNotice), 1f);
                        break;
                    case "Remove":
                        RemovedCount = Remove(quantity);
                        WaypointsPlugin.instance.Invoke(nameof(WaypointsPlugin.DelayedRemovedNotice), 1f);
                        break;
                    default: return;
                }
            }
            else
            {
                switch (parts.GetString(0))
                {
                    case "Generated":
                        WaypointsPlugin.LogDebug($"Server generated {quantity} new waypoint shrines");
                        Console.instance.LogInfo($"Server generated {quantity} new waypoint shrines");
                        break;
                    case "Removed":
                        WaypointsPlugin.LogDebug($"Server removed {quantity} waypoint shrines");
                        Console.instance.LogInfo($"Server removed {quantity} waypoint shrines");
                        break;
                    default: return;
                }
            }
        };
    }

    
}

