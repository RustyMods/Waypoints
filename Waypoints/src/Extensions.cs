using System.Globalization;
using System.Linq;

namespace Waypoints;

public static partial class Extensions
{
    public static string GetString(this Terminal.ConsoleEventArgs args, int index, string defaultValue = "")
    {
        if (args.Length < index + 1) return defaultValue;
        return args[index];
    }

    public static float GetFloat(this Terminal.ConsoleEventArgs args, int index, float defaultValue = 0f)
    {
        if (args.Length < index + 1) return defaultValue;
        string? arg = args[index];
        return float.TryParse(arg, out float result) ? result : defaultValue;
    }

    public static int GetInt(this Terminal.ConsoleEventArgs args, int index, int defaultValue = 0)
    {
        if (args.Length < index + 1) return defaultValue;
        string? arg = args[index];
        return int.TryParse(arg, out int result) ? result : defaultValue;
    }

    public static string GetStringFrom(this Terminal.ConsoleEventArgs args, int index, string defaultValue = "")
    {
        if (args.Length < index + 1) return defaultValue;
        return string.Join(" ", args.Args.Skip(index));
    }
    
    public static int GetInt(this string[] parts, int index, int defaultValue = 0)
    {
        if (parts.Length - 1 < index) return defaultValue;
        return int.TryParse(parts[index].Trim(), out int x) ? x : 0;
    }
    
    public static float GetFloat(this string[] parts, int index, float defaultValue = 0f)
    {
        if (parts.Length - 1 < index) return defaultValue;
        return float.TryParse(parts[index].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float result) ? result : defaultValue;
    }

    public static bool GetBool(this string[] parts, int index, bool defaultValue = false)
    {
        if (parts.Length - 1 < index) return defaultValue;
        return bool.TryParse(parts[index].Trim(), out bool result) ? result : defaultValue;
    }
    
    public static string GetString(this string[] parts, int index, string defaultValue = "")
    {
        if (parts.Length - 1 < index) return defaultValue;
        return parts[index].Trim();
    }
}
