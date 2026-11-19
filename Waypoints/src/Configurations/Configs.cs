using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using Waypoints.Managers;

namespace Waypoints;

public static class Configs
{
    private static ConfigEntry<Toggle> _generateLocations = null!;
    public static bool GenerateLocations => _generateLocations.Value is Toggle.On;
    private static ConfigEntry<Toggle> _usesCharges = null!;
    public static bool UseChargingSystem => _usesCharges.Value is Toggle.On;
    private static ConfigEntry<string> _chargeItem = null!;
    public static string ChargeItemID => _chargeItem.Value;
    private static ConfigEntry<int> _chargeMax = null!;
    public static int MaxCharge => _chargeMax.Value;
    private static ConfigEntry<int> _chargeDecay = null!;
    public static int ChargeDecay => _chargeDecay.Value;
    private static ConfigEntry<Toggle> _Decays = null!;
    public static bool Decays => _Decays.Value is Toggle.On;
    private static ConfigEntry<int> _cost = null!;
    public static int Cost => _cost.Value;
    private static ConfigEntry<Toggle> _TeleportAnything = null!;
    public static bool TeleportAnything => _TeleportAnything.Value is Toggle.On;
    private static ConfigEntry<Toggle> _UseKeys = null!;
    public static bool UseKeys => _UseKeys.Value is Toggle.On;
    private static ConfigEntry<Toggle> _TeleportTames = null!;
    public static bool TeleportTames => _TeleportTames.Value is Toggle.On;
    private static ConfigEntry<Toggle> _onlyAdminRenames = null!;
    public static bool OnlyAdminRenames => _onlyAdminRenames.Value is Toggle.On;
    private static ConfigEntry<Toggle> _teleportToBed = null!;
    public static bool CanTeleportToBeds => _teleportToBed.Value is Toggle.On;
    private static ConfigEntry<Toggle> _teleportToLocations = null!;
    public static bool CanTeleportToLocations => _teleportToLocations.Value is Toggle.On;
    private static ConfigEntry<float> _distancePerCharge = null!;
    public static float DistancePerCharge => _distancePerCharge.Value;
    private static ConfigEntry<Toggle> _useDistanceCharge = null!;
    public static bool UseDistanceChargeCalculation => _useDistanceCharge.Value is Toggle.On;
    private static ConfigEntry<int> _locationAmount = null!;
    public static int LocationQuantity => _locationAmount.Value;
    private static ConfigEntry<Toggle> _showConnectionTrails = null!;
    public static bool ShowConnectionTrails => _showConnectionTrails.Value is Toggle.On;
    private static ConfigEntry<float> _connectionMaxRange = null!;
    public static float ConnectionMaxRange => _connectionMaxRange.Value;
    
    public static readonly Dictionary<string, ConfigEntry<string>> keyConfigs = new();
    private static ConfigEntry<Toggle> _hideMapData = null!;
    public static bool HideMapData => _hideMapData.Value is Toggle.On;
    private static ConfigEntry<Toggle> _requireKnownLocation = null!;
    public static bool RequireKnownLocation => _requireKnownLocation.Value is Toggle.On;
    private static ConfigEntry<float> _minDistanceFromSimilar = null!;
    public static float MinDistanceFromSimilar => _minDistanceFromSimilar.Value;
    private static ConfigEntry<Toggle> _alwaysShowConnectionEffects = null!;
    public static bool AlwaysShowConnectionEffects => _alwaysShowConnectionEffects.Value is Toggle.On;
    private static ConfigEntry<string> _teleportBlacklist = null!;
    public static List<string> TeleportBlacklist => new StringList(_teleportBlacklist.Value).list;
    private static ConfigEntry<Toggle> _removeVanillaPortals = null!;
    public static bool RemoveVanillaPortals => _removeVanillaPortals.Value is Toggle.On;
    
