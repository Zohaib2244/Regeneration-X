using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// NuttyTools > VOB Formation Baker
/// Drop any model / prefab / Mesh, type how many cubes the shape should cost, press Bake.
/// Select several models in the Project window and use "Bake Selected" to do a whole folder at once.
/// </summary>
public class VOBFormationBakerWindow : EditorWindow
{
    const string DefaultFolder = "Assets/_Regeneration X/Gameplay/Formations";

    [SerializeField] Object source;
    [SerializeField] int cubeCost = 1000;
    [SerializeField] float cubeSize = 0.25f;
    [SerializeField] VOBFormation.FillMode fillMode = VOBFormation.FillMode.Hollow;
    [SerializeField] VOBFormation.PivotMode pivotMode = VOBFormation.PivotMode.Center;
    [SerializeField] string outputFolder = DefaultFolder;
    [SerializeField] bool appendCostToName = true;

    FormationVoxelizer.Result preview;
    Vector3 previewAnchor;
    string status;
    Vector2 scroll;

    [MenuItem("NuttyTools/VOB Formation Baker")]
    public static VOBFormationBakerWindow Open()
    {
        var w = GetWindow<VOBFormationBakerWindow>("VOB Formation Baker");
        w.minSize = new Vector2(340, 360);
        return w;
    }

