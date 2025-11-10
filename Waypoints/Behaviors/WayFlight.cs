// using UnityEngine;
// using Random = UnityEngine.Random;
//
// namespace Waypoints.Behaviors;
//
// public class WayFlight : MonoBehaviour
// {
//     public static WayFlight m_instance;
//     public float m_speed = 10f;
//     public float m_turnRate = 5f;
//     public float m_dropHeight = 10f;
//     public float m_startAltitude = 500f;
//     public float m_descentAltitude = 100f;
//     public float m_startDistance = 500f;
//     public float m_startDescentDistance = 200f;
//     public Vector3 m_attachOffset = new Vector3(0.0f, 0.0f, 1f);
//     public Transform m_attachPoint;
//     public Vector3 m_targetPoint;
//     public Vector3 m_ascentPoint;
//     public Vector3 m_descentStart;
//     public Vector3 m_flyAwayPoint;
//     public bool m_descent;
//     public bool m_droppedPlayer;
//     public bool m_pickedPlayer;
//     public Animator m_animator;
//     public ZNetView m_nview;
//
//     public void Awake()
//     {
//         m_instance = this;
//         m_nview = GetComponent<ZNetView>();
//         m_animator = GetComponentInChildren<Animator>();
//         m_attachPoint = Utils.FindChild(transform, "Attach");
//         if (!m_nview.IsOwner())
//         {
//             enabled = false;
//         }
//         else
//         {
//             Debug.LogWarning("Setting up wayflight");
//             float randomSpawnRotation = (float)(Random.value * 3.1415 * 2.0);
//             Vector3 spawnPos = new Vector3(Mathf.Sin(randomSpawnRotation), 0.0f, Mathf.Cos(randomSpawnRotation));
//             Vector3 up = Vector3.Cross(spawnPos, Vector3.up);
//             m_targetPoint = Player.m_localPlayer.transform.position;
//             transform.position = (m_targetPoint + spawnPos * m_startDistance) with
//             {
//                 y = m_startAltitude
//             };
//             m_descentStart = m_targetPoint + spawnPos * m_startDescentDistance + up * 200f;
//             m_descentStart.y = m_descentAltitude;
//             Vector3 descentPoint = (m_targetPoint - m_descentStart) with
//             {
//                 y = 0.0f
//             };
//             descentPoint.Normalize();
//             m_flyAwayPoint = m_targetPoint + descentPoint * m_startDescentDistance;
//             m_flyAwayPoint.y = m_startAltitude;
//         }
//     }
//
//     public void OnDestroy() => Debug.LogWarning("destroying wayflight valkyrie");
//
//     public void FixedUpdate()
//     {
//         if (!m_pickedPlayer)
//         {
//             UpdatePickup(Time.deltaTime);
//         }
//         else
//         {
//             UpdateFlight(Time.fixedDeltaTime);
//             if (m_droppedPlayer) return;
//             SyncPlayer(true);
//         }
//     }
//
//     public void PickupPlayer()
//     {
//         m_pickedPlayer = true;
//         Debug.LogWarning("Setting up wayflight");
//         float randomSpawnRotation = (float)(Random.value * 3.1415 * 2.0);
//         Vector3 spawnPos = new Vector3(Mathf.Sin(randomSpawnRotation), 0.0f, Mathf.Cos(randomSpawnRotation));
//         Vector3 up = Vector3.Cross(spawnPos, Vector3.up);
//         
//         Vector3 dropOffPos = Vector3.zero;
//         
//         m_targetPoint = dropOffPos + new Vector3(0.0f, m_dropHeight, 0.0f);
//         transform.position = (m_targetPoint + spawnPos * m_startDistance) with
//         {
//             y = m_startAltitude
//         };
//         m_descentStart = m_targetPoint + spawnPos * m_startDescentDistance + up * 200f;
//         m_descentStart.y = m_descentAltitude;
//         Vector3 descentPoint = (m_targetPoint - m_ascentPoint) with
//         {
//             y = 0.0f
//         };
//         descentPoint.Normalize();
//         m_flyAwayPoint = m_targetPoint + descentPoint * m_startDescentDistance;
//         m_flyAwayPoint.y = m_startAltitude;
//         SyncPlayer(true);
//     }
//
//     public void UpdatePickup(float dt)
//     {
//         Vector3 currentPos = !m_pickedPlayer ? (m_descent ? m_descentStart : m_targetPoint) : m_flyAwayPoint;
//         if (Utils.DistanceXZ(currentPos, transform.position) <= 0.5)
//         {
//             if (!m_descent)
//             {
//                 m_descent = true;
//                 Debug.LogWarning("Starting descent");
//             }
//             else if (!m_pickedPlayer)
//             {
//                 Debug.LogWarning("I am here");
//                 PickupPlayer();
//             }
//         }
//
//         Vector3 nextPos = transform.position + (currentPos - transform.position).normalized * 25f;
//         if (ZoneSystem.instance.GetGroundHeight(nextPos, out var height))
//         {
//             nextPos.y = Mathf.Max(nextPos.y, height + m_dropHeight);
//         }
//         Vector3 normalized = (nextPos - transform.position).normalized;
//         Quaternion lookRot = Quaternion.LookRotation(normalized);
//         Vector3 toPos = normalized with { y = 0.0f };
//         toPos.Normalize();
//         Vector3 forward = transform.forward with { y = 0.0f };
//         forward.Normalize();
//         transform.rotation = Quaternion.RotateTowards(transform.rotation,
//             Quaternion.Euler(0.0f, 0.0f,
//                 Mathf.Clamp(Vector3.SignedAngle(forward, toPos, Vector3.up), -30f, 30f) / 30f * 45f) * lookRot,
//             (m_pickedPlayer ? m_turnRate * 45f : m_turnRate) * dt);
//         Vector3 speed = transform.position + transform.forward * m_speed * dt;
//         if (ZoneSystem.instance.GetGroundHeight(speed, out var newHeight))
//         {
//             speed.y = Mathf.Max(speed.y, newHeight - m_dropHeight);
//         }
//         transform.position = speed;
//     }
//
//     public void UpdateFlight(float dt)
//     {
//         Vector3 currentPos = !m_droppedPlayer ? (m_descent ? m_descentStart : m_targetPoint) : m_flyAwayPoint;
//         if (Utils.DistanceXZ(currentPos, transform.position) <= 0.5)
//         {
//             if (!m_descent)
//             {
//                 m_descent = true;
//                 Debug.LogWarning("Starting descent");
//             }
//             else if (!m_droppedPlayer)
//             {
//                 Debug.LogWarning("We are here");
//                 DropPlayer();        
//             }
//             else
//             {
//                 m_nview.Destroy();
//             }
//         }
//
//         Vector3 nextPos = transform.position + (currentPos - transform.position).normalized * 25f;
//         if (ZoneSystem.instance.GetGroundHeight(nextPos, out var height))
//         {
//             nextPos.y = Mathf.Max(nextPos.y, height + m_dropHeight);
//         }
//         Vector3 normalized = (nextPos - transform.position).normalized;
//         Quaternion lookRot = Quaternion.LookRotation(normalized);
//         Vector3 toPos = normalized with { y = 0.0f };
//         toPos.Normalize();
//         Vector3 forward = transform.forward with { y = 0.0f };
//         forward.Normalize();
//         transform.rotation = Quaternion.RotateTowards(transform.rotation,
//             Quaternion.Euler(0.0f, 0.0f,
//                 Mathf.Clamp(Vector3.SignedAngle(forward, toPos, Vector3.up), -30f, 30f) / 30f * 45f) * lookRot,
//             (m_droppedPlayer ? m_turnRate * 45f : m_turnRate) * dt);
//         Vector3 speed = transform.position + transform.forward * m_speed * dt;
//         if (ZoneSystem.instance.GetGroundHeight(speed, out var newHeight))
//         {
//             speed.y = Mathf.Max(speed.y, newHeight - m_dropHeight);
//         }
//         transform.position = speed;
//     }
//
//     public void DropPlayer(bool destroy = false)
//     {
//         Debug.Log("Dropping player");
//         m_droppedPlayer = true;
//         Vector3 forward = transform.forward with { y = 0.0f };
//         forward.Normalize();
//         Player.m_localPlayer.transform.rotation = Quaternion.LookRotation(forward);
//         m_animator.SetBool("dropped", true);
//         if (!destroy) return;
//         m_nview.Destroy();
//     }
//     
//     public void SyncPlayer(bool doNetworkSync)
//     {
//         Player localPlayer = Player.m_localPlayer;
//         if (localPlayer == null)
//         {
//             Debug.LogError("Player not found");
//         }
//         else
//         {
//             localPlayer.transform.rotation = m_attachPoint.rotation;
//             localPlayer.transform.position = m_attachPoint.position - localPlayer.transform.TransformVector(m_attachOffset);
//             localPlayer.GetComponent<Rigidbody>().position = localPlayer.transform.position;
//             if (!doNetworkSync) return;
//             ZNet.instance.SetReferencePosition(localPlayer.transform.position);
//             localPlayer.GetComponent<ZSyncTransform>().SyncNow();
//             GetComponent<ZSyncTransform>().SyncNow();
//         }
//     }
// }