using UnityEngine;

public class LayerFixer : MonoBehaviour
{
    void Start()
    {
        AssignLayers();
    }

    void AssignLayers()
    {
        foreach (Transform t in GetComponentsInChildren<Transform>())
        {
            string name = t.name.ToLower();

            if (name.Contains("tile") || name.Contains("floor"))
            {
                t.gameObject.layer = LayerMask.NameToLayer("Ground");
            }
            else if (name.Contains("wall") || name.Contains("pillar") || name.Contains("column"))
            {
                t.gameObject.layer = LayerMask.NameToLayer("Obstacles");
            }
        }
    }
}