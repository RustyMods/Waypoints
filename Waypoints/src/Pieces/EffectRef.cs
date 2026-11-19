using System;
using System.Collections.Generic;

namespace Waypoints;

public class EffectListRef
{
    private static readonly List<EffectListRef> m_effectListRefs = [];

    public static void LoadAll()
    {
        for (int i = 0; i < m_effectListRefs.Count; ++i)
        {
            var effectRef = m_effectListRefs[i];
            effectRef.Load();
        }
    }
    
    public readonly EffectList m_effectList = new  EffectList();
    public readonly EffectRef[] m_effectRefs;

    public EffectListRef(params string[] effectNames)
    {
        List<EffectRef> refs = [];
        for (int i = 0; i < effectNames.Length; ++i)
        {
            var effectName = effectNames[i];
            var effectRef = new EffectRef(effectName);
            refs.Add(effectRef);
        }

        m_effectRefs = refs.ToArray();
        m_effectListRefs.Add(this);
    }

    public EffectListRef(params EffectRef[] effectRefs)
    {
        m_effectRefs = effectRefs;
        m_effectListRefs.Add(this);
    }

    private void Load()
    {
        List<EffectList.EffectData> list = [];
        for (int i = 0; i < m_effectRefs.Length; ++i)
        {
            EffectRef effectRef = m_effectRefs[i];
            effectRef.Load();
            if (!effectRef.m_isValid) continue;
            list.Add(effectRef.m_data);
        }

        m_effectList.m_effectPrefabs = list.ToArray();
    }
}

public class EffectRef(
    string prefabName,
    bool attach = false,
    bool follow = false,
    bool inheritParentRot = false,
    bool inheritParentScale = false,
    bool multiplyParentVisualScale = false,
    bool randomRotation = false,
    bool scale = false,
    string childTransform = "",
    int variant = -1,
    bool enabled = true,
    Action<EffectRef>? onLoad = null)
{
    public readonly EffectList.EffectData m_data = new()
    {
        m_enabled =  enabled,
        m_variant = variant,
        m_attach = attach,
        m_follow = follow,
        m_inheritParentRotation = inheritParentRot,
        m_inheritParentScale = inheritParentScale,
        m_multiplyParentVisualScale = multiplyParentVisualScale,
        m_randomRotation = randomRotation,
        m_scale = scale,
        m_childTransform = childTransform
    };
    public bool m_isValid = false;
    public readonly string m_prefabName = prefabName;
    public readonly Action<EffectRef>? m_onLoad = onLoad;
    public bool m_loaded;

    public void Load()
    {
        if (m_loaded) return;
        var prefab = ZNetScene.instance.GetPrefab(m_prefabName);
        if (prefab == null) return;
        m_data.m_prefab = prefab;
        if (m_onLoad != null) m_onLoad.Invoke(this);
        m_isValid = m_data.m_prefab != null;
        m_loaded = true;
    }
}