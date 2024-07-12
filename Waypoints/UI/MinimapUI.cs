using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Waypoints.Behaviors;
using Waypoints.Managers;

namespace Waypoints.UI;

public static class MinimapUI
{
    private static readonly List<Minimap.PinData> m_mapPins = new();
    private static readonly string m_savedBoolKey = "PlayerCustomDataWaypointBool";
    private static bool m_show = true;
    private static bool m_enabled;
    private static GameObject m_element = null!;

    [HarmonyPatch(typeof(Minimap), nameof(Minimap.Awake))]
    private static class CreateWaypointToggle
    {
        private static void Postfix(Minimap __instance)
        {
            GameObject original = Utils.FindChild(__instance.transform, "SharedPanel").gameObject;
            GameObject clone = Object.Instantiate(original, original.transform.parent, false);
            if (clone.transform is RectTransform rectTransform)
            {
                rectTransform.anchoredPosition = new Vector2(-375f, 41f);
            }
            Toggle toggle = clone.GetComponentInChildren<Toggle>();
            toggle.onValueChanged.RemoveAllListeners();
            toggle.onValueChanged = new Toggle.ToggleEvent();
            toggle.onValueChanged.AddListener(HandleToggle);
            
            Transform? text = Utils.FindChild(toggle.gameObject.transform, "Label");
            if (text.TryGetComponent(out TextMeshProUGUI component))
            {
                component.text = "$hud_sharedwaypoints";
            }

            var flag = GetSavedToggle();
            toggle.SetIsOnWithoutNotify(flag);
            m_show = GetSavedToggle();

            m_element = clone;
        }
    }

    [HarmonyPatch(typeof(Minimap), nameof(Minimap.Update))]
    private static class Minimap_Update_Patch
    {
        private static void Postfix()
        {
            UpdatePins(m_show);
        }
    }

    public static void SetElement(bool active) => m_element.SetActive(active);

    private static bool GetSavedToggle()
    {
        if (!Player.m_localPlayer) return false;
        if (!Player.m_localPlayer.m_customData.TryGetValue(m_savedBoolKey, out string data)) return false;
        return bool.TryParse(data, out bool boolean) && boolean;
    }

    private static void HandleToggle(bool show)
    {
        m_show = show;
        SaveToggle(show);
    }

    private static void SaveToggle(bool toggle)
    {
        if (!Player.m_localPlayer) return;
        Player.m_localPlayer.m_customData[m_savedBoolKey] = toggle.ToString();
    }

    private static void UpdatePins(bool show)
    {
        if (Waypoint.m_teleporting)
        {
            if (m_enabled) ClearMapPins();
        }
        else
        {
            if (m_enabled == show) return;
            if (show) AddPinsToMap();
            else ClearMapPins();
        }
    }
    
    private static void AddPinsToMap()
    {
        if (!Player.m_localPlayer || !Minimap.instance) return;
        foreach (Minimap.PinData pin in m_mapPins) Minimap.instance.RemovePin(pin);
        List<Vector3> data = Waypoint.GetPlayerCustomData(Player.m_localPlayer);
        HashSet<ZDO> destinations = WaypointManager.FindDestinations();
        foreach (ZDO? destination in destinations)
        {
            if (!Waypoint.IsMatchFound(data, destination.m_position)) continue;
            m_mapPins.Add(Minimap.instance.AddPin(destination.m_position, Minimap.PinType.Icon4, destination.GetString(Waypoint.m_key), false, false));
        }

        m_enabled = true;
    }
    
    private static void ClearMapPins()
    {
        foreach (Minimap.PinData pin in m_mapPins) Minimap.instance.RemovePin(pin);
        m_enabled = false;
    }
}