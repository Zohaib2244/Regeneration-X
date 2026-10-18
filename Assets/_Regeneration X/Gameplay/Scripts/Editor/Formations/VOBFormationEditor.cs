using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(VOBFormation))]
public class VOBFormationEditor : Editor
{
    int rebakeCost = -1;

    public override void OnInspectorGUI()
    {
        var f = (VOBFormation)target;
        if (rebakeCost < 0) rebakeCost = Mathf.Max(1, f.Cost);

        EditorGUILayout.HelpBox(
            $"Cube cost: {f.Cost}\n" +
            $"Surface {f.surfaceSlots} · just inside {f.interiorSlots}" + (f.duplicateSlots > 0 ? $" · stacked {f.duplicateSlots}" : "") + "\n" +
            $"Room for {(f.reserveSlots != null ? f.reserveSlots.Length : 0)} spare cubes inside, the rest float in a halo.",
            MessageType.None);

        var source = VOBFormationBakerWindow.LoadSource(f.sourceGuid);
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.ObjectField("Baked From", source, typeof(Object), false);

        using (new EditorGUI.DisabledScope(source == null))
        using (new EditorGUILayout.HorizontalScope())
        {
            rebakeCost = Mathf.Max(1, EditorGUILayout.IntField("Re-bake at cost", rebakeCost));
            if (GUILayout.Button("Re-bake", GUILayout.Width(70)))
            {
                var result = FormationVoxelizer.Bake(source, rebakeCost, f.cubeSize, f.fillMode, f.pivotMode);
                if (result.slots == null)
                    Debug.LogError($"[Formation Baker] {result.error}", f);
                else
                {
                    Undo.RecordObject(f, "Re-bake formation");
                    VOBFormationBakerWindow.Apply(f, result, source, f.cubeSize, f.fillMode, f.pivotMode);
                    EditorUtility.SetDirty(f);
                    AssetDatabase.SaveAssets();
                    if (!string.IsNullOrEmpty(result.error)) Debug.LogWarning($"[Formation Baker] {result.error}", f);
                }
            }
        }
        if (GUILayout.Button("Open in Formation Baker"))
            VOBFormationBakerWindow.OpenFor(f);

        EditorGUILayout.Space();
        DrawDefaultInspector();
    }
}
