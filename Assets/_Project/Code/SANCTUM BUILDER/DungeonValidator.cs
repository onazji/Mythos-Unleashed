using System.Collections.Generic;
using UnityEngine;

public class DungeonValidator : MonoBehaviour
{
    public Transform entry;
    public Transform exit;

    public float nodeSpacing = 4f;

    bool IsWalkable(Vector3 position)
    {
        Collider[] hits = Physics.OverlapSphere(position, 1f);

        foreach (Collider c in hits)
        {
            if (c.gameObject.layer == LayerMask.NameToLayer("Obstacles"))
                return false;
        }

        return true;
    }

    public bool PathExists()
    {
        Queue<Vector3> queue = new Queue<Vector3>();
        HashSet<Vector3> visited = new HashSet<Vector3>();

        queue.Enqueue(entry.position);

        while (queue.Count > 0)
        {
            Vector3 current = queue.Dequeue();

            if (Vector3.Distance(current, exit.position) < nodeSpacing)
                return true;

            Vector3[] neighbors =
            {
                current + Vector3.forward * nodeSpacing,
                current + Vector3.back * nodeSpacing,
                current + Vector3.left * nodeSpacing,
                current + Vector3.right * nodeSpacing
            };

            foreach (Vector3 n in neighbors)
            {
                if (!visited.Contains(n) && IsWalkable(n))
                {
                    visited.Add(n);
                    queue.Enqueue(n);
                }
            }
        }

        return false;
    }
}