using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using Waypoints.Managers;
using Waypoints.UI;
using YamlDotNet.Serialization;

namespace Waypoints.Behaviors;

public class Waypoint : MonoBehaviour, Interactable, Hoverable, TextReceiver
{
    public static readonly int m_key = "WaypointShrine".GetStableHashCode();
    public static readonly string m_playerCustomDataKey = "WaypointShrineKeys";
    private static readonly int m_chargeKey = "WaypointCharge".GetStableHashCode();
    private static readonly int m_timerKey = "WaypointTimer".GetStableHashCode();
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private static readonly Vector3 m_exitDistance = new Vector3(1f, 1f, 1f);
    public static readonly List<Minimap.PinData> m_tempPins = new();
    private static Waypoint? m_currentWaypoint;
    private static bool m_noMapMode;
    public static bool m_teleporting;
    
    private const float m_updateTime = 30f;
    private const float m_pinRadius = 35f;
    
    private ZNetView m_nview = null!;
    private ParticleSystem[] m_particles = null!;
    private MeshRenderer m_model = null!;
    private Light m_light = null!;
    private bool m_particlesActive = true;
    private float m_intensity;

    public void Awake()
    {
        m_nview = GetComponent<ZNetView>();
        m_particles = GetComponentsInChildren<ParticleSystem>();
        m_model = GetComponentInChildren<MeshRenderer>();
        m_light = GetComponentInChildren<Light>();
        SetEffects(false);
        if (!m_nview) return;
        m_nview.Register<string>(nameof(RPC_SetName), RPC_SetName);
        m_nview.Register<int>(nameof(RPC_AddCharge), RPC_AddCharge);
        m_nview.Register<int>(nameof(RPC_RemoveCharge), RPC_RemoveCharge);
        if (!m_nview.IsValid()) return;
        if (GetText().IsNullOrWhiteSpace()) SetText($"{WorldGenerator.instance.GetBiome(transform.position)} Waypoint");
        long lastDecay = m_nview.GetZDO().GetLong(m_timerKey);
        if (lastDecay == 0L)
        {
            m_nview.GetZDO().Set(m_timerKey, (long)ZNet.instance.GetTimeSeconds());
        }

        if (WaypointsPlugin._usesCharges.Value is WaypointsPlugin.Toggle.Off) return;
        if (GetCurrentCharge() > 0 && WaypointsPlugin._Decays.Value is WaypointsPlugin.Toggle.On)
        {
            InvokeRepeating(nameof(UpdateCharge), m_updateTime, m_updateTime);
        }
    }

    public void Update()
    {
        if (!Player.m_localPlayer || !m_nview.IsValid()) return;
        Player closestPlayer = Player.GetClosestPlayer(transform.position, 5f);
        if (closestPlayer == null)
        {
            SetEffects(false);
        }
        else
        {
            SetEffects(IsMatchFound(GetPlayerCustomData(closestPlayer), GetPosition()) && CanTeleport(closestPlayer, false));
        }
    }

    private void SetEffects(bool active)
    {
        SetParticles(active);
        m_intensity = Mathf.MoveTowards(m_intensity, m_particlesActive ? 1f : 0.0f, Time.deltaTime);
        m_model.material.SetColor(EmissionColor, Color.Lerp(Color.black, Color.red, m_intensity));
        m_light.intensity = m_intensity;
    }

    public void UpdateCharge()
    {
        if (!ZNet.instance) return;
        if (!m_nview.IsValid()) return;
        int floor = GetDecayAmount();
        if (floor == 0) return;
        if (!RemoveCharge(floor) || WaypointsPlugin._Decays.Value is WaypointsPlugin.Toggle.Off)
        {
            m_nview.CancelInvoke(nameof(UpdateCharge));
        }
    }

    private int GetDecaySeconds() => WaypointsPlugin._chargeDecay.Value * 60;

    private int GetDecayAmount()
    {
        if (!ZNet.instance) return 0;
        if (!m_nview.IsValid()) return 0;
        long lastDecay = m_nview.GetZDO().GetLong(m_timerKey);
        long difference = (long)ZNet.instance.GetTimeSeconds() - lastDecay;
        long amount = difference / GetDecaySeconds();
        return Mathf.FloorToInt(amount);
    }

