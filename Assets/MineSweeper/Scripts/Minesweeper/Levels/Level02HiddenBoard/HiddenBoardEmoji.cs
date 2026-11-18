using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;

namespace DesktopMinesweeper
{
    // A tiny level-owned emoji atlas keeps the glyphs aligned and independent of OS emoji fonts.
    internal sealed class HiddenBoardEmoji : IDisposable
    {
        // TMP keeps the schema version in an internal serialized field and only exposes the
        // getter publicly. Stamping the current version keeps its legacy upgrader (which logs
        // and rebuilds the tables from spriteInfoList) from running on this runtime asset.
        private static readonly FieldInfo VersionField =
            typeof(TMP_Asset).GetField("m_Version", BindingFlags.Instance | BindingFlags.NonPublic);

        public TMP_SpriteAsset Asset { get; }
        private readonly Texture2D texture;
        private readonly Material material;

        public HiddenBoardEmoji()
        {
            texture = new Texture2D(64, 32, TextureFormat.RGBA32, false) { name = "Hidden board emoji", filterMode = FilterMode.Bilinear };
            var pixels = new Color32[64 * 32];
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    Color color = Color.clear;
                    if (x >= 8 && x <= 10 && y >= 3 && y <= 29) color = new Color32(82, 88, 97, 255);
                    if (x >= 11 && x <= 27 && y >= 17 + (x - 11) / 5 && y <= 28) color = new Color32(229, 61, 62, 255);
                    pixels[y * 64 + x] = color;
                    color = Color.clear;
                    if (Vector2.Distance(new Vector2(x, y), new Vector2(16, 13)) <= 10) color = new Color32(37, 41, 48, 255);
                    if (Vector2.Distance(new Vector2(x, y), new Vector2(12, 17)) <= 2) color = new Color32(164, 176, 190, 255);
                    if (x >= 19 && x <= 24 && Mathf.Abs(y - (x + 1)) <= 1) color = new Color32(169, 115, 52, 255);
                    if (Vector2.Distance(new Vector2(x, y), new Vector2(25, 28)) <= 3) color = new Color32(255, 192, 46, 255);
                    pixels[y * 64 + x + 32] = color;
                }
            texture.SetPixels32(pixels);
            texture.Apply();
            Asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            Asset.name = "Hidden board emoji";
            Asset.spriteSheet = texture;
            // Declare the current schema before any lookup table is created.
            VersionField?.SetValue(Asset, "1.1.0");
            material = new Material(Shader.Find("TextMeshPro/Sprite") ?? Shader.Find("UI/Default"));
            material.mainTexture = texture;
            Asset.material = material;
            Asset.spriteGlyphTable.Clear();
            Asset.spriteCharacterTable.Clear();
            Add(0, 0x1F6A9, "flag");
            Add(1, 0x1F4A3, "bomb");
            Asset.UpdateLookupTables();
        }

        private void Add(uint index, uint unicode, string name)
        {
            var glyph = new TMP_SpriteGlyph(index, new GlyphMetrics(32, 32, 0, 28, 32), new GlyphRect((int)index * 32, 0, 32, 32), 1, 0);
            Asset.spriteGlyphTable.Add(glyph);
            Asset.spriteCharacterTable.Add(new TMP_SpriteCharacter(unicode, Asset, glyph) { name = name });
        }

        public void Dispose()
        {
            UnityEngine.Object.Destroy(Asset);
            UnityEngine.Object.Destroy(material);
            UnityEngine.Object.Destroy(texture);
        }
    }
}
