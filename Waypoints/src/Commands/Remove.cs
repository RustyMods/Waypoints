namespace Waypoints;

public static partial class Commands
{
    private static void RemoveLocations(Terminal.ConsoleEventArgs args)
    {
        var option = args.GetString(2);
        if (option == "all")
        {
            LocationManager.Remove(1, true);
        }
        else
        {
            var quantity = args.GetInt(2);
            LocationManager.Remove(quantity);
        }
    }
}