    private bool AddCharge(int amount)
    {
        if (!m_nview.IsValid()) return false;
        int currentCharge = GetCurrentCharge();
        if (currentCharge >= WaypointsPlugin._chargeMax.Value) return false;
        m_nview.ClaimOwnership();
        m_nview.InvokeRPC(nameof(RPC_AddCharge), amount);
        return true;
    }

    private int GetCurrentCharge() => !m_nview.IsValid() ? 0 : m_nview.GetZDO().GetInt(m_chargeKey);

    private void RPC_AddCharge(long sender, int amount)
    {
        if (!ZNet.instance) return;
        if (!m_nview.IsValid()) return;
        int currentCharge = GetCurrentCharge();
        if (currentCharge >= WaypointsPlugin._chargeMax.Value) return;
        m_nview.GetZDO().Set(m_chargeKey, Mathf.Clamp(currentCharge + amount, 0, WaypointsPlugin._chargeMax.Value));
        ResetTimer();
        CancelInvoke(nameof(UpdateCharge));
        if (WaypointsPlugin._Decays.Value is WaypointsPlugin.Toggle.Off) return;
        InvokeRepeating(nameof(UpdateCharge), m_updateTime, m_updateTime);
    }

    private bool RemoveCharge(int amount)
    {
        if (amount == 0) return false;
        if (!m_nview.IsValid()) return false;
        int currentCharge = GetCurrentCharge();
        if (currentCharge == 0) return false;
        m_nview.InvokeRPC(nameof(RPC_RemoveCharge), currentCharge - amount < 0 ? currentCharge : amount);
        return true;
    }

    private void RPC_RemoveCharge(long sender, int amount)
    {
        if (!ZNet.instance) return;
        if (!m_nview.IsValid() || m_nview.GetZDO() == null) return;
        int currentCharge = GetCurrentCharge();
        if (currentCharge - amount < 0)
        {
            m_nview.GetZDO().Set(m_chargeKey, 0);
        }
        else
        {
            m_nview.GetZDO().Set(m_chargeKey, currentCharge - amount);
        }
        ResetTimer();
    }

    private void ResetTimer()
    {
        if (m_nview == null || !ZNet.instance) return;
        if (!m_nview.IsValid() || m_nview.GetZDO() == null) return;
        m_nview.GetZDO().Set(m_timerKey, (long)ZNet.instance.GetTimeSeconds());
    }

    private bool CanTeleport(Player player, bool message = true)
    {
        if (GetCurrentCharge() - WaypointsPlugin._cost.Value < 0 && WaypointsPlugin._usesCharges.Value is WaypointsPlugin.Toggle.On)
        {
            if (message) player.Message(MessageHud.MessageType.Center, "$msg_chargerequired");
            return false;
        }
        if (WaypointsPlugin._TeleportAnything.Value is WaypointsPlugin.Toggle.On) return true;
        if (WaypointsPlugin._UseKeys.Value is WaypointsPlugin.Toggle.Off)
        {
            if (player.IsTeleportable()) return true;
            if (message) player.Message(MessageHud.MessageType.Center, "$msg_noteleport");
            return false;
            
        }
        foreach (ItemDrop.ItemData? itemData in player.GetInventory().m_inventory)
        {
            if (itemData.m_shared.m_teleportable) continue;
            if (!WaypointsPlugin.keyConfigs.TryGetValue(itemData.m_shared.m_name, out ConfigEntry<string> config)) continue;
            if (config.Value.IsNullOrWhiteSpace()) return false;
            if (ZoneSystem.instance.GetGlobalKey(config.Value)) continue;
            if (player.HaveUniqueKey(config.Value)) continue;
            if (message) player.Message(MessageHud.MessageType.Center, "$msg_noteleport");
            return false;
        }

        return true;
    }

    private ItemDrop? GetChargeItem()
    {
        if (!ObjectDB.instance) return null;
        GameObject prefab = ObjectDB.instance.GetItemPrefab(WaypointsPlugin._chargeItem.Value);
        if (!prefab)
        {
            prefab = ObjectDB.instance.GetItemPrefab("GreydwarfEye");
            if (!prefab) return null;
        }

        return prefab.TryGetComponent(out ItemDrop component) ? component : null;
    }

    private Vector3 GetPosition() => m_nview.GetZDO().m_position;
    private void SetParticles(bool active)
    {
        if (m_particlesActive == active) return;
        m_particlesActive = active;
        foreach (ParticleSystem particle in m_particles)
        {
            ParticleSystem.EmissionModule module = particle.emission;
            module.enabled = active;
        }
    }

