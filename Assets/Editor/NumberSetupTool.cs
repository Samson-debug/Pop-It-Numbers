using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-click setup tool for the Pop-It Numbers game.
///
/// Open via: PopIt Tools > 1 - Full Number Setup
///
/// What it does:
///   1. Ensures sprites in Assets/Pop It Numbers/Numbers/ are readable.
///   2. Creates BubbleLayoutData assets for numbers 0-9.
///   3. Creates LetterData (NumberData) ScriptableObjects for numbers 0-9.
///      Each gets its number sprite and a cycling bubble color assigned.
/// </summary>
public static class NumberSetupTool
{
    private const string NUMBERS_SPRITE_PATH = "Assets/Pop It Numbers/Numbers";
    private const string LAYOUTS_PATH        = "Assets/ScriptableObjects/BubbleLayouts";
    private const string NUMBERS_DATA_PATH   = "Assets/ScriptableObjects/Numbers";

    private static readonly float GRID_SPACING    = 0.55f;
    private static readonly float BUBBLE_SIZE     = 0.28f;
    private static readonly float ALPHA_THRESHOLD = 0.45f;

    // Color cycle: 0=Blue, 1=Green, 2=Pink, 3=Red, 4=Yellow  (repeats)
    private static readonly BubbleColor[] COLOR_CYCLE =
    {
        BubbleColor.Blue,
        BubbleColor.Green,
        BubbleColor.Pink,
        BubbleColor.Red,
        BubbleColor.Yellow,
        BubbleColor.Blue,
        BubbleColor.Green,
        BubbleColor.Pink,
        BubbleColor.Red,
        BubbleColor.Yellow,
    };

    [MenuItem("PopIt Tools/1 - Full Number Setup", priority = 1)]
    public static void RunFullSetup()
    {
        EnsureFolder("Assets/ScriptableObjects");
        EnsureFolder(LAYOUTS_PATH);
        EnsureFolder(NUMBERS_DATA_PATH);

        int ok = 0, skip = 0;

        for (int n = 0; n <= 9; n++)
        {
            // ── Sprite ─────────────────────────────────────────────────
            string spritePath = $"{NUMBERS_SPRITE_PATH}/{n}.png";
            EnsureReadableSprite(spritePath);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);

            if (sprite == null)
            {
                Debug.LogWarning($"[NumberSetup] Sprite not found: {spritePath}  — skipping {n}");
                skip++;
                continue;
            }

            // ── Bubble layout ───────────────────────────────────────────
            string layoutPath = $"{LAYOUTS_PATH}/Layout_Number_{n}.asset";
            BubbleLayoutData layout = AssetDatabase.LoadAssetAtPath<BubbleLayoutData>(layoutPath);

            BubbleEntry[] entries = GenerateEntries(sprite, GRID_SPACING, BUBBLE_SIZE, ALPHA_THRESHOLD);

            if (layout == null)
            {
                layout = ScriptableObject.CreateInstance<BubbleLayoutData>();
                layout.bubbles = entries;
                AssetDatabase.CreateAsset(layout, layoutPath);
            }
            else
            {
                layout.bubbles = entries;
                EditorUtility.SetDirty(layout);
            }

            // ── NumberData (LetterData) ─────────────────────────────────
            string dataPath = $"{NUMBERS_DATA_PATH}/Number_{n}.asset";
            LetterData data = AssetDatabase.LoadAssetAtPath<LetterData>(dataPath);

            if (data == null)
            {
                data = ScriptableObject.CreateInstance<LetterData>();
                data.number       = n;
                data.numberSprite = sprite;
                data.bubbleColor  = COLOR_CYCLE[n];
                data.bubbleLayout = layout;
                AssetDatabase.CreateAsset(data, dataPath);
            }
            else
            {
                data.number       = n;
                data.numberSprite = sprite;
                data.bubbleColor  = COLOR_CYCLE[n];
                data.bubbleLayout = layout;
                EditorUtility.SetDirty(data);
            }

            Debug.Log($"[NumberSetup] {n}: {entries.Length} bubbles, color={COLOR_CYCLE[n]} → {dataPath}");
            ok++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"<color=lime>[NumberSetup] Done — {ok} numbers set up, {skip} skipped.</color>");
        EditorUtility.DisplayDialog(
            "Number Setup Complete",
            $"{ok} NumberData assets created/updated in {NUMBERS_DATA_PATH}.\n\n" +
            "Next step: Open the GameManager in the Inspector and assign the 10 Number assets " +
            "(Number_0 … Number_9) to the 'Numbers' array.",
            "OK");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  BUBBLE LAYOUT GENERATION  (same algorithm as BubbleLayoutGenerator)
    // ═══════════════════════════════════════════════════════════════════

    private static BubbleEntry[] GenerateEntries(
        Sprite sprite, float gridSpacing, float bubbleSize, float alphaThreshold)
    {
        Texture2D tex   = sprite.texture;
        float     ppu   = sprite.pixelsPerUnit;
        Rect      rect  = sprite.rect;
        Vector2   pivot = sprite.pivot;

        int erosionPx = Mathf.Max(1, Mathf.RoundToInt(bubbleSize * ppu * 0.48f));

        float wLeft   = -pivot.x / ppu;
        float wRight  =  (rect.width  - pivot.x) / ppu;
        float wBottom = -pivot.y / ppu;
        float wTop    =  (rect.height - pivot.y) / ppu;

        float rangeW = wRight  - wLeft;
        float rangeH = wTop    - wBottom;
        float startX = wLeft   + (rangeW % gridSpacing) * 0.5f + gridSpacing * 0.5f;
        float startY = wBottom + (rangeH % gridSpacing) * 0.5f + gridSpacing * 0.5f;

        var offsets = new (int dx, int dy)[]
        {
            ( erosionPx,          0), (-erosionPx,          0),
            (         0,  erosionPx), (         0, -erosionPx),
            ( erosionPx,  erosionPx), (-erosionPx,  erosionPx),
            ( erosionPx, -erosionPx), (-erosionPx, -erosionPx),
        };

        var entries = new List<BubbleEntry>();

        for (float wy = startY; wy < wTop; wy += gridSpacing)
        {
            for (float wx = startX; wx < wRight; wx += gridSpacing)
            {
                int cx = Mathf.RoundToInt(rect.x + pivot.x + wx * ppu);
                int cy = Mathf.RoundToInt(rect.y + pivot.y + wy * ppu);

                if (cx < (int)rect.x || cx >= (int)(rect.x + rect.width))  continue;
                if (cy < (int)rect.y || cy >= (int)(rect.y + rect.height)) continue;

                if (tex.GetPixel(cx, cy).a < alphaThreshold) continue;

                bool inside = true;
                foreach (var (dx, dy) in offsets)
                {
                    int sx = cx + dx;
                    int sy = cy + dy;

                    if (sx < (int)rect.x || sx >= (int)(rect.x + rect.width) ||
                        sy < (int)rect.y || sy >= (int)(rect.y + rect.height))
                    {
                        inside = false;
                        break;
                    }

                    if (tex.GetPixel(sx, sy).a < alphaThreshold)
                    {
                        inside = false;
                        break;
                    }
                }

                if (inside)
                    entries.Add(new BubbleEntry { position = new Vector2(wx, wy), size = bubbleSize });
            }
        }

        return entries.ToArray();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════════════

    private static void EnsureReadableSprite(string assetPath)
    {
        var imp = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (imp == null) return;

        bool changed = false;
        if (imp.textureType != TextureImporterType.Sprite)
        {
            imp.textureType      = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            changed = true;
        }
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
