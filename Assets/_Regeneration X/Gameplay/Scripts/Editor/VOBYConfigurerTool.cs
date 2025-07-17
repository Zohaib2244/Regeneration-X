using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Unity.Physics.Authoring;

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
        if (vobyGameObject == null || referenceTransform == null)
        {
            Debug.LogError("Please assign all fields.");
            return;
        }

        // Add VOBYAuthoring if not present
        var vobyAuthoring = vobyGameObject.GetComponent<VOBYAuthoring>();
        if (vobyAuthoring == null)
        {
            vobyAuthoring = Undo.AddComponent<VOBYAuthoring>(vobyGameObject);
            vobyAuthoring.epicenter = referenceTransform.position;
            Undo.RecordObject(vobyAuthoring, "Configure VOBYAuthoring");
        }

        // Get all child GameObjects (direct children)
        List<GameObject> vobChildren = new List<GameObject>();
        foreach (Transform child in vobyGameObject.transform)
        {
            vobChildren.Add(child.gameObject);
        }

        // Sort by world position distance from referenceTransform
        vobChildren = vobChildren
            .OrderBy(go => Vector3.Distance(go.transform.position, referenceTransform.position))
            .ToList();

        // Assign unique index to each VOB (closest = 1, next = 2, ...)
        int currentIndex = 1;
        foreach (var vob in vobChildren)
        {
            var authoring = vob.GetComponent<VOBAuthoring>();
            if (authoring == null)
            {
                authoring = Undo.AddComponent<VOBAuthoring>(vob);
            }
            Undo.RecordObject(authoring, "Configure VOBAuthoring");
            authoring.VOBYParent = vobyGameObject;
            authoring.VOBIndex = currentIndex;
            // After assigning index
            var renderer = vob.GetComponent<Renderer>();
            if (renderer != null)
            {
                // Assign a unique material instance
                renderer.material = new Material(renderer.sharedMaterial);

                float t = (float)(currentIndex - 1) / (vobChildren.Count - 1);
                renderer.material.color = Color.Lerp(Color.red, Color.green, t); // Gradient color
            }
            currentIndex++;

        }

        Debug.Log($"Configured {vobChildren.Count} VOBs.");
        // Set VOBYAuthoring batch size
    }
    public void ResetColor()
    {
        if (vobyGameObject == null)
        {
            Debug.LogError("Please assign the VOBY GameObject.");
            return;
        }

        // Reset colors of all child VOBs
        foreach (Transform child in vobyGameObject.transform)
        {
            var renderer = child.GetComponent<Renderer>();
            if (renderer != null)
            {
                // Reset to the original material
                renderer.material = renderer.sharedMaterial;
            }
        }
    }
}
