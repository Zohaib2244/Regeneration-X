using Unity.Entities;
using Unity.Mathematics;
using System.IO;
using UnityEngine;
using System.Collections.Generic;
using AdvancedEditorTools.Attributes;

public class VOBYAuthoring : MonoBehaviour
{
    public VOBYShape VOBYShape = VOBYShape.Cube;

    [Header("Ownership")]
    [Tooltip("The VOBY the player controls. It can collect loose cubes and is what the bridge morphs.")]
    public bool isPlayer = false;
    [Tooltip("Size of one cube, used to space the spare-cube halo.")]
    public float cubeSize = 0.25f;
    [Tooltip("Extra slots past the starting cube count, so collected cubes have somewhere to go before the next morph.")]
    public int spareCapacity = FormationMorphUtility.DefaultSpareCapacity;

    [Header("Collecting (player only)")]
    public bool collectEnabled = true;
    [Tooltip("Loose cubes closer than this get pulled in.")]
    public float collectRadius = 6f;
    public float pullSpeed = 12f;
    [Tooltip("Absorbed once this close to the outside of the shape.")]
    public float absorbMargin = 0.5f;
    public float absorbAnimationDuration = 0.35f;

    public class Baker : Baker<VOBYAuthoring>
    {
        public override void Bake(VOBYAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new VOBYComponent { VOBYShape = authoring.VOBYShape });
            AddComponent(entity, new VOBYCubeCount());

            // The starting layout (the VOBs as placed under this object) becomes the first formation.
            var shape = new List<Vector3>();
            var rotations = new List<Quaternion>();
            for (int i = 0; i < authoring.transform.childCount; i++)
            {
                var child = authoring.transform.GetChild(i);
                if (GetComponent<VOBAuthoring>(child) == null) continue;
                shape.Add(child.localPosition);
                rotations.Add(child.localRotation);
            }

            var layout = FormationSlotLayout.Build(shape, rotations, null, authoring.cubeSize,
                shape.Count + Mathf.Max(0, authoring.spareCapacity), out float shapeRadius);
            var buffer = AddBuffer<VOBYSlot>(entity);
            foreach (var slot in layout) buffer.Add(slot);

            AddComponent(entity, new VOBYFormationInfo
            {
                ShapeSlots = shape.Count,
                NextFree = shape.Count,
                ShapeRadius = shapeRadius,
                OuterRadius = shapeRadius,
                CubeSize = authoring.cubeSize
            });

            if (authoring.isPlayer)
            {
                AddComponent<PlayerVOBYTag>(entity);
                AddComponent(entity, new VOBCollector
                {
                    Enabled = authoring.collectEnabled,
                    Radius = authoring.collectRadius,
                    PullSpeed = authoring.pullSpeed,
                    AbsorbMargin = authoring.absorbMargin,
                    AbsorbAnimationDuration = authoring.absorbAnimationDuration
                });
            }
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

#if UNITY_EDITOR
    /// <summary>Saves the children's current local layout as a VOBFormation asset (any hand-made or Rayfire layout).</summary>
    [Button("Save Layout As Formation")]
    public void SaveLayoutAsFormation()
    {
        var slots = new List<Vector3>();
        var rots = new List<Quaternion>();
        foreach (Transform child in transform)
        {
            if (child.GetComponent<VOBAuthoring>() == null) continue;
            slots.Add(child.localPosition);
            rots.Add(child.localRotation);
        }
        if (slots.Count == 0)
        {
            Debug.LogWarning("No VOB children found. Run NuttyTools > VOBY Configurer Tool first.");
            return;
        }

        const string folder = "Assets/_Regeneration X/Gameplay/Formations";
        if (!UnityEditor.AssetDatabase.IsValidFolder(folder))
            UnityEditor.AssetDatabase.CreateFolder("Assets/_Regeneration X/Gameplay", "Formations");

        var formation = ScriptableObject.CreateInstance<VOBFormation>();
        formation.slots = slots.ToArray();
        formation.rotations = rots.ToArray();
        formation.reserveSlots = new Vector3[0];
        formation.cubeSize = cubeSize;
        formation.surfaceSlots = slots.Count;

        string path = UnityEditor.AssetDatabase.GenerateUniqueAssetPath($"{folder}/{name}_Layout.asset");
        UnityEditor.AssetDatabase.CreateAsset(formation, path);
        UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.EditorGUIUtility.PingObject(formation);
        Debug.Log($"Saved {slots.Count}-cube formation to {path}", formation);
    }
#endif
}
