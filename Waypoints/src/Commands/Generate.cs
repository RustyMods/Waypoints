namespace Waypoints;

public static partial class Commands
{
    private static void GenerateLocations(Terminal.ConsoleEventArgs args)
    {
        var quantity = args.GetInt(2, 10);
        LocationManager.Generate(quantity, args.Context);
    }
}