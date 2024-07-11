using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using ServerSync;
using UnityEngine;
using Waypoints.Behaviors;
using YamlDotNet.Serialization;

namespace Waypoints.Managers;

public static class WaypointManager
{
    private static readonly List<ZDO> m_tempZDOs = new();
    private static readonly List<string> m_prefabsToSearch = new();
    public static bool m_teleportToUnplaced;

    private static readonly CustomSyncedValue<List<string>> m_locationWaypoints = new CustomSyncedValue<List<string>>(WaypointsPlugin.ConfigSync, "CustomSyncedWaypointsData", new());
    public static void AddPrefabToSearch(string prefabName)
    {
        if (m_prefabsToSearch.Contains(prefabName)) return;
        m_prefabsToSearch.Add(prefabName);
    }

    private static void InitCoroutine() => WaypointsPlugin._Plugin.StartCoroutine(SendWaypointDestinations());
    private static IEnumerator SendWaypointDestinations()
    {
        
        WaypointsPlugin.WaypointsLogger.LogDebug("Initialized waypoint coroutine");
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

            yield return new WaitForSeconds(10f);
        }
    }
    
    private static void UpdateServerLocationData()
    {
        if (!ZNet.instance || !ZNet.instance.IsServer()) return;
        
        List<ZoneSystem.LocationInstance> waypoints = ZoneSystem.instance.GetLocationList().Where(location => location.m_location.m_prefab.Name.ToLower().Contains("waypoint")).ToList();

        var data = new List<string>();
        int count = 0;
        foreach (ZoneSystem.LocationInstance waypoint in waypoints)
        {
            data.Add(Waypoint.FormatPosition(waypoint.m_position));
            ++count;
        }

        m_locationWaypoints.Value = data;
        WaypointsPlugin.WaypointsLogger.LogDebug($"Registered {count} waypoint locations on the server");
    }

    public static ZDO? GetDestination(Vector3 position) => FindDestinations().ToList().Find(x => Waypoint.MatchFound(x.m_position, position));

    public static HashSet<ZDO> FindDestinations()
    {
        if (ZDOMan.instance == null) return new();
        List<ZDO> destinations = new();
        foreach (string prefab in m_prefabsToSearch)
        {
            int amount = 0;
            while (!ZDOMan.instance.GetAllZDOsWithPrefabIterative(prefab, destinations, ref amount))
            {
            }
        }

        return new HashSet<ZDO>(destinations);
    }

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Awake))]
    private static class ZNet_Awake_Patch
    {
        private static void Postfix(ZNet __instance)
        {
            if (!__instance) return;
            if (!__instance.IsServer()) return;
            InitCoroutine();
        }
    }

    [HarmonyPatch(typeof(Terminal), nameof(Terminal.Awake))]
    private static class RegisterWaypointCommands
    {
        private static void Postfix()
        {
            Terminal.ConsoleCommand commands = new("waypoint", "Use help to list commands",
                (Terminal.ConsoleEventFailable)(
                    args =>
                    {
                        if (args.Length < 2) return false;
                        switch (args[1])
                        {
                            case "help":
                                ListCommandOptions();
                                break;
                            case "list":
                                ListKnownWaypoints();
                                break;
                            case "reset":
                                ResetKnownWaypoints();
                                break;
                            case "reveal":
                                RevealAllWaypoints(args);
                                break;
                        }
                        return true;
                    }), optionsFetcher: () => new(){"help", "list", "reset", "reveal"});
        }
    }

    private static void ListCommandOptions()
    {
        foreach(string info in new List<string>()
                {
                    "list : list all known waypoints",
                    "reset : clears player save file of known waypoints",
                    "reveal : reveals all waypoints - admin only",
                }) WaypointsPlugin.WaypointsLogger.LogInfo(info);
    }

    private static void RevealAllWaypoints(Terminal.ConsoleEventArgs args)
    {
        if (!Player.m_localPlayer) return;
        
        if (!Terminal.m_cheat)
        {
            Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Only admin can use this command");
            return;
        }
        
        RevealPlacedWaypoints();

        int count = 0;

        if (ZNet.instance.IsServer())
        {
            List<ZoneSystem.LocationInstance> waypoints = ZoneSystem.instance.GetLocationList().Where(location => location.m_location.m_prefab.Name.ToLower().Contains("waypoint")).ToList();

            foreach (Minimap.PinData pin in args.Context.m_findPins) Minimap.instance.RemovePin(pin);
            
            foreach (ZoneSystem.LocationInstance waypoint in waypoints)
            {
                if (waypoint.m_placed) continue;
                if (Waypoint.IsMatchFound(Waypoint.GetPlayerCustomData(Player.m_localPlayer), waypoint.m_position)) continue;
                Waypoint.m_tempPins.Add(Minimap.instance.AddPin(waypoint.m_position, Minimap.PinType.Icon4, Waypoint.FormatPosition(waypoint.m_position), false, false));
                ++count;
            }
            
        }
        else
        {
            if (m_locationWaypoints.Value.Count == 0) return;
            foreach (var position in m_locationWaypoints.Value)
            {
                if (!Waypoint.GetVector(position, out Vector3 pos)) continue;
                if (Waypoint.IsMatchFound(Waypoint.GetPlayerCustomData(Player.m_localPlayer), pos)) continue;
                Waypoint.m_tempPins.Add(Minimap.instance.AddPin(pos, Minimap.PinType.Icon4, Waypoint.FormatPosition(pos), false, false));
                ++count;
            }
        }
        WaypointsPlugin.WaypointsLogger.LogInfo($"Revealed {count} un-placed waypoints on map");
        m_teleportToUnplaced = true;
    }

    private static void RevealPlacedWaypoints()
    {
        int count = 0;
        List<Vector3> data = Waypoint.GetPlayerCustomData(Player.m_localPlayer);
        foreach (ZDO destination in FindDestinations())
        {
            if (Waypoint.IsMatchFound(data, destination.m_position)) continue;
            data.Add(destination.m_position);
            ++count;
        }

        if (count > 0)
        {
            ISerializer serializer = new SerializerBuilder().Build();
            Player.m_localPlayer.m_customData[Waypoint.m_playerCustomDataKey] = serializer.Serialize(data.Select(Waypoint.FormatPosition).ToList());
            WaypointsPlugin.WaypointsLogger.LogInfo($"Recorded {count} waypoints");
            return;
        }
        WaypointsPlugin.WaypointsLogger.LogInfo("No new waypoints added");
    }
    
    private static void ResetKnownWaypoints()
    {
        if (!Player.m_localPlayer) return;

        if (!Player.m_localPlayer.m_customData.ContainsKey(Waypoint.m_playerCustomDataKey))
        {
            WaypointsPlugin.WaypointsLogger.LogInfo("No records found");
            return;
        }

        Player.m_localPlayer.m_customData.Remove(Waypoint.m_playerCustomDataKey);
        
        WaypointsPlugin.WaypointsLogger.LogInfo("Cleared saved waypoints");
    }

    private static void ListKnownWaypoints()
    {
        if (!Player.m_localPlayer) return;
        foreach (Vector3 position in Waypoint.GetPlayerCustomData(Player.m_localPlayer))
        {
            ZDO? zdo = GetDestination(position);
            if (zdo == null)
            {
                WaypointsPlugin.WaypointsLogger.LogInfo(Waypoint.FormatPosition(position));
                continue;
            }
            WaypointsPlugin.WaypointsLogger.LogInfo($"{zdo.GetString(Waypoint.m_key)} : {Waypoint.FormatPosition(position)}");
        }
    }

    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
    private static class RegisterTeleportConfigs
    {
        private static void Postfix(ZNetScene __instance)
        {
            if (!__instance) return;
            foreach (var prefab in __instance.m_prefabs)
            {
                if (!prefab.TryGetComponent(out ItemDrop component)) continue;
                if (component.m_itemData.m_shared.m_teleportable) continue;
                ConfigEntry<string> config = WaypointsPlugin._Plugin.config("Keys", prefab.name, "", "Set defeat key to allow teleportation");
                WaypointsPlugin.keyConfigs[component.m_itemData.m_shared.m_name] = config;
            }
        }
    }

    [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.GenerateLocationsIfNeeded))]
    private static class RegisterGeneratedWaypoints
    {
        private static void Postfix() => UpdateServerLocationData();
    }
}