    private void SetMapMode()
    {
        m_noMapMode = Game.m_noMap;
        Game.m_noMap = false;
    }

    private static void ResetMapMode() => Game.m_noMap = m_noMapMode;

    private static Minimap.PinData? GetNearestPin(Vector3 position, float radius)
    {
        Minimap.PinData? result = null;
        float num1 = 99999f;
        foreach (Minimap.PinData? pin in m_tempPins)
        {
            float num2 = Utils.DistanceXZ(position, pin.m_pos);
            if (num2 > radius || num2 > num1) continue;
            result = pin;
            num1 = num2;
        }

        return result;
    }

    private static void HandleMapClick()
    {
        Minimap.PinData? destination = GetNearestPin(Minimap.instance.ScreenToWorldPoint(Input.mousePosition), m_pinRadius);
        if (destination == null) return;
        if (destination.m_type is Minimap.PinType.Bed)
        {
            if (!Teleport(destination.m_pos)) return;
        }
        else
        {
            ZDO? waypoint = WaypointManager.GetDestination(destination.m_pos);
            if (waypoint == null)
            {
                if (!WaypointManager.m_teleportToUnplaced) return;
                Teleport(destination.m_pos, false);
                WaypointManager.m_teleportToUnplaced = false;
            }
            else
            {
                if (!Teleport(waypoint.m_position)) return;
            }
        }
        CloseMap();
    }

    private static int GetCost(Waypoint waypoint, Vector3 pos)
    {
        if (waypoint == null) return 1;
        int cost = WaypointsPlugin._cost.Value;
        if (WaypointsPlugin._useDistanceCharge.Value is WaypointsPlugin.Toggle.Off) return cost;
        cost = Mathf.FloorToInt(Utils.DistanceXZ(pos, waypoint.GetPosition()) / WaypointsPlugin._distancePerCharge.Value);
        if (cost == 0) cost = 1;
        return cost;
    }

    private static bool Teleport(Vector3 pos, bool useCost = true)
    {
        if (useCost && WaypointsPlugin._usesCharges.Value is WaypointsPlugin.Toggle.On)
        {
            if (m_currentWaypoint == null) return false;
            int cost = GetCost(m_currentWaypoint, pos);
            if (m_currentWaypoint.GetCurrentCharge() < cost)
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, $"$msg_chargerequired: <color=red>{cost}</color>");
                return false;
            }

