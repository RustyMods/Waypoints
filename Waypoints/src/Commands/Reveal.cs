using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YamlDotNet.Serialization;

namespace Waypoints;

public static partial class Commands
{
    private static void RevealAllWaypoints(Terminal.ConsoleEventArgs args)
    {
        if (!Player.m_localPlayer || !Minimap.instance || !ZNet.instance || !ZoneSystem.instance) return;
        
        if (!Terminal.m_cheat)
        {
            Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Only admin can use this command");
            return;
        }
        
        RevealPlacedWaypoints();

        int count = 0;

        if (ZNet.instance.IsServer())
        {
            List<ZoneSystem.LocationInstance> waypoints = ZoneSystem.instance
                .GetLocationList()
                .Where(location => location.m_location.m_prefab.Name.ToLower().Contains("waypoint"))
                .ToList();

            foreach (Minimap.PinData pin in args.Context.m_findPins) Minimap.instance.RemovePin(pin);
            
            foreach (ZoneSystem.LocationInstance waypoint in waypoints)
            {
                if (waypoint.m_placed) continue;
                if (Waypoint.IsMatchFound(Player.m_localPlayer.GetWaypointData(), waypoint.m_position)) continue;
                Waypoint.m_tempPins.Add(
                    Minimap.instance.AddPin(
                        waypoint.m_position, 
                        Minimap.PinType.Icon4, 
                        $"{(int)waypoint.m_position.x} {(int)waypoint.m_position.y} {(int)waypoint.m_position.z}", 
                        false, 
                        false));
                ++count;
            }
        }
        else
        {
            if (string.IsNullOrEmpty(WaypointManager.m_locationWaypoints.Value)) return;
            string[] list = WaypointManager.m_locationWaypoints.Value.Split(';');
            foreach (string position in list)
            {
                if (!position.ParseStringVector(out Vector3 pos)) continue;
                if (Waypoint.IsMatchFound(Player.m_localPlayer.GetWaypointData(), pos)) continue;
                Waypoint.m_tempPins.Add(
                    Minimap.instance.AddPin(
                        pos, 
                        Minimap.PinType.Icon4, 
                        $"{(int)pos.x} {(int)pos.y} {(int)pos.z}", 
                        false, 
                        false));
                ++count;
            }
        }
        Console.instance.LogInfo($"Revealed {count} un-placed waypoints on map");
        WaypointManager.m_teleportToUnplaced = true;
    }
    
    private static void RevealPlacedWaypoints()
    {
        if (!Player.m_localPlayer) return;
        int count = 0;
        List<Vector3> data = Player.m_localPlayer.GetWaypointData();
        foreach (ZDO destination in WaypointManager.FindDestinations())
        {
            if (Waypoint.IsMatchFound(data, destination.m_position)) continue;
            data.Add(destination.m_position);
            ++count;
        }

        if (count > 0)
        {
            ISerializer serializer = new SerializerBuilder().Build();
            Player.m_localPlayer.m_customData[Waypoint.m_playerCustomDataKey] = serializer.Serialize(data.Select(WaypointExt.FormatPosition).ToList());
            Console.instance.LogInfo($"Recorded {count} waypoints");
            return;
        }
        Console.instance.LogWarning("No new waypoints added");
    }
}