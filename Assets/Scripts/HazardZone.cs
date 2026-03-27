using System.Collections.Generic;
using UnityEngine;

public class HazardZone : MonoBehaviour
{
    public string hazardType = "step";
    public float reentryCooldown = 2f;

    private readonly Dictionary<int, float> lastTriggerTimes = new Dictionary<int, float>();

    private void OnTriggerEnter(Collider other)
    {
        DigitalCane cane = other.GetComponentInParent<DigitalCane>();
        if (cane == null)
        {
            return;
        }

        int agentId = cane.gameObject.GetInstanceID();
        if (lastTriggerTimes.TryGetValue(agentId, out float lastTime) && Time.time - lastTime < reentryCooldown)
        {
            return;
        }

        lastTriggerTimes[agentId] = Time.time;
        Vector3 reportPosition = other.ClosestPoint(transform.position);
        cane.ReportHazard(hazardType, reportPosition);
    }

    private void OnDrawGizmos()
    {
        Collider zoneCollider = GetComponent<Collider>();
        if (zoneCollider == null)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0f, 0f, 0.25f);
        Matrix4x4 originalMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        if (zoneCollider is BoxCollider box)
        {
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(1f, 0f, 0f, 0.8f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
        else if (zoneCollider is SphereCollider sphere)
        {
            Gizmos.DrawSphere(sphere.center, sphere.radius);
            Gizmos.color = new Color(1f, 0f, 0f, 0.8f);
            Gizmos.DrawWireSphere(sphere.center, sphere.radius);
        }
        else if (zoneCollider is CapsuleCollider capsule)
        {
            Vector3 center = capsule.center;
            float radius = capsule.radius;
            float height = capsule.height;
            Vector3 size;

            if (capsule.direction == 0)
            {
                size = new Vector3(height, radius * 2f, radius * 2f);
            }
            else if (capsule.direction == 1)
            {
                size = new Vector3(radius * 2f, height, radius * 2f);
            }
            else
            {
                size = new Vector3(radius * 2f, radius * 2f, height);
            }

            Gizmos.DrawCube(center, size);
            Gizmos.color = new Color(1f, 0f, 0f, 0.8f);
            Gizmos.DrawWireCube(center, size);
        }

        Gizmos.matrix = originalMatrix;
    }
}