            if (!m_currentWaypoint.RemoveCharge(cost)) return false;
        }
        Player.m_localPlayer.TeleportTo(pos + m_exitDistance, Player.m_localPlayer.transform.rotation, true);
        if (WaypointsPlugin._TeleportTames.Value is WaypointsPlugin.Toggle.On)
        {
            TeleportCharacters(GetTames(Player.m_localPlayer), pos, Quaternion.identity);
        }
        return true;
    }

    private static void CloseUI()
    {
        m_teleporting = false;
        foreach (Minimap.PinData? pin in m_tempPins) Minimap.instance.RemovePin(pin);
        m_tempPins.Clear();
        ResetMapMode();
        m_currentWaypoint = null;
    }

    private static void CloseMap()
    {
        Minimap.instance.SetMapMode(Game.m_noMap ? Minimap.MapMode.None : Minimap.MapMode.Small);
        CloseUI();
        MinimapUI.SetElement(true);
    }
    
    private void AddPins(Player player)
    {
        AddCustomSpawnPin();
        AddLocationPins();
        AddWaypointPins(player);
    }

    private void AddWaypointPins(Player player)
    {
        List<Vector3> data = GetPlayerCustomData(player);
        if (data.Count == 0) return;
        HashSet<ZDO> destinations = WaypointManager.FindDestinations();
        foreach (ZDO? destination in destinations)
        {
            if (destination.m_uid == m_nview.GetZDO().m_uid) continue;
            if (!IsMatchFound(data, destination.m_position)) continue;
            string pinName = destination.GetString(m_key);
            bool flag = false;
            if (WaypointsPlugin._usesCharges.Value is WaypointsPlugin.Toggle.On)
            {
                int cost = GetCost(this, destination.m_position);
                flag = cost > GetCurrentCharge();
                pinName += $" (<color={(flag ? "orange" : "#a5c90f")}>{cost}</color>)";
            }
            m_tempPins.Add(Minimap.instance.AddPin(destination.m_position, Minimap.PinType.Icon4, pinName, false, flag));
        }
    }

    private static void AddLocationPins()
    {
        if (WaypointsPlugin._teleportToLocations.Value is WaypointsPlugin.Toggle.Off) return;
        if (!Minimap.instance) return;
        foreach (KeyValuePair<Vector3, Minimap.PinData> pin in Minimap.instance.m_locationPins)
        {
            m_tempPins.Add(new Minimap.PinData()
            {
                m_pos = pin.Value.m_pos,
                m_name = "UniqueLocation" ,
                m_type = Minimap.PinType.Bed
            });
        }
    }

    private static void AddCustomSpawnPin()
    {
        if (WaypointsPlugin._teleportToBed.Value is WaypointsPlugin.Toggle.Off) return;
        PlayerProfile? profile = Game.instance.GetPlayerProfile();
        if (profile.HaveCustomSpawnPoint())
        {
            m_tempPins.Add(new Minimap.PinData()
            {
                m_pos = profile.GetCustomSpawnPoint(),
                m_name = "SpawnPoint",
                m_type = Minimap.PinType.Bed
            });
        }
    }
    
    public static bool IsMatchFound(List<Vector3> list, Vector3 pos) => list.Any(position => MatchFound(position, pos));
    public static bool MatchFound(Vector3 x, Vector3 y)
    {
        if ((int)x.x != (int)y.x) return false;
        if ((int)x.y != (int)y.y) return false;
        if ((int)x.z != (int)y.z) return false;
        return true;
    }
    
    private void SaveWaypoint()
    {
        if (!Player.m_localPlayer) return;
        List<Vector3> data = GetPlayerCustomData(Player.m_localPlayer);
        if (IsMatchFound(data, GetPosition())) return;
        ISerializer serializer = new SerializerBuilder().Build();
        data.Add(m_nview.GetZDO().m_position);
        List<string> info = data.Select(FormatPosition).ToList();
        Player.m_localPlayer.m_customData[m_playerCustomDataKey] = serializer.Serialize(info);
    }

    public static string FormatPosition(Vector3 position) => $"{position.x},{position.y},{position.z}";

    public static List<Vector3> GetPlayerCustomData(Player player)
    {
        if (!player.m_customData.TryGetValue(m_playerCustomDataKey, out string data)) return new();
        if (data.IsNullOrWhiteSpace()) return new();
        IDeserializer deserializer = new DeserializerBuilder().Build();
        List<string> list = deserializer.Deserialize<List<string>>(data);
        List<Vector3> positions = new();
        foreach (string? input in list)
        {
            if (!GetVector(input, out Vector3 position)) continue;
            positions.Add(position);
        }

        return positions;
    }

    public static bool GetVector(string input, out Vector3 output)
    {
        output = Vector3.zero;
        string[] info = input.Split(',');
        if (info.Length != 3) return false;
        float x = float.Parse(info[0]);
        float y = float.Parse(info[1]);
        float z = float.Parse(info[2]);
        output = new Vector3(x, y, z);
        return true;
    }

    private bool CanRename()
    {
        if (WaypointsPlugin._onlyAdminRenames.Value is WaypointsPlugin.Toggle.Off) return true;
        try
        {
            string hostName = PrivilegeManager.GetNetworkUserId().Replace("Steam_", string.Empty);
            return ZNet.instance.IsAdmin(hostName);
        }
        catch
        {
            return false;
        }
    }

    public bool Interact(Humanoid user, bool hold, bool alt)
    {
        SaveWaypoint();
        if (hold) return false;
        if (alt)
        {
            if (CanRename()) TextInput.instance.RequestText(this, "$hud_rename", 40);
        }
        else
        {
            if (user is not Player player) return false;
            if (!CanTeleport(player))
            {
                return false;
            }
            
            OpenMap(user);
        }
        return true;
    }

    private void OpenMap(Humanoid user)
    {
        if (user is not Player player || !Minimap.instance) return;
        SetMapMode();
        m_teleporting = true;
        AddPins(player);
        Minimap.instance.ShowPointOnMap(transform.position);
        m_currentWaypoint = this;
        MinimapUI.SetElement(false);
    }
    
    public bool UseItem(Humanoid user, ItemDrop.ItemData item)
    {
        if (WaypointsPlugin._usesCharges.Value is WaypointsPlugin.Toggle.Off) return false;
        if (GetChargeItem()?.m_itemData.m_shared.m_name != item.m_shared.m_name) return false;
        int stack = item.m_stack;
        int max = WaypointsPlugin._chargeMax.Value - GetCurrentCharge();
        if (stack > max)
        {
            if (!AddCharge(max))
            {
                user.Message(MessageHud.MessageType.Center, "$msg_fullycharged");
            }
            else
            {
                if (!user.GetInventory().RemoveItem(item, max))
                {
                    user.Message(MessageHud.MessageType.Center, "$msg_fullycharged");
                }
            }
        }
        else
        {
            if (!AddCharge(stack))
            {
                user.Message(MessageHud.MessageType.Center, "$msg_fullycharged");
            }
            else
            {
                if (!user.GetInventory().RemoveItem(item))
                {
                    user.Message(MessageHud.MessageType.Center, "$msg_fullycharged");
                }
            }
        }
        return true;
    }

    public string GetHoverText()
    {
        StringBuilder stringBuilder = new StringBuilder();
        stringBuilder.Append(GetText() + "\n");
        if (WaypointsPlugin._usesCharges.Value is WaypointsPlugin.Toggle.On) 
            stringBuilder.AppendFormat("{0}: {1}/{2} \n", GetChargeItem()?.m_itemData.m_shared.m_name, GetCurrentCharge(), WaypointsPlugin._chargeMax.Value);
        stringBuilder.AppendFormat("[<color=yellow>{0}</color>] {1}\n", "$KEY_Use", "$piece_use");
        if (CanRename()) stringBuilder.AppendFormat("[<color=yellow>{0}</color>] {1}", "L.Shift + $KEY_Use", "$hud_rename");
        
        return Localization.instance.Localize(stringBuilder.ToString());
    }

    public string GetHoverName() => "$piece_waypoint";

    private void RPC_SetName(long sender, string text)
    {
        if (!m_nview.IsValid()) return;
        m_nview.GetZDO().Set(m_key, text);
    }

    public string GetText() => m_nview.IsValid() ? m_nview.GetZDO().GetString(m_key) : "";

    public void SetText(string text)
    {
        if (!m_nview.IsValid()) return;
        m_nview.InvokeRPC(nameof(RPC_SetName), text);
    }

    private static List<Character> GetTames(Player player)
    {
        List<Character> m_characters = new();
        foreach (Character? character in Character.GetAllCharacters())
        {
            if (!character.TryGetComponent(out Tameable tameable)) continue;
            if (tameable.m_monsterAI == null) continue;
            GameObject follow = tameable.m_monsterAI.GetFollowTarget();
            if (!follow) continue;
            if (!follow.TryGetComponent(out Player component)) continue;
            if (component.GetHoverName() != player.GetHoverName()) continue;
            m_characters.Add(character);
        }

        return m_characters;
    }
    
    private static void TeleportCharacters(List<Character> characters, Vector3 position, Quaternion rotation)
    {
        foreach (Character? character in characters)
        {
            Vector3 random = Random.insideUnitSphere * 10f;
            Vector3 location = position + new Vector3(random.x, 0f, random.z);
            TeleportTo(character, location, rotation);
        }
    }
    
    private static void TeleportTo(Character character, Vector3 pos, Quaternion rot)
    {
        if (!character.m_nview.IsOwner()) character.m_nview.ClaimOwnership();
        Transform transform1 = character.transform;
        pos.y = ZoneSystem.instance.GetSolidHeight(pos) + 0.5f;
        transform1.position = pos;
        transform1.rotation = rot;
        character.m_body.velocity = Vector3.zero;
    }
    
    [HarmonyPatch(typeof(Minimap), nameof(Minimap.OnMapLeftClick))]
    private static class Minimap_OnMapLeftClick_Patch
    {
        private static bool Prefix()
        {
            if (!m_teleporting) return true;
            HandleMapClick();
            return false;
        }
    }

    [HarmonyPatch(typeof(Minimap), nameof(Minimap.SetMapMode))]
    private static class Minimap_SetMapMode_Patch
    {
        private static void Postfix(Minimap.MapMode mode)
        {
            if (mode is not Minimap.MapMode.Large) CloseUI();
        }
    }
}