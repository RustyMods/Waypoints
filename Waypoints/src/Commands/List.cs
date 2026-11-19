using UnityEngine;

namespace Waypoints;

public static partial class Commands
{
    private static void ListKnownWaypoints(Terminal.ConsoleEventArgs args)
    {
        if (!Player.m_localPlayer)
        {
            args.Context.LogWarning("Local Player not assigned");
            return;
        }

        var data = Player.m_localPlayer.GetWaypointData();
        if (data.Count == 0)
        {
            args.Context.LogWarning("No waypoints recorded on local player");
        }
        else
        {
            for (int i = 0; i < data.Count; ++i)
            {
                var position = data[i];
                ZDO? zdo = WaypointManager.GetDestination(position);
                if (zdo == null)
                {
                    args.Context.LogWarning($"Null ZDO : x: {position.x}, y: {position.y}, z: {position.z}");
                    continue;
                }
                args.Context.LogInfo($"{zdo.GetString(Waypoint.m_key, "Empty Name")} : x: {position.x}, y: {position.y}, z: {position.z}");
            }
        }
    }
}