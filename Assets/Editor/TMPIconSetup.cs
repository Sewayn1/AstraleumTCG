using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using TMPro;

namespace Astraleum.Editor
{
    public static class TMPIconSetup
    {
        const string IconsDir  = "Assets/Game/UI/Sprites/Icons";
        const string AtlasPath = "Assets/Resources/TMP_Icons/AstralanIconsSheet.png";
        const string AssetPath = "Assets/Resources/TMP_Icons/AstralanIcons.asset";

        [MenuItem("Tools/Astraleum/Generate TMP Icons")]
        public static void Generate()
        {
            var icons = BuildIcons();
            if (icons.Count == 0) { Debug.LogError("[TMPIconSetup] No icons loaded."); return; }

            var atlas = PackAtlas(icons, out int cellSize);
            SavePNG(AtlasPath, atlas);
            AssetDatabase.Refresh();
            ConfigureAtlas(AtlasPath);
            AssetDatabase.Refresh();

            PopulateSpriteAsset(icons, cellSize);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[TMPIconSetup] Done — {icons.Count} icons ({cellSize}px cell) in atlas.");
        }

        // ── Icon list — charge les PNG existants du projet ────────────────────

        static List<(string name, Texture2D tex)> BuildIcons() => new()
        {
            ("dgt",  Load("Icon_Attack")),
            ("pv",   Load("Icon_HP")),
            ("heal", Load("Icon_Heal")),
            ("arm",  Load("Icon_Armor")),
            ("burn",  Load("Icon_Burn")),
        };

        static Texture2D Load(string fileName)
        {
            string path = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", $"{IconsDir}/{fileName}.png"));
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (File.Exists(path))
                tex.LoadImage(File.ReadAllBytes(path));
            else
                Debug.LogWarning($"[TMPIconSetup] Not found: {IconsDir}/{fileName}.png");
            return tex;
        }

        // ── Atlas — taille de cellule = max des dimensions chargées ───────────

        const int MaxCellSize = 128;

        static Texture2D PackAtlas(List<(string name, Texture2D tex)> icons, out int cellSize)
        {
            cellSize = 32;
            foreach (var (_, t) in icons)
                cellSize = Mathf.Max(cellSize, t.width, t.height);
            cellSize = Mathf.Min(cellSize, MaxCellSize);

            int n = icons.Count;
            var atlas = new Texture2D(n * cellSize, cellSize, TextureFormat.RGBA32, false);
            atlas.SetPixels32(new Color32[n * cellSize * cellSize]);

            for (int i = 0; i < n; i++)
                BlitScaled(icons[i].tex, atlas, i * cellSize, 0, cellSize, cellSize);

            atlas.Apply();
            return atlas;
        }

        static void BlitScaled(Texture2D src, Texture2D dst, int dstX, int dstY, int w, int h)
        {
            for (int px = 0; px < w; px++)
            for (int py = 0; py < h; py++)
            {
                float u = (float)px / (w - 1);
                float v = (float)py / (h - 1);
                float sx = u * (src.width  - 1);
                float sy = v * (src.height - 1);
                int x0 = (int)sx, y0 = (int)sy;
                int x1 = Mathf.Min(x0 + 1, src.width  - 1);
                int y1 = Mathf.Min(y0 + 1, src.height - 1);
                float tx = sx - x0, ty = sy - y0;
                Color c = Color.Lerp(
                    Color.Lerp(src.GetPixel(x0, y0), src.GetPixel(x1, y0), tx),
                    Color.Lerp(src.GetPixel(x0, y1), src.GetPixel(x1, y1), tx), ty);
                dst.SetPixel(dstX + px, dstY + py, c);
            }
        }

        static void SavePNG(string assetPath, Texture2D tex)
        {
            string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
            File.WriteAllBytes(fullPath, tex.EncodeToPNG());
        }

        static void ConfigureAtlas(string assetPath)
        {
            var imp = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (imp == null) return;
            imp.textureType         = TextureImporterType.Default;
            imp.alphaIsTransparency = true;
            imp.filterMode          = FilterMode.Bilinear;
            imp.mipmapEnabled       = false;
            imp.npotScale           = TextureImporterNPOTScale.None;
            imp.SaveAndReimport();
        }

        // ── Sprite Asset ──────────────────────────────────────────────────────

        static void PopulateSpriteAsset(List<(string name, Texture2D _)> icons, int cellSize)
        {
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
            if (atlas == null) { Debug.LogError("[TMPIconSetup] Atlas not found."); return; }

            var sa = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(AssetPath);
            if (sa == null)
            {
                sa = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
                AssetDatabase.CreateAsset(sa, AssetPath);
            }

            sa.spriteSheet = atlas;
            sa.spriteGlyphTable.Clear();
            sa.spriteCharacterTable.Clear();

            float bearingY = cellSize * 0.875f;
            uint unicode = 0xE000;

            for (int i = 0; i < icons.Count; i++)
            {
                var glyph = new TMP_SpriteGlyph
                {
                    index     = (uint)i,
                    metrics   = new UnityEngine.TextCore.GlyphMetrics(cellSize, cellSize, 0, bearingY, cellSize),
                    glyphRect = new UnityEngine.TextCore.GlyphRect(i * cellSize, 0, cellSize, cellSize),
                    scale     = 1f,
                };

                var ch = new TMP_SpriteCharacter((uint)unicode++, sa, glyph)
                {
                    name       = icons[i].name,
                    glyphIndex = (uint)i,
                    scale      = 1f,
                };

                sa.spriteGlyphTable.Add(glyph);
                sa.spriteCharacterTable.Add(ch);
            }

            sa.UpdateLookupTables();

            // Material avec shader TMP Sprite et atlas comme texture
            string matPath = "Assets/Resources/TMP_Icons/AstralanIconsMat.mat";
            var spriteMat  = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            var shader     = Shader.Find("TextMeshPro/Sprite") ?? Shader.Find("Hidden/TextMeshPro/Sprite");
            if (shader != null)
            {
                if (spriteMat == null)
                {
                    spriteMat = new Material(shader);
                    AssetDatabase.CreateAsset(spriteMat, matPath);
                }
                spriteMat.shader      = shader;
                spriteMat.mainTexture = atlas;
                EditorUtility.SetDirty(spriteMat);
                sa.material = spriteMat;
            }

            EditorUtility.SetDirty(sa);

            // Sprite Asset global par défaut TMP
            var settings = TMP_Settings.instance;
            if (settings != null)
            {
                var so   = new SerializedObject(settings);
                var prop = so.FindProperty("m_defaultSpriteAsset");
                if (prop != null)
                {
                    prop.objectReferenceValue = sa;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(settings);
                }
            }
        }
    }
}
