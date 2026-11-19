using HarmonyLib;
using PieceManager;

namespace Waypoints;

public static class Pieces
{
    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
    private static class ZNetScene_Awake_Patch
    {
        private static void Postfix() => EffectListRef.LoadAll();
    }
    
    public static void Load()
    {
        var placeEffects = new EffectListRef("vfx_Place_workbench", "sfx_build_hammer_stone").m_effectList;
        var destroyEffects = new EffectListRef("vfx_RockDestroyed", "sfx_rock_destroyed").m_effectList;
        var hitEffects = new EffectListRef("vfx_RockHit").m_effectList;
        var switchEfects = new EffectListRef("vfx_Place_throne02").m_effectList;
        BuildPiece Waypoint = new BuildPiece(WaypointsPlugin._assetBundle, "WaypointShrine");
        Waypoint.Name.English("Waypoint Shrine");
        Waypoint.Description.English("Use charge item on waypoint to charge it, requires to touch to learn location");
        Waypoint.Category.Set(BuildPieceCategory.Misc);
        Waypoint.RequiredItems.Add("SwordCheat", 1, false);
        Waypoint.Prefab.AddComponent<Waypoint>();
        Waypoint.Prefab.AddComponent<GlowTrails>();
        WaypointManager.AddPrefabToSearch(Waypoint.Prefab.name);
        MaterialReplacer.RegisterGameObjectForShaderSwap(Waypoint.Prefab, MaterialReplacer.ShaderType.PieceShader);
        Waypoint.Prefab.GetComponent<Piece>().m_placeEffect = placeEffects;
        var wnt = Waypoint.Prefab.GetComponent<WearNTear>();
        wnt.m_destroyedEffect = destroyEffects;
        wnt.m_hitEffect = hitEffects;
        wnt.m_switchEffect = switchEfects;
        Waypoint.SpecialProperties.AdminOnly = true;
        
        BuildPiece WaypointPortal = new BuildPiece(WaypointsPlugin._assetBundle, "WaypointShrinePortal");
        WaypointPortal.Name.English("Waypoint Portal");
        WaypointPortal.Description.English("Bypass need to know it to use");
        WaypointPortal.Category.Set(BuildPieceCategory.Misc);
        WaypointPortal.RequiredItems.Add("SwordCheat", 1, false);
        Waypoint component = WaypointPortal.Prefab.AddComponent<Waypoint>();
        WaypointPortal.Prefab.AddComponent<GlowTrails>();
        component.m_poi = true;
        WaypointManager.AddPrefabToSearch(WaypointPortal.Prefab.name);
        MaterialReplacer.RegisterGameObjectForShaderSwap(WaypointPortal.Prefab, MaterialReplacer.ShaderType.PieceShader);
        WaypointPortal.Prefab.GetComponent<Piece>().m_placeEffect = placeEffects;
        var pWnt = WaypointPortal.Prefab.GetComponent<WearNTear>();
        pWnt.m_destroyedEffect = destroyEffects;
        pWnt.m_hitEffect = hitEffects;
        pWnt.m_switchEffect = switchEfects;
    }
}