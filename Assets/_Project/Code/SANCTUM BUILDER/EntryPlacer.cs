using UnityEngine;
// using System.Collections.Generic;
// using DungeonArchitect;
// using DungeonArchitect.Builders.GridFlow;


public class EntryPlacer : MonoBehaviour{





















































    public GameObject entryPrefab;
    public Transform startAnchor;

    [ContextMenu("Place Entry")]
    public void PlaceEntry()
    {
        if(entryPrefab == null)
        {
            Debug.LogError("EntryPlacer: entryPrefab is missing.");
            return;
        }
        if(startAnchor==null){Debug.LogError("EntryPlacer: startAnchor is missing.");
        return;
        }
        GameObject oldEntry = GameObject.Find("DEBUG_ENTRY");
        if(oldEntry != null){
            DestroyImmediate(oldEntry);
        }
        GameObject newEntry = Instantiate(entryPrefab,startAnchor.position,startAnchor.rotation);
        newEntry.name = "DEBUG_ENTRY";
    }
 }
