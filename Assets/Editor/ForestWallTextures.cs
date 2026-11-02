using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Makes the "dark forest" wall textures by code (no outside files needed):
///   Forest_Wall_Albedo.png  - old wet stone blocks, moss near the bottom and top, dark cracks, water streaks
///   Forest_Wall_Normal.png  - matching normal map (block edges, cracks, moss lumps)
///   Forest_Vines.png        - hanging vines with leaves (transparent background, for cutout quads)
/// The textures tile sideways (3 metres = one tile) and the moss pattern follows height, so the bottom of
/// every wall is mossier than the top. Run from Mazura menu, or call ForestWallTextures.Generate().
/// </summary>
public static class ForestWallTextures
{
    public const string Folder = "Assets/Textures/Maze/Forest";

    [MenuItem("Mazura/Generate Forest Wall Textures")]
    public static void Generate()
    {
        Directory.CreateDirectory(Folder);
        MakeWall(1024, 1024);
        MakeVines(256, 512);
        AssetDatabase.Refresh();
        Import(Folder + "/Forest_Wall_Albedo.png", false, false);
        Import(Folder + "/Forest_Wall_Normal.png", true, false);
        Import(Folder + "/Forest_Vines.png", false, true);
        Debug.Log("ForestWallTextures: textures created in " + Folder);
    }

