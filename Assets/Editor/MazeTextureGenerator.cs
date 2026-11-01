using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Makes tileable textures for the Mazura maze (mossy stone-block wall, leafy forest floor),
/// with normal maps, and two URP Lit materials that use them.
/// Menu: Tools > Mazura > Generate Maze Textures. Run it again any time to remake them.
/// Wall texture covers 3 x 3 metres, floor texture covers 4 x 4 metres (see MazeGenerator tile sizes).
/// </summary>
public static class MazeTextureGenerator
{
    const int Size = 512;
    const string Folder = "Assets/Textures/Maze";

    [MenuItem("Tools/Mazura/Generate Maze Textures")]
    public static void Generate()
    {
        if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);

        Color[] col = new Color[Size * Size];
        float[] hei = new float[Size * Size];

        BuildWall(col, hei);
        Save("Maze_Wall_Albedo", col, false);
        Save("Maze_Wall_Normal", ToNormal(hei, 3.0f), true);

        BuildFloor(col, hei);
        Save("Maze_Floor_Albedo", col, false);
        Save("Maze_Floor_Normal", ToNormal(hei, 2.2f), true);

        MakeMaterial("Maze_Wall", "Maze_Wall_Albedo", "Maze_Wall_Normal", 0.12f);
        MakeMaterial("Maze_Floor", "Maze_Floor_Albedo", "Maze_Floor_Normal", 0.08f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("MazeTextureGenerator: textures and materials saved in " + Folder);
    }

    // ---------------------------------------------------------------- wall: mossy stone blocks

