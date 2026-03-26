using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor window that auto-generates BubbleLayoutData assets by scanning
/// each number sprite's alpha channel on a world-space grid.
///
/// A bubble is placed wherever the sampled pixel is opaque (alpha >= threshold).
/// World coordinates are relative to the sprite's pivot point.
///
/// Open via: PopIt Tools > 4 - Generate Bubble Layouts From Sprites
/// </summary>
public class BubbleLayoutGeneratorWindow : EditorWindow
{
    private float _gridSpacing    = 0.55f;
    private float _bubbleSize     = 0.28f;
    private float _alphaThreshold = 0.45f;

    private int       _prevIdx   = 0;    // 0-9
    private Texture2D _prevTex;
    private int       _prevCount = -1;

    private Vector2 _scroll;

    [MenuItem("PopIt Tools/4 - Generate Bubble Layouts From Sprites", priority = 4)]
    public static void OpenWindow() =>
        GetWindow<BubbleLayoutGeneratorWindow>("Bubble Layout Gen").Show();

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Bubble Layout Generator", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Scans each number sprite's alpha channel on a world-space grid.\n" +
            "Erosion check: a bubble is placed only when its centre AND all 8 surrounding\n" +
            "sample points (at bubble-radius distance) are fully inside the number.\n\n" +
            "Recommended settings for ~10-14 bubbles per number:\n" +
            "  Grid Spacing    0.50 - 0.60\n" +
            "  Bubble Size     0.24 - 0.32\n" +
            "  Alpha Threshold 0.40 - 0.55\n\n" +
            "Use Refresh Preview to check bubble count before generating all.",
            MessageType.Info);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Grid Settings", EditorStyles.boldLabel);

        _gridSpacing    = EditorGUILayout.Slider("Grid Spacing (world units)",    _gridSpacing,    0.20f, 1.20f);
        _bubbleSize     = EditorGUILayout.Slider("Bubble Size (world units)",     _bubbleSize,     0.15f, 1.10f);
        _alphaThreshold = EditorGUILayout.Slider("Alpha Threshold (0 - 1)",       _alphaThreshold, 0.05f, 0.95f);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Preview Single Number", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        _prevIdx = EditorGUILayout.IntSlider(_prevIdx, 0, 9);
        EditorGUILayout.LabelField(
            _prevIdx.ToString(),
            new GUIStyle(EditorStyles.boldLabel) { fontSize = 22 },
            GUILayout.Width(30));
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Refresh Preview"))
            RefreshPreview();

        if (_prevCount >= 0)
            EditorGUILayout.HelpBox($"Bubble count for '{_prevIdx}': {_prevCount}", MessageType.None);

        if (_prevTex != null)
        {
            float size = Mathf.Min(EditorGUIUtility.currentViewWidth - 20f, 420f);
            Rect r = GUILayoutUtility.GetRect(size, size);
            EditorGUI.DrawPreviewTexture(r, _prevTex, null, ScaleMode.ScaleToFit);
        }

        EditorGUILayout.Space(10);
        var oldBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.35f, 0.85f, 0.35f);
        if (GUILayout.Button("▶  Generate All Layouts (0-9)", GUILayout.Height(38)))
            GenerateAll();
        GUI.backgroundColor = oldBg;

        EditorGUILayout.EndScrollView();
    }

    private void OnDestroy()
    {
        if (_prevTex != null) DestroyImmediate(_prevTex);
    }

    private void RefreshPreview()
    {
        string path = $"Assets/Pop It Numbers/Numbers/{_prevIdx}.png";

        EnsureReadableSprite(path);
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            Debug.LogWarning($"[BubbleGen] Sprite not found: {path}");
            return;
        }

        BubbleEntry[] entries = GenerateEntries(sprite, _gridSpacing, _bubbleSize, _alphaThreshold);
        _prevCount = entries.Length;

        if (_prevTex != null) DestroyImmediate(_prevTex);
        _prevTex = BuildPreviewTexture(sprite, entries);
        Repaint();
    }

    private void GenerateAll()
    {
        EnsureFolder("Assets/ScriptableObjects");
        EnsureFolder("Assets/ScriptableObjects/BubbleLayouts");

        int ok = 0, skip = 0;

        for (int i = 0; i <= 9; i++)
        {
            if (ProcessNumber(i)) ok++;
            else                  skip++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"<color=lime>[BubbleGen] Done — {ok} layouts written, {skip} skipped.</color>");
    }

    private bool ProcessNumber(int n)
    {
        string sPath = $"Assets/Pop It Numbers/Numbers/{n}.png";

        EnsureReadableSprite(sPath);
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(sPath);
        if (sprite == null)
        {
            Debug.LogWarning($"[BubbleGen] Sprite not found: {sPath}");
            return false;
        }

        BubbleEntry[] entries = GenerateEntries(sprite, _gridSpacing, _bubbleSize, _alphaThreshold);
        if (entries.Length == 0)
        {
            Debug.LogWarning($"[BubbleGen] '{n}': no opaque pixels found (threshold={_alphaThreshold:F2}). Skipping.");
            return false;
        }

        string aPath = $"Assets/ScriptableObjects/BubbleLayouts/Layout_Number_{n}.asset";

        var existing = AssetDatabase.LoadAssetAtPath<BubbleLayoutData>(aPath);
        if (existing == null)
        {
            var asset = ScriptableObject.CreateInstance<BubbleLayoutData>();
            asset.bubbles = entries;
            AssetDatabase.CreateAsset(asset, aPath);
        }
        else
        {
            existing.bubbles = entries;
            EditorUtility.SetDirty(existing);
        }

        Debug.Log($"[BubbleGen] '{n}': {entries.Length} bubbles → {aPath}");
        return true;
    }

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
                    entries.Add(new BubbleEntry
                    {
                        position = new Vector2(wx, wy),
                        size     = bubbleSize
                    });
            }
        }

        return entries.ToArray();
    }

    private static Texture2D BuildPreviewTexture(Sprite sprite, BubbleEntry[] entries)
    {
        Texture2D src   = sprite.texture;
        Rect      rect  = sprite.rect;
        float     ppu   = sprite.pixelsPerUnit;
        Vector2   pivot = sprite.pivot;

        int w = (int)rect.width;
        int h = (int)rect.height;

        Texture2D dst = new Texture2D(w, h, TextureFormat.RGBA32, false);
        dst.filterMode = FilterMode.Bilinear;

        Color[] pixels = src.GetPixels((int)rect.x, (int)rect.y, w, h);
        dst.SetPixels(pixels);

        Color fillColor = new Color(1f, 0.25f, 0.1f, 0.70f);
        Color rimColor  = new Color(1f, 0.95f, 0f,  1f);

        foreach (var e in entries)
        {
            int cx = Mathf.RoundToInt(pivot.x + e.position.x * ppu);
            int cy = Mathf.RoundToInt(pivot.y + e.position.y * ppu);
            int r  = Mathf.Max(3, Mathf.RoundToInt(e.size * ppu * 0.46f));
            int r2 = r * r;
            int ri = r - 2;
            int ri2 = ri * ri;

            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                int dist2 = dx * dx + dy * dy;
                if (dist2 > r2) continue;

                int ox = cx + dx;
                int oy = cy + dy;
                if (ox < 0 || ox >= w || oy < 0 || oy >= h) continue;

                bool isRim = dist2 > ri2;
                Color c = isRim ? rimColor : fillColor;

                Color bg = dst.GetPixel(ox, oy);
                float a  = c.a;
                dst.SetPixel(ox, oy,
                    new Color(
                        bg.r * (1 - a) + c.r * a,
                        bg.g * (1 - a) + c.g * a,
                        bg.b * (1 - a) + c.b * a,
                        Mathf.Max(bg.a, a)));
            }
        }

        dst.Apply();
        return dst;
    }

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

        if (!imp.isReadable)
        {
            imp.isReadable = true;
            changed = true;
        }

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
