using UnityEngine;

public class QueryInspector : MonoBehaviour
{
    public Component dungeonQuery;
    [ContextMenu("List Methods")]


    void ListMethods()
    {
        var methods = dungeonQuery.GetType().GetMethods();

        foreach (var m in methods)
        {
        Debug.Log(m.Name);
        }
        var props = dungeonQuery.GetType().GetProperties();
        foreach (var p in props)
        {
        Debug.Log("PROP: "+ p.Name);
        }
    }

        [ContextMenu("List Query Signatures")]
    void ListQuerySignatures()
    {
        if(dungeonQuery==null)
        {
            Debug.LogError("QueryInspector: dungeonQuery is not assigned");
            return;
        }

        var targetNames = new string[]{
            "GetLayoutNode",
            "IsMainPath",
            "GetLayoutNodeTile",
            "TileCoordToWorld"
        };

        var methods = dungeonQuery.GetType().GetMethods();
        foreach (var m in methods)
        {
            foreach (var target in targetNames)
            {
                if(m.Name == target){
                    var parameters = m.GetParameters();
                    string sig = m.ReturnType.Name + " " + m.Name + "(";
                    for(int i = 0; i < parameters.Length; i++){
                        sig += parameters[i].ParameterType.Name + " "+ parameters[i].Name;
                        if(i < parameters.Length-1) sig += ", ";
                    }
                    sig += ")";
                    Debug.Log(sig);
                }
            }
        }
    }
}