    static void Import(string path, bool normal, bool clamp)
    {
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) return;
        ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        ti.sRGBTexture = !normal;
        ti.wrapMode = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
        ti.filterMode = FilterMode.Trilinear;
        ti.anisoLevel = 8;
        ti.mipmapEnabled = true;
        ti.alphaIsTransparency = clamp;
        ti.maxTextureSize = 1024;
        ti.textureCompression = TextureImporterCompression.CompressedHQ;
        ti.SaveAndReimport();
    }

    // ---------------------------------------------------------------- noise (tiles sideways)

    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 1442695041;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }
    }

    // Value noise. 'period' = number of lattice cells across u in [0,1), so the result tiles in u.
    static float Noise(float u, float v, int period, int seed)
    {
        float x = u * period, y = v * period;
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
        int xa = ((x0 % period) + period) % period, xb = (xa + 1) % period;
        float a = Hash(xa, y0, seed), b = Hash(xb, y0, seed);
        float c = Hash(xa, y0 + 1, seed), d = Hash(xb, y0 + 1, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    static float Fbm(float u, float v, int period, int seed, int octaves)
    {
        float sum = 0f, amp = 0.5f, norm = 0f;
        for (int i = 0; i < octaves; i++)
        {
            sum += Noise(u, v, period, seed + i * 17) * amp;
            norm += amp;
            amp *= 0.5f; period *= 2;
        }
        return sum / norm;
    }

    // ---------------------------------------------------------------- wall

    static void MakeWall(int W, int H)
    {
        const float tileMeters = 3f;
        const int rows = 4;
        System.Random rnd = new System.Random(2026);

        // Block boundaries per row (widths sum to 1, so the pattern tiles sideways).
        float[][] bounds = new float[rows][];
        float[][] tone = new float[rows][];
        float[] rowShift = new float[rows];
        for (int r = 0; r < rows; r++)
        {
            int n = 3 + rnd.Next(2);
            float[] w = new float[n];
            float sum = 0f;
            for (int i = 0; i < n; i++) { w[i] = 0.6f + (float)rnd.NextDouble(); sum += w[i]; }
            bounds[r] = new float[n + 1];
            tone[r] = new float[n];
            float acc = 0f;
            for (int i = 0; i < n; i++)
            {
                bounds[r][i] = acc;
                acc += w[i] / sum;
                tone[r][i] = 0.7f + (float)rnd.NextDouble() * 0.5f;
            }
            bounds[r][n] = 1f;
            rowShift[r] = (float)rnd.NextDouble();
        }

        float[] height = new float[W * H];
        Color[] col = new Color[W * H];

        for (int y = 0; y < H; y++)
        {
            float v = (y + 0.5f) / H;
            for (int x = 0; x < W; x++)
            {
                float u = (x + 0.5f) / W;

                // Which block are we in?
                float rv = v * rows;
                int r = Mathf.Min(rows - 1, (int)rv);
                float rowFrac = rv - r;
                float uu = u + rowShift[r]; uu -= Mathf.Floor(uu);
                float[] bd = bounds[r];
                int bi = 0;
                while (bi < bd.Length - 2 && uu >= bd[bi + 1]) bi++;
                float dx = Mathf.Min(uu - bd[bi], bd[bi + 1] - uu) * tileMeters;      // metres to the side edge
                float dy = Mathf.Min(rowFrac, 1f - rowFrac) * (tileMeters / rows);     // metres to top/bottom edge
                float edge = Mathf.Min(dx, dy);
                // wobble the edge a little so the blocks are not perfectly straight
                edge += (Noise(u, v, 32, 5) - 0.5f) * 0.03f;

                float bevel = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge / 0.07f));
                float mortar = 1f - Mathf.Clamp01(edge / 0.03f);

                float n1 = Fbm(u, v, 6, 11, 5);
                float n2 = Fbm(u, v, 24, 23, 3);
                float blockTone = tone[r][bi] * (0.9f + 0.2f * Noise(u, v, 3, 31 + bi + r * 7));

                // stone colour (dark, wet, slightly green-grey)
                float s = Mathf.Lerp(0.20f, 0.46f, n1) * blockTone;
                Color stone = new Color(s * 0.98f, s * 1.02f, s * 0.92f);

                // dark streaks where water runs down
                float streak = Fbm(u, v, 48, 41, 2);
                float streakV = Mathf.Lerp(1f, 0.55f, Mathf.SmoothStep(0.55f, 0.85f, streak) * Mathf.Lerp(0.4f, 1f, v));
                stone *= streakV;

                // thin dark cracks
                float crack = 1f - Mathf.Abs(2f * Fbm(u, v, 9, 53, 3) - 1f);
                float crackMask = Mathf.SmoothStep(0.965f, 0.99f, crack);
                stone *= 1f - 0.7f * crackMask;

                // moss: thick near the ground, patchy in the middle, some again at the very top, always in the gaps
                float mossBase = 0.42f * Mathf.Pow(1f - v, 1.6f) + 0.22f * Mathf.SmoothStep(0.86f, 1f, v);
                float mossNoise = Fbm(u, v, 7, 61, 5);
                float mossAmt = Mathf.SmoothStep(0.42f, 0.70f, mossNoise * 0.62f + mossBase + 0.32f * mortar + 0.10f * (1f - bevel));
                float mossShade = Fbm(u, v, 40, 67, 3);
                Color mossDark = new Color(0.05f, 0.14f, 0.05f);
                Color mossLight = new Color(0.20f, 0.44f, 0.10f);
                Color moss = Color.Lerp(mossDark, mossLight, mossShade * mossShade * 1.4f);

                Color c = Color.Lerp(stone, moss, mossAmt);
                c = Color.Lerp(c, new Color(0.04f, 0.06f, 0.04f), mortar * (1f - mossAmt) * 0.8f);
                // pale lichen specks
                float lich = Noise(u, v, 90, 71);
                if (lich > 0.86f && mossAmt < 0.5f) c = Color.Lerp(c, new Color(0.32f, 0.38f, 0.26f), 0.35f);
                c.a = 1f;
                col[y * W + x] = c;

                height[y * W + x] = bevel * 0.55f + (n2 - 0.5f) * 0.18f - crackMask * 0.25f
                                    + mossAmt * (Fbm(u, v, 60, 83, 3) - 0.3f) * 0.35f;
            }
        }

        // Normal map from the height field (tiles sideways).
        Color[] nrm = new Color[W * H];
        float strength = 5f;
        for (int y = 0; y < H; y++)
        {
            int yu = Mathf.Min(H - 1, y + 1), yd = Mathf.Max(0, y - 1);
            for (int x = 0; x < W; x++)
            {
                int xl = (x - 1 + W) % W, xr = (x + 1) % W;
                float dhx = (height[y * W + xr] - height[y * W + xl]) * strength;
                float dhy = (height[yu * W + x] - height[yd * W + x]) * strength;
                Vector3 n = new Vector3(-dhx, -dhy, 1f).normalized;
                nrm[y * W + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }
        }

        Save(col, W, H, Folder + "/Forest_Wall_Albedo.png");
        Save(nrm, W, H, Folder + "/Forest_Wall_Normal.png");
    }

    // ---------------------------------------------------------------- vines

    static void MakeVines(int W, int H)
    {
        System.Random rnd = new System.Random(77);
        Color[] px = new Color[W * H];
        Color leafBg = new Color(0.07f, 0.2f, 0.07f, 0f); // colour under the transparent pixels (avoids pale edges)
        for (int i = 0; i < px.Length; i++) px[i] = leafBg;

        int strands = 5;
        for (int s = 0; s < strands; s++)
        {
            float baseX = (s + 0.5f) / strands * W + ((float)rnd.NextDouble() - 0.5f) * 16f;
            float length = H * Mathf.Lerp(0.45f, 0.98f, (float)rnd.NextDouble());
            float amp = 4f + (float)rnd.NextDouble() * 8f;
            float freq = 0.012f + (float)rnd.NextDouble() * 0.015f;
            float phase = (float)rnd.NextDouble() * 6.28f;
            float nextLeaf = 10f + (float)rnd.NextDouble() * 10f;
            int side = rnd.Next(2) == 0 ? -1 : 1;

            for (float d = 0f; d < length; d += 0.5f)
            {
                float yy = H - 1 - d;
                float xx = baseX + Mathf.Sin(d * freq * 6.28f + phase) * amp * Mathf.Clamp01(d / 60f);
                float rad = Mathf.Lerp(2.2f, 0.9f, d / length);
                Color stem = new Color(0.20f, 0.22f, 0.08f, 1f);
                Disc(px, W, H, xx, yy, rad, stem);

                if (d > nextLeaf)
                {
                    nextLeaf += 12f + (float)rnd.NextDouble() * 14f;
                    side = -side;
                    float size = Mathf.Lerp(9f, 15f, (float)rnd.NextDouble()) * Mathf.Lerp(1f, 0.7f, d / length);
                    float ang = side * Mathf.Lerp(0.5f, 1.2f, (float)rnd.NextDouble()) + 1.57f * 0f;
                    Leaf(px, W, H, xx, yy, size, ang, rnd);
                }
            }
            // a leaf at the tip
            float tipX = baseX + Mathf.Sin(length * freq * 6.28f + phase) * amp * Mathf.Clamp01(length / 60f);
            Leaf(px, W, H, tipX, H - 1 - length, 10f, (float)(rnd.NextDouble() - 0.5), rnd);
        }
        Save(px, W, H, Folder + "/Forest_Vines.png");
    }

    static void Disc(Color[] px, int W, int H, float cx, float cy, float r, Color c)
    {
        int x0 = Mathf.Max(0, (int)(cx - r - 1)), x1 = Mathf.Min(W - 1, (int)(cx + r + 1));
        int y0 = Mathf.Max(0, (int)(cy - r - 1)), y1 = Mathf.Min(H - 1, (int)(cy + r + 1));
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                if (dx * dx + dy * dy <= r * r) px[y * W + x] = c;
            }
    }

    // Pointed leaf, 'size' pixels long, pointing from (cx,cy) in direction ang (0 = down).
    static void Leaf(Color[] px, int W, int H, float cx, float cy, float size, float ang, System.Random rnd)
    {
        float dirX = Mathf.Sin(ang), dirY = -Mathf.Cos(ang);
        float perpX = -dirY, perpY = dirX;
        float half = size * 0.38f;
        float shade = 0.75f + (float)rnd.NextDouble() * 0.5f;
        int steps = (int)(size * 2f);
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;                       // 0 at the stem, 1 at the tip
            float width = half * Mathf.Sin(t * Mathf.PI) * (1f - 0.25f * t);
            for (float w = -width; w <= width; w += 0.5f)
            {
                float x = cx + dirX * t * size + perpX * w;
                float y = cy + dirY * t * size + perpY * w;
                int ix = (int)x, iy = (int)y;
                if (ix < 0 || iy < 0 || ix >= W || iy >= H) continue;
                float vein = 1f - Mathf.Clamp01(Mathf.Abs(w) / Mathf.Max(0.5f, width));
                Color c = new Color(0.10f + 0.08f * shade, (0.34f + 0.22f * vein) * shade, 0.08f, 1f);
                px[iy * W + ix] = c;
            }
        }
    }

    static void Save(Color[] pixels, int W, int H, string path)
    {
        Texture2D t = new Texture2D(W, H, TextureFormat.RGBA32, false);
        t.SetPixels(pixels);
        t.Apply();
        File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
    }
}
