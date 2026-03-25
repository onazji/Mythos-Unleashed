/*
Onazji Drayden Entry Placer.
Generated Entry Placement now reads dungeon main path
Entry Placement now queries DA
*/

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

        Vector3 spawnPos = startAnchor != null ? startAnchor.position : transform.position;
        var queryType = dungeonQuery.GetType();
        var isMainPathMethod= queryType.GetMethod("IsMainPath");

        if(isMainPathMethod != null)
        {
            for(int x = -50; x<=50;x+=2)
            {
                for(int z = -50;z<=50;z+=2)
                {

                    Vector3 center = startAnchor!= null ? startAnchor.position: transform.position;
                    Vector3 testPos = center + new Vector3(x,0,z);


                    bool isMain = (bool)isMainPathMethod.Invoke(dungeonQuery,new object[] {testPos});
                    if(isMain)
                    {
                        spawnPos=testPos;
                        Debug.Log("Found main path at: "+spawnPos );
                        
                        Debug.Log("Test: " + testPos + " => " + isMain);
                        goto FOUND;
                    }
                }
                
            }
        }
        FOUND:

   
               
               var pathMethod = queryType.GetMethod("GetMainPath");
               if(pathMethod != null)
               {
                var path = pathMethod.Invoke(dungeonQuery,null) as System.Collections.IEnumerable;
                if(path != null)
                {
                    foreach (var node in path)
                    {
                        var nodeType = node.GetType();
                        var posProp = nodeType.GetProperty("WorldPosition");

                        if(posProp != null)
                        {
                            spawnPos = (Vector3)posProp.GetValue(node);
                            break;
                        }
                    }
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