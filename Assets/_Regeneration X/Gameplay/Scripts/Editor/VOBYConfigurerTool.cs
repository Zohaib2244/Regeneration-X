using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class VOBYConfigurerTool : EditorWindow
{
    private GameObject vobyGameObject;
    private Transform referenceTransform;
    private int batchSize = 1;

    [MenuItem("NuttyTools/VOBY Configurer Tool")]
    public static void ShowWindow()
    {
        GetWindow<VOBYConfigurerTool>("VOBY Configurer Tool");
    }

    private void OnGUI()
    {
        GUILayout.Label("VOBY Configurer", EditorStyles.boldLabel);
        vobyGameObject = (GameObject)EditorGUILayout.ObjectField("VOBY GameObject", vobyGameObject, typeof(GameObject), true);
        referenceTransform = (Transform)EditorGUILayout.ObjectField("Reference Transform", referenceTransform, typeof(Transform), true);
        batchSize = EditorGUILayout.IntField("VOBIndex Batch Size", batchSize);

        if (GUILayout.Button("Find VOBs & Configure"))
        {
            ConfigureVOBs();
        }
    }

    private void ConfigureVOBs()
    {
        if (vobyGameObject == null || referenceTransform == null || batchSize < 1)
        {
            Debug.LogError("Please assign all fields and ensure batch size >= 1.");
            return;
        }

        //*  Add VOBYAuthoring if not present
        var vobyAuthoring = vobyGameObject.GetComponent<VOBYAuthoring>();
        if (vobyAuthoring == null)
        {
            vobyAuthoring = Undo.AddComponent<VOBYAuthoring>(vobyGameObject);
            vobyAuthoring.epicenter = referenceTransform.position; // Set epicenter to reference transform position
            Undo.RecordObject(vobyAuthoring, "Configure VOBYAuthoring");
        }
        //* Get all child GameObjects (direct children)
        List<GameObject> vobChildren = new List<GameObject>();
        foreach (Transform child in vobyGameObject.transform)
        {
            vobChildren.Add(child.gameObject);
        }

        //* Sort by distance from referenceTransform
        vobChildren = vobChildren.OrderBy(go => Vector3.Distance(go.transform.position, referenceTransform.position)).ToList();

        int currentIndex = 1;
        int batchCounter = 0;
        foreach (var vob in vobChildren)
        {
            //* Add VOBAuthoring if not present
            var authoring = vob.GetComponent<VOBAuthoring>();
            if (authoring == null)
            {
                authoring = Undo.AddComponent<VOBAuthoring>(vob);
            }
            Undo.RecordObject(authoring, "Configure VOBAuthoring");
            authoring.VOBYParent = vobyGameObject;
            authoring.VOBIndex = currentIndex;

            batchCounter++;
            if (batchCounter >= batchSize)
            {
                batchCounter = 0;
                currentIndex++;
            }
        }

        Debug.Log($"Configured {vobChildren.Count} VOBs under {vobyGameObject.name}.");
    }
}
