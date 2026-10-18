using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-click setup for the player:
///   NuttyTools > Create Forms From Selected Formations   (select baked formations in the Project window first)
///   NuttyTools > Setup Player Rig                        (adds the Player, wires the bridge, camera and forms)
/// </summary>
public static class PlayerRigSetup
{
    const string FormsFolder = "Assets/_Regeneration X/Gameplay/Forms";

    // ------------------------------------------------------------------ forms from formations

    [MenuItem("NuttyTools/Create Forms From Selected Formations")]
    static void CreateFormsFromSelection()
    {
        var formations = Selection.GetFiltered<VOBFormation>(SelectionMode.Assets);
        if (formations.Length == 0)
        {
            Debug.LogWarning("Select one or more VOB Formation assets in the Project window first (bake them with NuttyTools > VOB Formation Baker).");
            return;
        }

        EnsureFolder(FormsFolder);
        PlayerFormDefinition last = null;
        foreach (var formation in formations)
        {
            string path = AssetDatabase.GenerateUniqueAssetPath($"{FormsFolder}/{formation.name}_Form.asset");
            var form = ScriptableObject.CreateInstance<PlayerFormDefinition>();
            form.formation = formation;
            form.uiColor = ColorFor(formation.name);
            ApplyDefaultsFromName(form, formation.name);
            AssetDatabase.CreateAsset(form, path);
            last = form;
        }
        AssetDatabase.SaveAssets();
        if (last != null) EditorGUIUtility.PingObject(last);
        Debug.Log($"Created {formations.Length} player form(s) in {FormsFolder}. Run NuttyTools > Setup Player Rig to add them to the player.");
    }

    [MenuItem("NuttyTools/Create Forms From Selected Formations", true)]
    static bool CreateFormsValidate() => Selection.GetFiltered<VOBFormation>(SelectionMode.Assets).Length > 0;

    // Rolling shapes get a rolling feel; everything else walks. All of it is tweakable on the asset.
    static void ApplyDefaultsFromName(PlayerFormDefinition form, string name)
    {
        string n = name.ToLowerInvariant();
        if (n.Contains("sphere") || n.Contains("ball") || n.Contains("orb"))
        {
            form.moveStyle = PlayerFormDefinition.MoveStyle.Roll;
            form.moveSpeed = 9f;
            form.acceleration = 14f;
            form.canDodge = false;
            form.description = "Rolls in the direction of travel. Fast, heavy momentum.";
        }
    }

    static Color ColorFor(string name)
    {
        float hue = (Mathf.Abs(name.GetHashCode()) % 360) / 360f;
        return Color.HSVToRGB(hue, 0.55f, 1f);
    }

    // ------------------------------------------------------------------ player rig

    [MenuItem("NuttyTools/Setup Player Rig")]
    static void SetupRig()
    {
        var bridge = Object.FindFirstObjectByType<ECSCommandBridge>();
        if (bridge == null)
        {
            Debug.LogError("No ECSCommandBridge found in the open scene. Add one (or open the scene that has it) and run this again.");
            return;
        }

        // Player
        var player = Object.FindFirstObjectByType<PlayerController>();
        bool created = player == null;
        if (created)
        {
            var go = new GameObject("Player");
            Undo.RegisterCreatedObjectUndo(go, "Create Player");
            var cc = go.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.radius = 0.5f;
            cc.center = new Vector3(0f, 1f, 0f);
            player = go.AddComponent<PlayerController>();

            // Start where the VOBY is, if its authoring is in an open scene / subscene.
            var voby = Object.FindFirstObjectByType<VOBYAuthoring>();
            if (voby != null)
                go.transform.position = new Vector3(voby.transform.position.x, 0f, voby.transform.position.z);
        }

        Undo.RecordObject(player, "Setup Player Rig");
        player.bridge = bridge;
        if (player.aimCamera == null) player.aimCamera = Camera.main;

        // Forms
        if (player.forms == null || player.forms.Length == 0)
        {
            var found = AssetDatabase.FindAssets("t:PlayerFormDefinition")
                .Select(g => AssetDatabase.LoadAssetAtPath<PlayerFormDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(f => f != null && f.formation != null)
                .OrderBy(f => f.Cost)
                .Take(9)
                .ToArray();
            player.forms = found;
        }

        // VOBY: mark as the player's body
        var vobyAuthoring = Object.FindFirstObjectByType<VOBYAuthoring>();
        string vobyNote;
        if (vobyAuthoring != null)
        {
            Undo.RecordObject(vobyAuthoring, "Mark VOBY as player");
            vobyAuthoring.isPlayer = true;
            EditorUtility.SetDirty(vobyAuthoring);
            vobyNote = $"Marked '{vobyAuthoring.name}' as the player's VOBY (save the scene / subscene).";
        }
        else
        {
            vobyNote = "Couldn't find a VOBYAuthoring (is its SubScene open for editing?). Tick 'Is Player' on it by hand.";
        }

        // Camera
        var cam = Camera.main;
        if (cam == null)
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            Undo.RegisterCreatedObjectUndo(camGo, "Create Camera");
            cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
        }
        var follow = cam.GetComponent<TopDownCamera>();
        if (follow == null) follow = Undo.AddComponent<TopDownCamera>(cam.gameObject);
        follow.target = player;
        player.aimCamera = cam;

        EditorUtility.SetDirty(player);
        EditorUtility.SetDirty(follow);
        EditorUtility.SetDirty(bridge);
        Selection.activeGameObject = player.gameObject;

        Debug.Log($"{(created ? "Created" : "Updated")} Player rig: {player.forms.Length} form(s) assigned. {vobyNote}", player);
        if (player.forms.Length == 0)
            Debug.Log("No player forms yet: bake some formations, select them, run NuttyTools > Create Forms From Selected Formations, then run this again.");
    }

    // ------------------------------------------------------------------ helpers

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
}
