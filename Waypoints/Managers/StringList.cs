using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;

namespace Waypoints.Managers;

public class StringList
{
    public readonly List<string> list;

    public StringList(List<string> prefabs)
    {
        list = prefabs;
        if (list.Count == 0)
        {
            list.Add("");
        }
    }

    public StringList(params string[] prefabs)
    {
        list = prefabs.ToList();
        if (list.Count == 0)
        {
            list.Add("");
        }
    }

    public StringList(string config)
    {
        list = config.Split(',').ToList();
        if (list.Count == 0)
        {
            list.Add("");
        }
    }
    
    public override string ToString() => string.Join(",", list);

    public static void Draw(ConfigEntryBase cfg)
    {
        bool locked = cfg.Description.Tags
            .Select(a =>
                a.GetType().Name == "ConfigurationManagerAttributes"
                    ? (bool?)a.GetType().GetField("ReadOnly")?.GetValue(a)
                    : null).FirstOrDefault(v => v != null) ?? false;
        bool wasUpdated = false;
        List<string> prefabs = new();
        GUILayout.BeginVertical();
        foreach (string? prefab in new StringList((string)cfg.BoxedValue).list)
        {
            GUILayout.BeginHorizontal();
            var prefabName = prefab;
            var nameField = GUILayout.TextField(prefab);
            if (nameField != prefab && !locked)
            {
                wasUpdated = true;
                prefabName = nameField;
            }

            if (GUILayout.Button("x", new GUIStyle(GUI.skin.button) { fixedWidth = 21 }) && !locked)
            {
                wasUpdated = true;
            }
            else
            {
                prefabs.Add(prefabName);
            }

            if (GUILayout.Button("+", new GUIStyle(GUI.skin.button) { fixedWidth = 21 }) && !locked)
            {
                prefabs.Add("");
                wasUpdated = true;
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndVertical();
        if (wasUpdated)
        {
            cfg.BoxedValue = new StringList(prefabs).ToString();
        }
    }
    
    public static readonly WaypointsPlugin.ConfigurationManagerAttributes attributes = new ()
    {
        CustomDrawer = Draw
    };
}