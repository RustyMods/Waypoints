using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using HarmonyLib;
using SoftReferenceableAssets;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Waypoints.Managers;

public class AssetLoaderManager
{
    private static BepInPlugin m_plugin = null!;
    private static readonly Dictionary<AssetID, AssetRef> m_assets = new();
    private static readonly Dictionary<string, AssetID> m_ids = new();

    static AssetLoaderManager()
    {
        Harmony harmony = new Harmony("org.bepinex.helpers.AssetLoaderManager");
        harmony.Patch(AccessTools.DeclaredMethod(typeof(AssetBundleLoader), nameof(AssetBundleLoader.OnInitCompleted)),
            postfix: new HarmonyMethod(AccessTools.DeclaredMethod(typeof(AssetLoaderManager),
                nameof(AssetBundleLoader_Postfix))));
    }

    private static void AssetBundleLoader_Postfix(AssetBundleLoader __instance)
    {
        foreach (KeyValuePair<AssetID, AssetRef> kvp in m_assets)
        {
            AddAssetToBundleLoader(__instance, kvp.Key, kvp.Value);
        }
    }

    public AssetLoaderManager(BepInPlugin plugin)
    {
        m_plugin = plugin;
    }

    public SoftReference<GameObject> GetSoftReference(AssetID assetID)
    {
        return assetID.IsValid ? new SoftReference<GameObject>(assetID) : default;
    }

    public SoftReference<GameObject>? GetSoftReference(string name)
    {
        AssetID? assetID = GetAssetID(name);
        if (assetID == null) return null;
        return assetID.Value.IsValid ? new SoftReference<GameObject>(assetID.Value) : default;
    }

    private static void AddAssetToBundleLoader(AssetBundleLoader __instance, AssetID assetID, AssetRef assetRef)
    {
        string bundleName = $"RustyMods_{assetRef.asset.name}";
        string assetPath = $"{assetRef.sourceMod.GUID}/Prefabs/{assetRef.asset.name}";

        if (__instance.m_bundleNameToLoaderIndex.ContainsKey(bundleName)) return;
        
        AssetLocation location = new AssetLocation(bundleName, assetPath);
        BundleLoader bundleLoader = new BundleLoader(bundleName, "");
        bundleLoader.HoldReference();
        __instance.m_bundleNameToLoaderIndex[bundleName] = __instance.m_bundleLoaders.Length;
        __instance.m_bundleLoaders = __instance.m_bundleLoaders.AddItem(bundleLoader).ToArray();

        int originalBundleLoaderIndex = __instance.m_assetLoaders
            .FirstOrDefault(l => l.m_assetID == assetID).m_bundleLoaderIndex;

        if (assetID.IsValid && originalBundleLoaderIndex > 0)
        {
            BundleLoader originalBundleLoader = __instance.m_bundleLoaders[originalBundleLoaderIndex];

            bundleLoader.m_bundleLoaderIndicesOfThisAndDependencies = originalBundleLoader.m_bundleLoaderIndicesOfThisAndDependencies
                .Where(i => i != originalBundleLoaderIndex)
                .AddItem(__instance.m_bundleNameToLoaderIndex[bundleName])
                .OrderBy(i => i)
                .ToArray();
        }
        else
        {
            bundleLoader.SetDependencies(Array.Empty<string>());
        }

        __instance.m_bundleLoaders[__instance.m_bundleNameToLoaderIndex[bundleName]] = bundleLoader;

        AssetLoader loader = new AssetLoader(assetID, location)
        {
            m_asset = assetRef.asset
        };
        loader.HoldReference();

        __instance.m_assetIDToLoaderIndex[assetID] = __instance.m_assetLoaders.Length;
        __instance.m_assetLoaders = __instance.m_assetLoaders.AddItem(loader).ToArray();

        m_ids[assetRef.asset.name] = assetID;
                
        WaypointsPlugin.WaypointsLogger.LogDebug($"Added prefab {assetRef.asset.name} with ID: {assetID} to AssetBundleLoader");
    }

    private AssetID? GetAssetID(string name) => m_ids.TryGetValue(name, out AssetID id) ? id : null;

    public void AddAsset(Object asset, out AssetID id)
    {
        AssetID assetID = GenerateID(asset);
        id = assetID;
        AssetRef assetRef = new(m_plugin, asset, assetID);
        m_assets[assetID] = assetRef;
    }

    private AssetID GenerateID(Object asset)
    {
        uint u = (uint)asset.name.GetStableHashCode();
        return new AssetID(u, u, u, u);
    }

    public bool isReady()
    {
        return Runtime.s_assetLoader != null && ((AssetBundleLoader)Runtime.s_assetLoader).Initialized;
    }
}

public struct AssetRef
{
    public BepInPlugin sourceMod;
    public Object asset;
    public AssetID originalID;

    public AssetRef(BepInPlugin sourceMod, Object asset, AssetID assetID)
    {
        this.sourceMod = sourceMod;
        this.asset = asset;
        this.originalID = assetID;
    }
}