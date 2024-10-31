using System.Collections.Generic;
using UnityEngine;

namespace Waypoints.Behaviors;

public class GlowTrails : MonoBehaviour
{
    private static readonly GameObject m_connectionPrefab =
        WaypointsPlugin._assetBundle.LoadAsset<GameObject>("vfx_waypoint_connection");

    private Waypoint m_waypoint = null!;
    private ZNetView m_nview = null!;
    private readonly Dictionary<Player, GameObject> m_connections = new();
    private Vector3 m_topPoint;

    public void Awake()
    {
        m_nview = GetComponent<ZNetView>();
        m_waypoint = GetComponent<Waypoint>();
        m_topPoint = transform.Find("attach_point").position;
    }

    public void Update()
    {
        if (!m_nview.IsValid()) return;
        if (WaypointsPlugin._showConnectionTrails.Value is WaypointsPlugin.Toggle.Off)
        {
            OnDestroy();
            return;
        }
        
        foreach (Player player in Player.GetAllPlayers())
        {
            var distance = Vector3.Distance(player.transform.position, transform.position);
            if (distance > WaypointsPlugin._connectionMaxRange.Value || m_waypoint.IsKnown(player))
            {
                RemoveConnection(player);
            }
            else
            {
                UpdateConnection(player);
            }
        }
    }

    public void OnDestroy()
    {
        foreach (KeyValuePair<Player, GameObject> kvp in m_connections) Destroy(kvp.Value);
        m_connections.Clear();
    }

    private void RemoveConnection(Player player)
    {
        if (!m_connections.TryGetValue(player, out GameObject connection)) return;
        Destroy(connection);
        m_connections.Remove(player);
    }
    
    private void UpdateConnection(Player player)
    {
        if (!m_connections.TryGetValue(player, out GameObject connection))
        {
            connection = Instantiate(m_connectionPrefab, player.GetCenterPoint(), Quaternion.identity);
        }
        Vector3 vector3 = m_topPoint - player.GetCenterPoint();
        Quaternion quaternion = Quaternion.LookRotation(vector3.normalized);
        connection.transform.position = player.GetCenterPoint(); 
        connection.transform.rotation = quaternion;
        connection.transform.localScale = new Vector3(1f, 1f, vector3.magnitude);
        m_connections[player] = connection;
    }
}