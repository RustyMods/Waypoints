
namespace Waypoints;

public static partial class Commands
{
    private static void ResetKnownWaypoints(Terminal.ConsoleEventArgs args)
    {
        if (!Player.m_localPlayer) return;

        args.Context.LogInfo(Player.m_localPlayer.m_customData.Remove(Waypoint.m_playerCustomDataKey)
            ? "Cleared saved waypoints"
            : "No records found");
    }
}