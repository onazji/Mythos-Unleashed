using UnityEngine;
using UnityEditor;

public class EntryPlacer:MonoBehaviour{
    public GameObject entryPrefab;
    public Component dungeonQuery;
    public Transform startAnchor;

    private const string SpawnedName = "DEBUG_ENTRY";

    [ContextMenu("Place Entry")]
    public void PlaceEntry()
    {
        if(entryPrefab == null){
            Debug.LogError("EntryPlacer: Missing prefab ");
            return;
        } 
        GameObject oldEntry = GameObject.Find(SpawnedName);
        if(oldEntry != null)
        {
            DestroyImmediate(oldEntry);
        }
        // GameObject newEntry = Instantiate(entryPrefab, startAnchor.position, Quaternion.identity);

        Vector3 spawnPos = startAnchor != null ? startAnchor.position : transform.position;

        var queryType = dungeonQuery.GetType();
        var method = queryType.GetMethod("GetRandomCellOfType");

        if(method != null)
        {
            object result = method.Invoke(dungeonQuery,new object[]{"Room"});
            if(result is Vector3 roomPos){
                spawnPos = roomPos;
            }
        }
        GameObject newEntry = Instantiate(entryPrefab,spawnPos,Quaternion.identity);


        newEntry.name = SpawnedName;

        Debug.Log("Entry placed from generated dungeon query");
    }
    [ContextMenu("BuildDungeon + Place Place Entry")]
    public void BuildDungeonAndPlaceEntry()
    {
        SendMessage("Build",SendMessageOptions.DontRequireReceiver);
        EditorApplication.delayCall += DelayedPlaceEntry;

    }
    private void DelayedPlaceEntry(){
        EditorApplication.delayCall -= DelayedPlaceEntry;
        PlaceEntry();
    }
    }