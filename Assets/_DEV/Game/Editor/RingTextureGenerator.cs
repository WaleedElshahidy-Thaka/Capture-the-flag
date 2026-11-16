using System.IO;
using UnityEditor;
using UnityEngine;

// Game > Create Ring Texture: writes a white, soft-edged glowing ring PNG with a transparent
// background and sets its import settings, ready for the ring quad under the cars.
// White on purpose - SeatColorTint multiplies it by the seat colour.
static class RingTextureGenerator
{
    const string Path = "Assets/_DEV/Game/Player/Resources/Ring.png";
    const int Size = 512;

    // Ring shape, as fractions of the texture's radius.
    const float RingRadius = 0.78f;   // centre line of the ring
    const float RingWidth = 0.02f;    // solid band, half-width - the line's thickness (was 0.07)
    const float GlowWidth = 0.08f;    // soft glow beyond the band, each side (was 0.16)

    [MenuItem("Game/Create Ring Texture")]
    static void Create()
    {
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        float half = Size * 0.5f;

        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            float dx = (x + 0.5f - half) / half;
            float dy = (y + 0.5f - half) / half;
            float r = Mathf.Sqrt(dx * dx + dy * dy);       // 0 at centre, 1 at the edge
            float d = Mathf.Abs(r - RingRadius);            // distance from the ring's centre line

            float alpha;
            if (d <= RingWidth) alpha = 1f;                                    // solid band
            else alpha = Mathf.Pow(1f - Mathf.Clamp01((d - RingWidth) / GlowWidth), 2f); // glow falloff

            texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }

        texture.Apply();
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
        File.WriteAllBytes(Path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(Path);

        var importer = (TextureImporter)AssetImporter.GetAtPath(Path);
        importer.textureType = TextureImporterType.Default;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = true;
        importer.SaveAndReimport();

        Selection.activeObject = AssetDatabase.LoadAssetAtPath<Texture2D>(Path);
        Debug.Log($"[RingTextureGenerator] Created {Path}.");
    }
}
