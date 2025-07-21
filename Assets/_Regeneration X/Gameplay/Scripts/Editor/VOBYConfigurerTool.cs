using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Unity.Physics.Authoring;

public class VOBYConfigurerTool : EditorWindow
{
    private GameObject vobyGameObject;

    [MenuItem("NuttyTools/VOBY Configurer Tool")]
    public static void ShowWindow()
    {
        GetWindow<VOBYConfigurerTool>("VOBY Configurer Tool");
    }

    private void OnGUI()
    {
        GUILayout.Label("VOBY Configurer", EditorStyles.boldLabel);
        vobyGameObject = (GameObject)EditorGUILayout.ObjectField("VOBY GameObject", vobyGameObject, typeof(GameObject), true);
        if (GUILayout.Button("Find VOBs & Configure"))
        {
            ConfigureVOBs();
        }
    }

    private void ConfigureVOBs()
    {
        if (vobyGameObject == null)
        {
            Debug.LogError("Please assign all fields.");
            return;
        }

        // Add VOBYAuthoring if not present
        var vobyAuthoring = vobyGameObject.GetComponent<VOBYAuthoring>();
        if (vobyAuthoring == null)
        {
            vobyAuthoring = Undo.AddComponent<VOBYAuthoring>(vobyGameObject);
            Undo.RecordObject(vobyAuthoring, "Configure VOBYAuthoring");
        }

        // Get all child GameObjects (direct children)
        List<GameObject> vobChildren = new List<GameObject>();
        foreach (Transform child in vobyGameObject.transform)
        {
            vobChildren.Add(child.gameObject);
        }

        // No sorting or index assignment based on distance

        foreach (var vob in vobChildren)
        {
            var authoring = vob.GetComponent<VOBAuthoring>();
            if (authoring == null)
            {
                authoring = Undo.AddComponent<VOBAuthoring>(vob);
            }
            Undo.RecordObject(authoring, "Configure VOBAuthoring");
            authoring.VOBYParent = vobyGameObject;
        }

        Debug.Log($"Configured {vobChildren.Count} VOBs.");
    }
}