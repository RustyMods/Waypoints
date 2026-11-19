namespace Waypoints;

public static partial class Commands
{
    private const string HEX_Gray = "#B2BEB5";
    private const string HEX_SoftBlue = "#60A5FA";
    private const string HEX_Green = "#34D399";
    private const string HEX_Amber = "#FBBF24";
    private const string HEX_Red = "#F87171";

    public static void LogInfo(this Terminal terminal, string msg) => terminal.Log(HEX_Green, msg);
    public static void LogWarning(this Terminal terminal, string msg) => terminal.Log(HEX_Amber, msg);
    public static void LogError(this Terminal terminal, string msg) => terminal.Log(HEX_Red, msg);
    public static void LogDebug(this Terminal terminal, string msg) => terminal.Log(HEX_SoftBlue, msg);
    public static void Log(this Terminal terminal, string hex, string msg) => terminal.AddString($"<color={hex}>{msg}</color>");


    public static void Init()
    {
        _ = new Command("list", "list local player known waypoints", ListKnownWaypoints);
        _ = new Command("reset", "clears local player save file of known waypoints", ResetKnownWaypoints);
        _ = new Command("reveal", "reveals all possible waypoint locations", RevealAllWaypoints, adminOnly: true);
        _ = new Command("generate", "[quantity] attempts to generate new waypoints", GenerateLocations, adminOnly: true);
        _ = new Command("remove", "[quantity / all] attempts to remove waypoint locations", RemoveLocations, adminOnly: true);
    }
}