    static void BuildWall(Color[] col, float[] hei)
    {
        const int rows = 6;     // 6 rows of blocks, each 0.5 m high
        const int cols = 3;     // 3 blocks per row, each 1 m wide
        Color stoneDark = new Color(0.20f, 0.22f, 0.20f);
        Color stoneLight = new Color(0.36f, 0.36f, 0.32f);
        Color mortar = new Color(0.06f, 0.06f, 0.05f);
        Color moss = new Color(0.12f, 0.26f, 0.08f);

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float u = x / (float)Size, v = y / (float)Size;
                float py = v * rows;
                int row = Mathf.FloorToInt(py);
                float fy = py - row;
                float px = u * cols + ((row % 2 == 1) ? 0.5f : 0f);
                px = px - Mathf.Floor(px / cols) * cols;
                int bc = Mathf.FloorToInt(px);
                float fx = px - bc;

                float dx = Mathf.Min(fx, 1f - fx) * (Size / (float)cols);
                float dy = Mathf.Min(fy, 1f - fy) * (Size / (float)rows);
                float d = Mathf.Min(dx, dy);

                float block = Hash(bc, row, 11);
                float grain = Fbm(u, v, 24, 3, 5);
                float bevel = Mathf.SmoothStep(0f, 1f, d / 16f);

                Color c = Color.Lerp(stoneDark, stoneLight, block) * (0.8f + 0.4f * grain);
                float h = bevel * (0.55f + 0.35f * block) + grain * 0.12f;

                if (d < 4f) { c = mortar * (0.8f + 0.4f * grain); h = 0f; }

                // moss grows more in the cracks between blocks
                float mossN = Fbm(u, v, 6, 4, 31);
                float crack = 1f - Mathf.SmoothStep(0f, 22f, d);
                float m = Mathf.SmoothStep(0.50f, 0.68f, mossN + crack * 0.12f) * 0.9f;
                c = Color.Lerp(c, moss * (0.7f + 0.6f * Fbm(u, v, 40, 2, 9)), m);
                h += m * 0.08f;

                c.a = 1f;
                col[y * Size + x] = c;
                hei[y * Size + x] = h;
            }
        }
    }

    // ---------------------------------------------------------------- floor: dirt, moss, leaves, pebbles

    static void BuildFloor(Color[] col, float[] hei)
    {
        Color dirtDark = new Color(0.10f, 0.07f, 0.045f);
        Color dirtLight = new Color(0.21f, 0.15f, 0.09f);
        Color moss = new Color(0.10f, 0.21f, 0.06f);

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float u = x / (float)Size, v = y / (float)Size;
                float n1 = Fbm(u, v, 4, 5, 3);
                float n2 = Fbm(u, v, 48, 2, 8);
                Color c = Color.Lerp(dirtDark, dirtLight, n1) * (0.8f + 0.4f * n2);
                float h = n1 * 0.35f + n2 * 0.15f;

                float g = Mathf.SmoothStep(0.55f, 0.72f, Fbm(u, v, 5, 4, 21));
                c = Color.Lerp(c, moss * (0.7f + 0.7f * Fbm(u, v, 32, 3, 13)), g * 0.85f);
                h += g * 0.1f;

                c.a = 1f;
                col[y * Size + x] = c;
                hei[y * Size + x] = h;
            }
        }

        System.Random rnd = new System.Random(777);
        Color[] leafColors =
        {
            new Color(0.36f, 0.17f, 0.05f), new Color(0.28f, 0.22f, 0.06f),
            new Color(0.18f, 0.11f, 0.04f), new Color(0.40f, 0.26f, 0.07f)
        };

        for (int i = 0; i < 170; i++)   // fallen leaves
        {
            Blob(col, hei, (float)rnd.NextDouble() * Size, (float)rnd.NextDouble() * Size,
                 3f + (float)rnd.NextDouble() * 5f, 1.6f + (float)rnd.NextDouble() * 2f,
                 (float)rnd.NextDouble() * Mathf.PI, leafColors[rnd.Next(leafColors.Length)], 0.06f);
        }
        for (int i = 0; i < 46; i++)    // pebbles
        {
            float grey = 0.24f + (float)rnd.NextDouble() * 0.2f;
            float r = 4f + (float)rnd.NextDouble() * 6f;
            Blob(col, hei, (float)rnd.NextDouble() * Size, (float)rnd.NextDouble() * Size,
                 r, r * (0.7f + (float)rnd.NextDouble() * 0.3f), (float)rnd.NextDouble() * Mathf.PI,
                 new Color(grey, grey, grey * 0.95f), 0.35f);
        }
    }

    // Draws a soft ellipse that wraps around the texture edges (so the texture still tiles).
    static void Blob(Color[] col, float[] hei, float cx, float cy, float rx, float ry, float ang, Color c, float height)
    {
        float cos = Mathf.Cos(ang), sin = Mathf.Sin(ang);
        int ext = Mathf.CeilToInt(Mathf.Max(rx, ry)) + 1;
        for (int oy = -ext; oy <= ext; oy++)
        {
            for (int ox = -ext; ox <= ext; ox++)
            {
                float lx = ox * cos + oy * sin;
                float ly = -ox * sin + oy * cos;
                float t = (lx * lx) / (rx * rx) + (ly * ly) / (ry * ry);
                if (t > 1f) continue;
                int px = (((int)cx + ox) % Size + Size) % Size;
                int py = (((int)cy + oy) % Size + Size) % Size;
                float edge = Mathf.SmoothStep(0f, 1f, (1f - t) * 3f);
                float shade = 0.85f + 0.3f * (1f - t);
                int idx = py * Size + px;
                col[idx] = Color.Lerp(col[idx], c * shade, edge);
                hei[idx] = Mathf.Max(hei[idx], hei[idx] * (1f - edge) + height * edge * (1f - t * 0.6f));
            }
        }
    }

    // ---------------------------------------------------------------- noise (tileable)

    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            int n = x * 374761393 + y * 668265263 + seed * 1442695041;
            n = (n ^ (n >> 13)) * 1274126177;
            n ^= n >> 16;
            return (n & 0x7fffffff) / (float)0x7fffffff;
        }
    }

    static float Noise(float u, float v, int freq, int seed)
    {
        float x = u * freq, y = v * freq;
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        int x1 = (x0 + 1) % freq, y1 = (y0 + 1) % freq;
        x0 %= freq; y0 %= freq;
        float a = Hash(x0, y0, seed), b = Hash(x1, y0, seed);
        float c = Hash(x0, y1, seed), d = Hash(x1, y1, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    static float Fbm(float u, float v, int baseFreq, int octaves, int seed)
    {
        float sum = 0f, amp = 0.5f, norm = 0f;
        int f = baseFreq;
        for (int i = 0; i < octaves; i++)
        {
            sum += Noise(u, v, f, seed + i * 17) * amp;
            norm += amp;
            amp *= 0.5f;
            f *= 2;
        }
        return sum / norm;
    }

    // ---------------------------------------------------------------- output

    // Height map -> tangent-space normal map (wraps at the edges so it tiles).
    static Color[] ToNormal(float[] hei, float strength)
    {
        Color[] n = new Color[Size * Size];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float l = hei[y * Size + (x - 1 + Size) % Size];
                float r = hei[y * Size + (x + 1) % Size];
                float d = hei[((y - 1 + Size) % Size) * Size + x];
                float u = hei[((y + 1) % Size) * Size + x];
                Vector3 v = new Vector3((l - r) * strength, (d - u) * strength, 1f).normalized;
                n[y * Size + x] = new Color(v.x * 0.5f + 0.5f, v.y * 0.5f + 0.5f, v.z * 0.5f + 0.5f, 1f);
            }
        }
        return n;
    }

    static void Save(string name, Color[] pixels, bool isNormal)
    {
        string path = Folder + "/" + name + ".png";
        Texture2D t = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        t.SetPixels(pixels);
        t.Apply();
        File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        ti.wrapMode = TextureWrapMode.Repeat;
        ti.filterMode = FilterMode.Bilinear;
        ti.mipmapEnabled = true;
        ti.anisoLevel = 8;
        ti.SaveAndReimport();
    }

    static void MakeMaterial(string matName, string albedo, string normal, float smooth)
    {
        string path = Folder + "/" + matName + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/" + albedo + ".png"));
        m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/" + normal + ".png"));
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_BumpScale", 1f);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", 0f);
        m.EnableKeyword("_NORMALMAP");
        EditorUtility.SetDirty(m);
    }
}
