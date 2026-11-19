using System;
using System.Collections.Generic;

namespace Waypoints;

public class Command
{
    public readonly string m_description;
    private readonly bool m_isSecret;
    public readonly bool m_adminOnly;
    private readonly Action<Terminal.ConsoleEventArgs> m_command;
    private readonly Func<int, string, List<string>>? m_tabOptions;
    private readonly Func<string[], string, string>? m_descriptions;
    public bool Run(Terminal.ConsoleEventArgs args)
    {
        if (!IsAdmin())
        {
            return true;
        }
        m_command(args);
        return true;
    }
    private bool IsAdmin()
    {
        if (!ZNet.m_instance) return true;
        if (!m_adminOnly || Console.instance.IsCheatsEnabled() || ZNet.instance.LocalPlayerIsAdminOrHost()) return true;
        Console.instance.Print("<color=red>Admin Only</color>");
        return false;
    }
    public bool IsSecret() => m_isSecret;
    public List<string> FetchOptions(int i, string word = "") => m_tabOptions == null ? new List<string>() : m_tabOptions(i, word);
    public bool HasOptions() => m_tabOptions != null;
    public bool HasDescriptions() => m_descriptions != null;
    public string GetDescription(string[] args) =>
        m_descriptions == null ? 
            m_description : 
            m_descriptions(args, m_description);
        
    public Command(string input, 
        string description, 
        Action<Terminal.ConsoleEventArgs> command,
        Func<int, string, List<string>>? optionsFetcher = null, 
        bool isSecret = false, 
        bool adminOnly = false,
        Func<string[], string, string>? descriptions = null)
    {
        m_description = description;
        m_command = command;
        m_isSecret = isSecret;
        m_tabOptions = optionsFetcher;
        m_adminOnly = adminOnly;
        m_descriptions = descriptions;
        CommandManager.commands[input] = this;
    }
}