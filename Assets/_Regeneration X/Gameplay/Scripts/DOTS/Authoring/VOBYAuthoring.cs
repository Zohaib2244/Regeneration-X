using Unity.Entities;
using Unity.Mathematics;
using System.IO;
using UnityEngine;
using System.Collections.Generic;
using AdvancedEditorTools.Attributes;

public class VOBYAuthoring : MonoBehaviour
{
    public VOBYShape VOBYShape = VOBYShape.Cube;
    public class Baker : Baker<VOBYAuthoring>
    {
        public override void Bake(VOBYAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new VOBYComponent { VOBYShape = authoring.VOBYShape });
        }
    }

    string JSONSavePath => $"Assets/Resources/{VOBYShape}.json";

    [Button("Extract VOBs Transforms")]
    public void ExtractVOBsTransforms()
    {
        if (File.Exists(JSONSavePath))
        {
            Debug.LogWarning($"JSON file already exists at {JSONSavePath}. Extraction skipped.");
            return;
        }

        var vobList = new List<VOBTransformData>();
        foreach (Transform child in transform)
        {
            vobList.Add(new VOBTransformData
            {
                position = child.position,
                rotation = child.rotation
            });
        }

        string json = JsonUtility.ToJson(new Serialization<VOBTransformData>(vobList), true);
        File.WriteAllText(JSONSavePath, json);
        Debug.Log($"Saved {vobList.Count} VOB transforms to {JSONSavePath}");
    }
}