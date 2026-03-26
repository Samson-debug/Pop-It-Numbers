using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Wires all Inspector references in the active scene:
///   - GameManager: numbers[0-9], all bubble sprites
///   - UIManager: numberSprites[0-9]
///
/// Run AFTER "PopIt Tools > 1 - Full Number Setup".
/// Open via: PopIt Tools > 2 - Wire Scene References
/// </summary>
public static class SceneWiringTool
{
    private const string NUMBERS_DATA_PATH  = "Assets/ScriptableObjects/Numbers";
    private const string BUBBLES_PATH       = "Assets/Pop It Numbers/Pop bubbles";
    private const string NUMBERS_IMG_PATH   = "Assets/Pop It Numbers/Numbers";

    [MenuItem("PopIt Tools/2 - Wire Scene References", priority = 2)]
    public static void WireSceneReferences()
    {
        int changes = 0;

        // ── GameManager ────────────────────────────────────────────────
        GameManager gm = Object.FindFirstObjectByType<GameManager>();
        if (gm == null)
        {
            Debug.LogWarning("[SceneWiring] GameManager not found in scene.");
        }
        else
        {
            // Numbers 0-9
            var numbers = new LetterData[10];
            for (int n = 0; n <= 9; n++)
            {
                string path = $"{NUMBERS_DATA_PATH}/Number_{n}.asset";
                numbers[n] = AssetDatabase.LoadAssetAtPath<LetterData>(path);
                if (numbers[n] == null)
                    Debug.LogWarning($"[SceneWiring] Missing asset: {path}  — run '1 - Full Number Setup' first.");
            }

            Undo.RecordObject(gm, "Wire GameManager Numbers");
            gm.numbers = numbers;

            // Bubble sprites (unpopped = -1, popped = -2)
            gm.blueUnpopped   = LoadSprite($"{BUBBLES_PATH}/blue-1.png");
            gm.bluePopped     = LoadSprite($"{BUBBLES_PATH}/blue-2.png");
            gm.greenUnpopped  = LoadSprite($"{BUBBLES_PATH}/green-1.png");
            gm.greenPopped    = LoadSprite($"{BUBBLES_PATH}/green-2.png");
            gm.pinkUnpopped   = LoadSprite($"{BUBBLES_PATH}/pink-1.png");
            gm.pinkPopped     = LoadSprite($"{BUBBLES_PATH}/pink-2.png");
            gm.redUnpopped    = LoadSprite($"{BUBBLES_PATH}/red-1.png");
            gm.redPopped      = LoadSprite($"{BUBBLES_PATH}/red-2.png");
            gm.yellowUnpopped = LoadSprite($"{BUBBLES_PATH}/yellow-1.png");
            gm.yellowPopped   = LoadSprite($"{BUBBLES_PATH}/yellow-2.png");

            EditorUtility.SetDirty(gm);
            changes++;
            Debug.Log("[SceneWiring] GameManager: numbers and bubble sprites assigned.");
        }

        // ── UIManager ──────────────────────────────────────────────────
        UIManager ui = Object.FindFirstObjectByType<UIManager>();
        if (ui == null)
        {
            Debug.LogWarning("[SceneWiring] UIManager not found in scene.");
        }
        else
        {
            var sprites = new Sprite[10];
            for (int n = 0; n <= 9; n++)
            {
                sprites[n] = LoadSprite($"{NUMBERS_IMG_PATH}/{n}.png");
                if (sprites[n] == null)
                    Debug.LogWarning($"[SceneWiring] Missing sprite: {NUMBERS_IMG_PATH}/{n}.png");
            }

            Undo.RecordObject(ui, "Wire UIManager Number Sprites");
            ui.numberSprites = sprites;
            EditorUtility.SetDirty(ui);
            changes++;
            Debug.Log("[SceneWiring] UIManager: number sprites assigned.");
        }

        // ── Save scene ─────────────────────────────────────────────────
        if (changes > 0)
        {
            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            EditorUtility.DisplayDialog(
                "Scene Wiring Complete",
                $"{changes} component(s) wired.\n\n" +
                "Remember to save the scene (Ctrl+S / Cmd+S).\n\n" +
                "Still needed manually:\n" +
                "  - LobbyManager > Play Button\n" +
                "  - GameManager > Letter Puzzle Prefab, Bubble Prefab, Letter Puzzle Area, UI Manager\n" +
                "  - GameManager > Jiggle Letter Object (optional)",
                "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Scene Wiring", "Nothing was wired — check warnings in the Console.", "OK");
        }
    }

    private static Sprite LoadSprite(string path)
    {
        EnsureSprite(path);
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null) Debug.LogWarning($"[SceneWiring] Sprite not found: {path}");
        return s;
    }

    private static void EnsureSprite(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;
        bool changed = false;
        if (imp.textureType != TextureImporterType.Sprite)
        { imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single; changed = true; }
        if (imp.isReadable == false) { imp.isReadable = true; changed = true; }
        if (changed) imp.SaveAndReimport();
    }
}
