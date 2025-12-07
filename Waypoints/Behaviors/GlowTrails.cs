using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Waypoints.Behaviors;

public class GlowTrails : MonoBehaviour
{
    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
    private static class ZNetScene_Awake_Patch
    {
        private static void Postfix()
        {
            m_connectionPrefab = ZNetScene.instance.GetPrefab("piece_workbench_ext1").GetComponent<StationExtension>().m_connectionPrefab;
        }
    }

    private static GameObject? m_connectionPrefab;
    
    private Waypoint m_waypoint = null!;
    private ZNetView m_nview = null!;
    private readonly Dictionary<long, GameObject> m_connections = new();
    private Vector3 m_topPoint;
    private float m_timer;
    private float m_updateInterval = 0.05f;
    public HashSet<long> m_playersToRemove = new();
    

    public void Awake()
    {
        m_nview = GetComponent<ZNetView>();
        m_waypoint = GetComponent<Waypoint>();
        m_topPoint = transform.Find("demister").position;
    }

    public void Update()
    {
        if (!m_nview.IsValid()) return;
        
        if (WaypointsPlugin._showConnectionTrails.Value is WaypointsPlugin.Toggle.Off)
        {
            RemoveAllConnections();
            return;
        }
        
        m_timer += Time.deltaTime;
        if (m_timer < m_updateInterval) return;
        m_timer = 0.0f;
        
        foreach (Player player in Player.GetAllPlayers())
        {
            float distance = Vector3.Distance(player.transform.position, transform.position);
            if (distance > WaypointsPlugin._connectionMaxRange.Value || m_waypoint.IsKnown(player))
            {
                RemoveConnection(player);
            }
            else
            {
                UpdateConnection(player);
            }
        }
        
        m_playersToRemove.Clear();
        foreach (KeyValuePair<long, GameObject> kvp in m_connections)
        {
            if (Player.GetPlayer(kvp.Key) is null) m_playersToRemove.Add(kvp.Key);
        }

        foreach (long id in m_playersToRemove)
        {
            if (m_connections.TryGetValue(id, out GameObject connection))
            {
                if(connection is not null) Destroy(connection);
            }
            m_connections.Remove(id);
        }
        
    }

    public void OnDestroy() => RemoveAllConnections();

    public void RemoveAllConnections()
    {
        foreach (KeyValuePair<long, GameObject> kvp in m_connections) Destroy(kvp.Value);
        m_connections.Clear();
    }

    private void RemoveConnection(Player player)
    {
        if (!m_connections.TryGetValue(player.GetPlayerID(), out GameObject connection)) return;
        Destroy(connection);
        m_connections.Remove(player.GetPlayerID());
    }
    
    private void UpdateConnection(Player player)
    {
        if (m_connectionPrefab is null) return;
        if (!m_connections.TryGetValue(player.GetPlayerID(), out GameObject connection))
        {
            connection = Instantiate(m_connectionPrefab, player.GetCenterPoint(), Quaternion.identity);
        }
        Vector3 vector3 = m_topPoint - player.GetCenterPoint();
        Quaternion quaternion = Quaternion.LookRotation(vector3.normalized);
        connection.transform.position = player.GetCenterPoint(); 
        connection.transform.rotation = quaternion;
        connection.transform.localScale = new Vector3(1f, 1f, vector3.magnitude);
        m_connections[player.GetPlayerID()] = connection;
    }
}