    public static void Load()
    {

        _generateLocations = ConfigManager.config("2 - Settings", "0 - Locations", Toggle.On, "If on, waypoints will generate along side game locations");
        _locationAmount = ConfigManager.config("2 - Settings", "1 - Amount", 500, "Set amount of waypoint locations to attempt to generate");
        _onlyAdminRenames = ConfigManager.config("2 - Settings", "2 - Only Admin Renames", Toggle.Off, "If on, only admins can rename waypoints");
        _TeleportAnything = ConfigManager.config("2 - Settings", "3 - Teleport Anything", Toggle.Off, "If on, player can teleport non-teleportable items");
        _UseKeys = ConfigManager.config("2 - Settings", "4 - Use Keys", Toggle.Off, "If on, portal checks if game has global key to allow teleportation of non-teleportable items");
        _TeleportTames = ConfigManager.config("2 - Settings", "5 - Teleport Tames", Toggle.Off, "If on, portal can teleport tames that are following player");
        _teleportToBed = ConfigManager.config("2 - Settings", "6 - Teleport To Bed", Toggle.On, "If on, players can teleport to their beds");
        _teleportToLocations = ConfigManager.config("2 - Settings", "7 - Teleport To Locations", Toggle.On, "If on, players can teleport to locations");
        
        _usesCharges = ConfigManager.config("3 - Charge System", "0 - Enabled", Toggle.Off, "If on, waypoints use charge system");
        _chargeItem = ConfigManager.config("3 - Charge System", "1 - Charge Item", "GreydwarfEye", "Set charge item");
        _chargeMax = ConfigManager.config("3 - Charge System", "2 - Charge Max", 50, "Set max charge");
        _Decays = ConfigManager.config("3 - Charge System", "3 - Charge Decays", Toggle.On, "If on, portal charge decays over time");
        _chargeDecay = ConfigManager.config("3 - Charge System", "4 - Minute between decay", 5, "Set loss of charge time in minutes");
        _cost = ConfigManager.config("3 - Charge System", "5 - Cost", 1, "Set base charge cost to teleport");
        _useDistanceCharge = ConfigManager.config("3 - Charge System", "6 - Dynamic Cost", Toggle.Off, "If on, waypoints calculate distance of destination to evaluate cost");
        _distancePerCharge = ConfigManager.config("3 - Charge System", "7 - Distance Units Per Charge", 100f, "Units of distance per charge, higher number reduces cost");
        _showConnectionTrails = ConfigManager.config("4 - Trails", "_Enable", Toggle.On, "If on, trails appear when players get near unknown waypoint");
        _connectionMaxRange = ConfigManager.config("4 - Trails", "Max Range", 100f, "Set max range for trails to start appearing");
        _hideMapData = ConfigManager.config("2 - Settings", "8 - Hide Map Data", Toggle.Off,
            "If on, while playing no-map, map data gets hidden when map open");
        _requireKnownLocation = ConfigManager.config("2 - Settings", "Require Known Location", Toggle.Off,
            "If on, location pins must be in explored area to use");
        _minDistanceFromSimilar = ConfigManager.config("2 - Settings", "Min Distance", 1000f,
            "Set minimum distance waypoints can spawn near each other");
        _alwaysShowConnectionEffects = ConfigManager.config("4 - Trails", "Always Show", Toggle.Off,
            "If on, trails will not disappear if waypoint is known");

        _removeVanillaPortals = ConfigManager.config("2 - Settings", "Disable Portals", Toggle.Off,
            "If on, will remove vanilla portals from build menu");

        _teleportBlacklist = ConfigManager.config("2 - Settings", "Blacklist", "",
            new ConfigDescription("Add item ID to list that will prevent teleporting, ID separated by comma", null,
                StringList.attributes));
    }
    
    private static bool _loadedKeyConfigs;

    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
    private static class RegisterTeleportConfigs
    {
        private static void Postfix(ZNetScene __instance)
        {
            if (!__instance || _loadedKeyConfigs) return;
            foreach (GameObject prefab in __instance.m_prefabs)
            {
                if (!prefab.TryGetComponent(out ItemDrop component)) continue;
                if (component.m_itemData.m_shared.m_teleportable) continue;
                ConfigEntry<string> config = ConfigManager.config("Keys", prefab.name, "", "Set defeat key to allow teleportation");
                Configs.keyConfigs[component.m_itemData.m_shared.m_name] = config;
            }
    
            _loadedKeyConfigs = true;
        }
    }
    
    [HarmonyPatch(typeof(PieceTable), nameof(PieceTable.GetPiecesInSelectedCategory))]
    private static class PieceTable_GetPiecesInSelectedCategory_Patch
    {
        private static void Postfix(ref List<Piece> __result)
        {
            if (!RemoveVanillaPortals) return;

            __result.RemoveAll(p => p.GetComponent<TeleportWorld>());
        }
    }
}