    /// <summary>Opens the window pre-filled with a formation's source and settings.</summary>
    public static void OpenFor(VOBFormation formation)
    {
        var w = Open();
        w.source = LoadSource(formation.sourceGuid);
        w.cubeCost = Mathf.Max(1, formation.Cost);
        w.cubeSize = formation.cubeSize;
        w.fillMode = formation.fillMode;
        w.pivotMode = formation.pivotMode;
        string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(formation))?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(folder)) w.outputFolder = folder;
        w.Repaint();
    }

    void OnEnable() => SceneView.duringSceneGui += DrawPreview;
    void OnDisable() => SceneView.duringSceneGui -= DrawPreview;

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        source = EditorGUILayout.ObjectField(new GUIContent("Mesh / Model / Prefab"), source, typeof(Object), true);
        if (EditorGUI.EndChangeCheck()) { preview = null; status = null; }
        if (source != null && !(source is GameObject) && !(source is Mesh))
            EditorGUILayout.HelpBox("Use a model, prefab, scene object, or Mesh asset.", MessageType.Warning);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Shape", EditorStyles.boldLabel);
        cubeCost = Mathf.Max(1, EditorGUILayout.IntField(new GUIContent("Cube Cost", "How many cubes the player needs to take this shape."), cubeCost));
        cubeSize = Mathf.Max(0.0001f, EditorGUILayout.FloatField(new GUIContent("Cube Size", "Size of one VOB. The shape is scaled so cubes tile perfectly."), cubeSize));
        fillMode = (VOBFormation.FillMode)EditorGUILayout.EnumPopup(new GUIContent("Fill", "Hollow = most detail for the cost. Solid = chunkier, full volume."), fillMode);
        pivotMode = (VOBFormation.PivotMode)EditorGUILayout.EnumPopup(new GUIContent("Pivot", "Bottom makes the shape stand on the VOBY's origin."), pivotMode);

        var voby = FindVOBY();
        using (new EditorGUI.DisabledScope(voby == null))
        {
            if (GUILayout.Button(voby != null ? $"Use cube size from '{voby.name}'" : "Use cube size from VOBY (select one)"))
                cubeSize = voby.cubeSize;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            outputFolder = EditorGUILayout.TextField("Folder", outputFolder);
            if (GUILayout.Button("…", GUILayout.Width(24)))
            {
                string picked = EditorUtility.OpenFolderPanel("Formations folder", "Assets", "");
                if (!string.IsNullOrEmpty(picked))
                {
                    string rel = FileUtil.GetProjectRelativePath(picked);
                    if (!string.IsNullOrEmpty(rel)) outputFolder = rel;
                }
            }
        }
        appendCostToName = EditorGUILayout.Toggle(new GUIContent("Add cost to name", "Sword_120.asset"), appendCostToName);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(source == null))
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Preview", GUILayout.Height(28))) DoPreview();
            if (GUILayout.Button("Bake", GUILayout.Height(28))) BakeOne(source);
        }

        var selected = SelectedSources();
        using (new EditorGUI.DisabledScope(selected.Count == 0))
        {
            if (GUILayout.Button(selected.Count > 0
                    ? $"Bake Selected ({selected.Count}) at cost {cubeCost}"
                    : "Bake Selected (select models in the Project window)"))
            {
                int ok = 0;
                foreach (var s in selected)
                    if (BakeOne(s, ping: false) != null) ok++;
                status = $"Baked {ok}/{selected.Count} formations into {outputFolder}.";
            }
        }

        if (preview != null)
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(Describe(preview) + "\nShown as wire cubes at the Scene view's focus point.", MessageType.Info);
            if (GUILayout.Button("Clear Preview")) { preview = null; SceneView.RepaintAll(); }
        }
        if (!string.IsNullOrEmpty(status))
            EditorGUILayout.HelpBox(status, MessageType.None);

        EditorGUILayout.EndScrollView();
    }

    // ------------------------------------------------------------------ actions

    void DoPreview()
    {
        preview = FormationVoxelizer.Bake(source, cubeCost, cubeSize, fillMode, pivotMode);
        var sv = SceneView.lastActiveSceneView;
        previewAnchor = sv != null ? sv.pivot : Vector3.zero;
        status = preview.error;
        SceneView.RepaintAll();
    }

    VOBFormation BakeOne(Object src, bool ping = true)
    {
        var result = FormationVoxelizer.Bake(src, cubeCost, cubeSize, fillMode, pivotMode);
        if (result.slots == null)
        {
            status = $"{src.name}: {result.error}";
            Debug.LogError($"[Formation Baker] {status}", src);
            return null;
        }

        EnsureFolder(outputFolder);
        string fileName = appendCostToName ? $"{src.name}_{cubeCost}" : src.name;
        string path = $"{outputFolder}/{Sanitize(fileName)}.asset";

        var formation = AssetDatabase.LoadAssetAtPath<VOBFormation>(path);
        bool isNew = formation == null;
        if (isNew) formation = CreateInstance<VOBFormation>();
        else Undo.RecordObject(formation, "Re-bake formation");

        Apply(formation, result, src, cubeSize, fillMode, pivotMode);

        if (isNew) AssetDatabase.CreateAsset(formation, path);
        else EditorUtility.SetDirty(formation);
        AssetDatabase.SaveAssets();

        status = $"{(isNew ? "Created" : "Updated")} {path}\n{Describe(result)}";
        if (!string.IsNullOrEmpty(result.error)) status += "\n" + result.error;
        if (ping) EditorGUIUtility.PingObject(formation);
        preview = result;
        var sv = SceneView.lastActiveSceneView;
        previewAnchor = sv != null ? sv.pivot : Vector3.zero;
        SceneView.RepaintAll();
        return formation;
    }

    /// <summary>Writes a bake result and the settings that produced it into a formation asset.</summary>
    public static void Apply(VOBFormation formation, FormationVoxelizer.Result result, Object src, float cubeSize,
        VOBFormation.FillMode fill, VOBFormation.PivotMode pivot)
    {
        formation.slots = result.slots;
        formation.rotations = new Quaternion[0];
        formation.reserveSlots = result.reserve;
        formation.cubeSize = cubeSize;
        formation.surfaceSlots = result.surfaceCount;
        formation.interiorSlots = result.interiorCount;
        formation.duplicateSlots = result.duplicateCount;
        formation.resolution = result.resolution;
        formation.fillMode = fill;
        formation.pivotMode = pivot;

        string srcPath = src != null ? AssetDatabase.GetAssetPath(src) : null;
        formation.sourceGuid = string.IsNullOrEmpty(srcPath) ? "" : AssetDatabase.AssetPathToGUID(srcPath);
    }

    public static Object LoadSource(string guid)
    {
        if (string.IsNullOrEmpty(guid)) return null;
        string path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path)) return null;
        var main = AssetDatabase.LoadMainAssetAtPath(path);
        if (main is GameObject || main is Mesh) return main;
        return AssetDatabase.LoadAssetAtPath<Mesh>(path);
    }

    public static string Describe(FormationVoxelizer.Result r)
    {
        if (r.slots == null) return r.error;
        string s = $"Cost {r.slots.Length}: {r.surfaceCount} on the surface, {r.interiorCount} just inside";
        if (r.duplicateCount > 0) s += $", {r.duplicateCount} stacked (shape is thin for this cost)";
        s += $".\nRoom for {r.reserve.Length} spare cubes inside. Grid {r.resolution} voxels on the longest side.";
        return s;
    }

    // ------------------------------------------------------------------ helpers

    static List<Object> SelectedSources()
    {
        var list = new List<Object>();
        foreach (var o in Selection.objects)
        {
            if (!EditorUtility.IsPersistent(o)) continue; // project assets only
            if (o is GameObject || o is Mesh) list.Add(o);
        }
        return list;
    }

    static VOBYAuthoring FindVOBY()
    {
        var go = Selection.activeGameObject;
        if (go != null)
        {
            var v = go.GetComponentInParent<VOBYAuthoring>();
            if (v != null) return v;
        }
        return Object.FindFirstObjectByType<VOBYAuthoring>();
    }

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string[] parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    static string Sanitize(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name;
    }

    void DrawPreview(SceneView view)
    {
        if (preview == null || preview.slots == null) return;
        const int maxDrawn = 6000;
        var size = Vector3.one * cubeSize;
        Handles.color = new Color(0.3f, 0.9f, 1f, 0.9f);
        // Only the outside: the cubes just under the surface would just clutter the view.
        int visible = preview.surfaceCount > 0 ? preview.surfaceCount : preview.slots.Length;
        int n = Mathf.Min(maxDrawn, visible);
        for (int i = 0; i < n; i++)
            Handles.DrawWireCube(previewAnchor + preview.slots[i], size);
    }
}
