using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Scene-based Bubble Layout Editor for numbers 0-9.
///
/// Workflow:
///   1. Pick a number (0-9).
///   2. Click "Spawn in Scene" — the number sprite appears with existing bubbles.
///   3. Move / add / delete bubbles freely in the Scene view.
///   4. Click "Save Layout from Scene" — positions are written to the
///      BubbleLayoutData ScriptableObject for that number.
///   5. Click "Clear Scene" when done.
///
/// Open via: PopIt Tools > 5 - Edit Bubble Layout in Scene
/// </summary>
public class BubbleLayoutSceneEditor : EditorWindow
{
    private int   _numberIdx  = 0;    // 0-9
    private float _bubbleSize = 0.60f;

    private GameObject _editRoot;
    private Transform  _bubblesContainer;

    private Vector2 _scroll;

    [MenuItem("PopIt Tools/5 - Edit Bubble Layout in Scene", priority = 5)]
    public static void OpenWindow() =>
        GetWindow<BubbleLayoutSceneEditor>("Bubble Layout Editor").Show();

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Bubble Layout Scene Editor", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "1. Pick a number and click Spawn in Scene.\n" +
            "2. Select, move or delete bubbles in the Scene view (Transform gizmos).\n" +
            "3. Use Add Bubble to place a new one at the scene origin.\n" +
            "4. Click Save Layout — writes positions to the BubbleLayoutData asset.\n" +
            "5. Click Clear Scene when finished.",
            MessageType.Info);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Number", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        _numberIdx = EditorGUILayout.IntSlider(_numberIdx, 0, 9);
        EditorGUILayout.LabelField(
            _numberIdx.ToString(),
            new GUIStyle(EditorStyles.boldLabel) { fontSize = 24 },
            GUILayout.Width(32));
        EditorGUILayout.EndHorizontal();

        _bubbleSize = EditorGUILayout.Slider(
            new GUIContent("Default Bubble Size", "Scale applied to new bubbles added with 'Add Bubble'."),
            _bubbleSize, 0.20f, 1.10f);

        EditorGUILayout.Space(6);
        GUI.backgroundColor = new Color(0.5f, 0.75f, 1f);
        if (GUILayout.Button($"Spawn  '{_numberIdx}'  in Scene", GUILayout.Height(32)))
            SpawnForEditing();
        GUI.backgroundColor = Color.white;

        if (_editRoot != null)
        {
            SyncContainerRef();
            int count = _bubblesContainer != null ? _bubblesContainer.childCount : 0;

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Active Edit Session", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                $"Editing: '{_numberIdx}'   |   Bubbles in scene: {count}\n" +
                "Move bubbles with the standard Transform gizmo.\n" +
                "Delete a bubble: select it → press Delete.",
                count > 0 ? MessageType.Info : MessageType.Warning);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Quick-Jump (0-9)", EditorStyles.miniLabel);
            DrawNumberGrid();

            EditorGUILayout.Space(6);

            if (GUILayout.Button("＋  Add Bubble at Origin"))
                AddBubble();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select All Bubbles"))
                SelectAllBubbles();
            if (GUILayout.Button("Frame in Scene View"))
                FrameEditRoot();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            GUI.backgroundColor = new Color(0.3f, 0.88f, 0.3f);
            if (GUILayout.Button($"  Save Layout from Scene  ({count} bubbles)", GUILayout.Height(36)))
                SaveLayout();
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(4);
            GUI.backgroundColor = new Color(1f, 0.55f, 0.4f);
            if (GUILayout.Button("Clear Scene (remove edit objects)"))
                ClearScene();
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.EndScrollView();

        if (_editRoot != null) Repaint();
    }

    private void DrawNumberGrid()
    {
        EditorGUILayout.BeginHorizontal();
        for (int i = 0; i <= 9; i++)
        {
            bool active = i == _numberIdx;
            GUI.backgroundColor = active ? new Color(0.4f, 0.8f, 1f) : Color.white;
            if (GUILayout.Button(i.ToString(), GUILayout.Width(32), GUILayout.Height(22)))
            {
                _numberIdx = i;
                SpawnForEditing();
            }
        }
        EditorGUILayout.EndHorizontal();
        GUI.backgroundColor = Color.white;
    }

    private void SpawnForEditing()
    {
        ClearScene(silent: true);

        string spritePath = $"Assets/Pop It Numbers/Numbers/{_numberIdx}.png";
        EnsureReadable(spritePath);
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);

        string layoutPath = LayoutAssetPath(_numberIdx);
        var    layout     = AssetDatabase.LoadAssetAtPath<BubbleLayoutData>(layoutPath);

        var bubblePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Bubble.prefab");

        _editRoot = new GameObject($"[EDIT]  {_numberIdx}");
        Undo.RegisterCreatedObjectUndo(_editRoot, $"Spawn Edit {_numberIdx}");

        var bgGO = new GameObject("NumberBackground");
        bgGO.transform.SetParent(_editRoot.transform, false);
        bgGO.transform.localPosition = Vector3.zero;
        var sr = bgGO.AddComponent<SpriteRenderer>();
        sr.sprite       = sprite;
        sr.sortingOrder = 1;
        bgGO.hideFlags  = HideFlags.NotEditable;

        var containerGO = new GameObject("BubblesContainer");
        containerGO.transform.SetParent(_editRoot.transform, false);
        _bubblesContainer = containerGO.transform;

        if (layout != null && layout.bubbles != null)
        {
            foreach (var entry in layout.bubbles)
                SpawnBubbleGO(entry.position, entry.size, bubblePrefab);
        }

        Selection.activeGameObject = _editRoot;
        FrameEditRoot();
        Repaint();

        Debug.Log($"[BubbleEditor] Spawned '{_numberIdx}' with {_bubblesContainer.childCount} bubbles.");
    }

    private GameObject SpawnBubbleGO(Vector2 pos, float size, GameObject prefab)
    {
        GameObject go;
        if (prefab != null)
            go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _bubblesContainer);
        else
        {
            go = new GameObject("Bubble");
            go.transform.SetParent(_bubblesContainer, false);
            var sr          = go.AddComponent<SpriteRenderer>();
            sr.color        = new Color(0.3f, 0.7f, 1f, 0.8f);
            sr.sortingOrder = 2;
        }
        go.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
        go.transform.localScale    = Vector3.one * size;
        return go;
    }

    private void AddBubble()
    {
        if (_bubblesContainer == null) { Debug.LogWarning("[BubbleEditor] No active edit session."); return; }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Bubble.prefab");
        var go     = SpawnBubbleGO(Vector2.zero, _bubbleSize, prefab);
        Undo.RegisterCreatedObjectUndo(go, "Add Bubble");

        Selection.activeGameObject = go;
        SceneView.FrameLastActiveSceneView();
        Repaint();
    }

    private void SaveLayout()
    {
        SyncContainerRef();
        if (_bubblesContainer == null) { Debug.LogWarning("[BubbleEditor] No BubblesContainer found."); return; }

        var entries = new List<BubbleEntry>();

        foreach (Transform child in _bubblesContainer)
        {
            entries.Add(new BubbleEntry
            {
                position = new Vector2(child.localPosition.x, child.localPosition.y),
                size     = child.localScale.x
            });
        }

        if (entries.Count == 0)
        {
            if (!EditorUtility.DisplayDialog("Save Empty Layout",
                $"No bubbles in scene for '{_numberIdx}'. Save empty layout?",
                "Yes", "Cancel"))
                return;
        }

        EnsureFolder("Assets/ScriptableObjects");
        EnsureFolder("Assets/ScriptableObjects/BubbleLayouts");

        string path     = LayoutAssetPath(_numberIdx);
        var    existing = AssetDatabase.LoadAssetAtPath<BubbleLayoutData>(path);

        if (existing == null)
        {
            var asset = ScriptableObject.CreateInstance<BubbleLayoutData>();
            asset.bubbles = entries.ToArray();
            AssetDatabase.CreateAsset(asset, path);
        }
        else
        {
            Undo.RecordObject(existing, $"Save Bubble Layout {_numberIdx}");
            existing.bubbles = entries.ToArray();
            EditorUtility.SetDirty(existing);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"<color=lime>[BubbleEditor] Saved {entries.Count} bubbles for '{_numberIdx}' → {path}</color>");
    }

    private void ClearScene(bool silent = false)
    {
        if (_editRoot != null)
        {
            Undo.DestroyObjectImmediate(_editRoot);
            _editRoot         = null;
            _bubblesContainer = null;
        }
        else
        {
            foreach (var go in FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (go != null && go.name.StartsWith("[EDIT]") && go.transform.parent == null)
                    Undo.DestroyObjectImmediate(go);
            }
        }
        if (!silent) Repaint();
    }

    private string LayoutAssetPath(int n) =>
        $"Assets/ScriptableObjects/BubbleLayouts/Layout_Number_{n}.asset";

    private void SyncContainerRef()
    {
        if (_editRoot == null) return;
        if (_bubblesContainer != null) return;
        var t = _editRoot.transform.Find("BubblesContainer");
        if (t != null) _bubblesContainer = t;
    }

    private void SelectAllBubbles()
    {
        SyncContainerRef();
        if (_bubblesContainer == null) return;
        var gos = new List<GameObject>();
        foreach (Transform child in _bubblesContainer)
            gos.Add(child.gameObject);
        Selection.objects = gos.ToArray();
    }

    private void FrameEditRoot()
    {
        if (_editRoot == null) return;
        Selection.activeGameObject = _editRoot;
        SceneView.FrameLastActiveSceneView();
    }

    private static void EnsureReadable(string assetPath)
    {
        var imp = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (imp == null) return;
        bool changed = false;
        if (imp.textureType != TextureImporterType.Sprite)
        { imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single; changed = true; }
        if (!imp.isReadable) { imp.isReadable = true; changed = true; }
        if (changed) imp.SaveAndReimport();
    }

    private static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(
                Path.GetDirectoryName(path)?.Replace('\\', '/'),
                Path.GetFileName(path));
    }
}
