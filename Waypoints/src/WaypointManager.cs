using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using ServerSync;
using UnityEngine;

namespace Waypoints;

public static class WaypointManager
{
    private static readonly List<ZDO> m_tempZDOs = [];
    private static readonly List<string> m_prefabsToSearch = [];
    public static bool m_teleportToUnplaced;
    private static readonly WaitForSeconds interval = new(10f);

    public static readonly CustomSyncedValue<string> m_locationWaypoints = new (
        ConfigManager.ConfigSync, 
        "CustomSyncedWaypointsData", 
        "");
    public static void AddPrefabToSearch(string prefabName)
    {
        if (m_prefabsToSearch.Contains(prefabName)) return;
        m_prefabsToSearch.Add(prefabName);
    }

    private static void InitCoroutine() => WaypointsPlugin.instance.StartCoroutine(SendWaypointDestinations());
    private static IEnumerator SendWaypointDestinations()
    {
        WaypointsPlugin.LogDebug("Initialized waypoint coroutine");
        for (;;)
        {
            if (!Game.instance || ZDOMan.instance == null || !ZNet.instance || !ZNet.instance.IsServer()) continue;
            m_tempZDOs.Clear();
            foreach (string prefab in m_prefabsToSearch)
            {
                int index = 0;
                while (!ZDOMan.instance.GetAllZDOsWithPrefabIterative(prefab, m_tempZDOs, ref index))
                {
                    yield return null;
                }
            }

            foreach (ZDO zdo in m_tempZDOs)
            {
                ZDOMan.instance.ForceSendZDO(zdo.m_uid);
            }

            yield return interval;
        }
    }
    public static void UpdateServerLocationData(ZoneSystem __instance)
    {
        if (!ZNet.instance || !ZNet.instance.IsServer()) return;
        List<ZoneSystem.LocationInstance> waypoints = __instance.GetLocationList().Where(location => location.m_location.m_prefab.Name.ToLower().Contains("waypoint")).ToList();
        m_locationWaypoints.Value = string.Join(";", waypoints.Select(waypoint => WaypointExt.FormatPosition(waypoint.m_position)));
        WaypointsPlugin.LogDebug($"Registered {waypoints.Count} waypoint locations on the server");
    }
    public static ZDO? GetDestination(Vector3 position) => FindDestinations().ToList().Find(x => Waypoint.MatchFound(x.m_position, position));
    public static HashSet<ZDO> FindDestinations()
    {
        if (ZDOMan.instance == null) return [];
        List<ZDO> destinations = [];
        foreach (string prefab in m_prefabsToSearch)
        {
            int amount = 0;
            while (!ZDOMan.instance.GetAllZDOsWithPrefabIterative(prefab, destinations, ref amount))
            {
            }
        }

        return [..destinations];
    }

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Awake))]
    private static class ZNet_Awake_Patch
    {
        private static void Postfix(ZNet __instance)
        {
            if (!__instance || !__instance.IsServer()) return;
            InitCoroutine();
        }
    }

    [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.GenerateLocationsIfNeeded))]
    private static class RegisterGeneratedWaypoints
    {
        private static void Postfix(ZoneSystem __instance) => UpdateServerLocationData(__instance);
    }

    [HarmonyPatch(typeof(Game), nameof(Game.Logout))]
    private static class Game_Logout_Patch
    {
        private static void Postfix() => WaypointsPlugin.instance.StopAllCoroutines();
    }
}