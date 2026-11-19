using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;

namespace Waypoints;

public static class CommandManager
{
    private static readonly string startCommand;
    public static readonly Dictionary<string, Command> commands;

    static CommandManager()
    {
        commands = new Dictionary<string, Command>();
        Harmony harmony = WaypointsPlugin.instance._harmony;
        startCommand = "wp";

        harmony.Patch(AccessTools.Method(typeof(Terminal), nameof(Terminal.Awake)),
            postfix: new HarmonyMethod(AccessTools.Method(typeof(CommandManager), 
                nameof(Patch_Terminal_Awake))));
        harmony.Patch(AccessTools.Method(typeof(Terminal), nameof(Terminal.updateSearch)),
            prefix: new HarmonyMethod(AccessTools.Method(typeof(CommandManager),
                nameof(Patch_Terminal_UpdateSearch))));
        harmony.Patch(AccessTools.Method(typeof(Terminal), nameof(Terminal.tabCycle)),
            prefix: new HarmonyMethod(AccessTools.Method(typeof(CommandManager),
                nameof(Patch_Terminal_TabCycle))));
        harmony.Patch(AccessTools.Method(typeof(Terminal), nameof(Terminal.UpdateInput)),
            postfix: new HarmonyMethod(AccessTools.Method(typeof(CommandManager), nameof(Patch_Terminal_UpdateInput))));
    }

    private static void Patch_Terminal_Awake()
    {
        _ = new Terminal.ConsoleCommand(startCommand, "use help to find available commands", args =>
        {
            if (args.Length < 2)
            {
                return true;
            }
            if (!commands.TryGetValue(args[1], out Command data))
            {
                return true;
            }
            return data.Run(args);
        },  optionsFetcher: commands
            .Where(x => !x.Value.IsSecret())
            .Select(x => x.Key)
            .ToList);

        _ = new Command("help", "list of available commands", args =>
        {
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<string, Command> command in commands)
            {
                if (command.Value.IsSecret()) continue;
                if (command.Key == "help") continue;
                
                sb.Clear();
                sb.AppendFormat("<color=#FBBF24>{0}</color> - {1}", command.Key, command.Value.m_description);
                if (command.Value.m_adminOnly)
                {
                    sb.Append(" <color=#F87171>(admin only)</color>");
                }
                args.Context.AddString(sb.ToString());
            }
        });
        
    }

    private static void Patch_Terminal_UpdateInput(Terminal __instance)
    {
        if (__instance.m_lastSearch.Count <= 0) return;
        if (__instance.m_lastSearch.Count > 1) return;

        var strArray = __instance.m_input.text.Split(' ');
        if (strArray.GetString(0) != startCommand) return;
        var word = strArray.GetString(1);
        if (!commands.TryGetValue(word, out var command)) return;

        var search = __instance.m_lastSearch.First();

        if (search == strArray.GetString(2))
        {
            __instance.m_search.text = command.GetDescription(strArray);
        }
    }

    private static bool Patch_Terminal_UpdateSearch(Terminal __instance, string word, ref List<string> options)
    {
        if (__instance.m_search == null) return true;
        string[] strArray = __instance.m_input.text.Split(' ');
        if (strArray.GetString(0) != startCommand) return true;
        if (!commands.TryGetValue(word, out var command)) return true;
        
        if (command.HasOptions() && strArray.Length > 2)
        {
            var option = strArray.GetString(2);
            options = command.FetchOptions(strArray.Length - 1, option);
        }
        else
        {
            __instance.m_lastSearch.Clear();
            __instance.m_search.text = command.GetDescription(strArray);
            return false;
        }

        return true;
    }

    private static void Patch_Terminal_TabCycle(Terminal __instance, string word, bool usePrefix, ref List<string>? options)
    {
        if (string.IsNullOrEmpty(__instance.m_input.text))
        {
            return;
        }
        string[] strArray = __instance.m_input.text.Split(' ');
        if (strArray.Length < 2)
        {
            return;
        }
        string inputCommand = strArray[0];
        if (!string.Equals(startCommand, inputCommand, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        
        string w = strArray.Length > 2 ? strArray[2] : "";
        
        if (commands.TryGetValue(word, out Command command))
        {
            options = command.FetchOptions(strArray.Length - 1, w);
        }
    }
}


