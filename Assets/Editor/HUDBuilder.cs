using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the in-game HUD in the open scene (everything stays editable in the Hierarchy).
///   Tools > Mazura > Build HUD                 : (re)creates  HUDCanvas/GameHUD  and the sprites it needs
///   Tools > Mazura > Build HUD (redraw icons)  : same, and renders the 3D item icons again
/// The icons are rendered from the models in Assets/Project Assest/Items, the panels are drawn in code.
/// </summary>
public static class HUDBuilder
{
    const string Dir = "Assets/UI/HUD";
    const string IconDir = Dir + "/Icons";
    const string ItemModels = "Assets/Project Assest/Items/";
    static readonly Color Gold = new Color(1f, 0.83f, 0.45f, 1f);

    [MenuItem("Tools/Mazura/Build HUD")]
    public static void Build() { Build(false); }

    [MenuItem("Tools/Mazura/Build HUD (redraw icons)")]
    public static void BuildRedraw() { Build(true); }

    static void Build(bool redrawIcons)
    {
        if (Application.isPlaying) { Debug.LogError("HUDBuilder: stop Play mode first."); return; }
        EnsureFolder("Assets/UI");
        EnsureFolder(Dir);
        EnsureFolder(IconDir);

        // ---------------------------------------------------------------- sprites
        Sprite panel = Rounded(Dir + "/Panel.png", 128, 128, 22f, 5f, new Color(0.06f, 0.055f, 0.07f, 0.88f), 0f, 0f);
        Sprite slot = Rounded(Dir + "/Slot.png", 96, 96, 16f, 3f, new Color(0.07f, 0.07f, 0.085f, 0.9f), 0f, 0f);
        Sprite slotSel = Rounded(Dir + "/SlotSelected.png", 136, 136, 18f, 6f, new Color(1f, 0.8f, 0.3f, 0.12f), 16f, 0.8f);
        Sprite barBg = Rounded(Dir + "/BarBg.png", 64, 32, 16f, 3f, new Color(0.03f, 0.03f, 0.04f, 0.95f), 0f, 0f);
        Sprite barFill = Pill(Dir + "/BarFill.png");
        Sprite pip = Diamond(Dir + "/Pip.png");
        Sprite sun = Sun(IconDir + "/Icon_Sun.png");
        Sprite moon = Moon(IconDir + "/Icon_Moon.png");

        Sprite heart = ModelIcon(IconDir + "/Icon_Heart.png", ItemModels + "Heart.fbx", Quaternion.identity, redrawIcons);
        Sprite map = ModelIcon(IconDir + "/Icon_Map.png", ItemModels + "Parchment.fbx", Quaternion.Euler(0f, 0f, -20f), redrawIcons);
        Sprite axe = ModelIcon(IconDir + "/Icon_Axe.png", ItemModels + "Axe_small.fbx", Quaternion.Euler(0f, 0f, -40f), redrawIcons);
        Sprite sword = ModelIcon(IconDir + "/Icon_Sword.png", ItemModels + "Sword.fbx", Quaternion.Euler(0f, 0f, -40f), redrawIcons);
        Sprite pick = PickaxeIcon(IconDir + "/Icon_Pickaxe.png", redrawIcons);

        // ---------------------------------------------------------------- canvas
        GameObject hudCanvas = GameObject.Find("HUDCanvas");
        if (hudCanvas == null) { Debug.LogError("HUDBuilder: HUDCanvas not found."); return; }
        Transform old = hudCanvas.transform.Find("GameHUD");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        RectTransform root = NewRect("GameHUD", hudCanvas.transform);
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = Vector2.zero; root.offsetMax = Vector2.zero;
        root.SetAsFirstSibling();
        GameHUD hud = root.gameObject.AddComponent<GameHUD>();

        // ---- top left: health + map pieces
        RectTransform tl = NewRect("StatusPanel", root);
        Place(tl, new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -28), new Vector2(480, 152));
        NewImage("Frame", tl, panel, Color.white, true, true);
        NewImage("HeartIcon", tl, heart, Color.white, false).rectTransform.Set(new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -16), new Vector2(76, 76));
        Image hpBg = NewImage("HealthBarBg", tl, barBg, Color.white, true);
        hpBg.rectTransform.Set(new Vector2(0, 1), new Vector2(0, 1), new Vector2(108, -30), new Vector2(350, 38));
        Image hpFill = NewImage("HealthBarFill", hpBg.transform, barFill, new Color(0.85f, 0.18f, 0.2f, 1f), false);
        hpFill.preserveAspect = false;
        hpFill.type = Image.Type.Filled; hpFill.fillMethod = Image.FillMethod.Horizontal; hpFill.fillOrigin = 0; hpFill.fillAmount = 1f;
        hpFill.rectTransform.anchorMin = Vector2.zero; hpFill.rectTransform.anchorMax = Vector2.one;
        hpFill.rectTransform.offsetMin = new Vector2(4, 4); hpFill.rectTransform.offsetMax = new Vector2(-4, -4);
        TextMeshProUGUI hpText = NewText("HealthText", hpBg.transform, "100 / 100", 24f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(hpText.rectTransform);

        NewImage("MapIcon", tl, map, Color.white, false).rectTransform.Set(new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -82), new Vector2(60, 60));
        TextMeshProUGUI mapLabel = NewText("MapLabel", tl, "MAP PIECES", 20f, Gold, TextAlignmentOptions.Left, FontStyles.Bold);
        mapLabel.rectTransform.Set(new Vector2(0, 1), new Vector2(0, 1), new Vector2(92, -88), new Vector2(220, 26));
        Image[] pips = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            pips[i] = NewImage("Pip" + (i + 1), tl, pip, new Color(0.25f, 0.23f, 0.22f, 1f), false);
            pips[i].rectTransform.Set(new Vector2(0, 1), new Vector2(0, 1), new Vector2(96 + i * 38, -118), new Vector2(28, 28));
        }
        TextMeshProUGUI mapText = NewText("MapText", tl, "0 / 4", 30f, Gold, TextAlignmentOptions.Right, FontStyles.Bold);
        mapText.rectTransform.Set(new Vector2(0, 1), new Vector2(0, 1), new Vector2(340, -104), new Vector2(120, 40));

        // ---- top right: day / night
        RectTransform tr = NewRect("TimePanel", root);
        Place(tr, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-28, -28), new Vector2(340, 124));
        NewImage("Frame", tr, panel, Color.white, true, true);
        Image timeIcon = NewImage("TimeIcon", tr, sun, Color.white, false);
        timeIcon.rectTransform.Set(new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -18), new Vector2(88, 88));
        TextMeshProUGUI timeTitle = NewText("TimeTitle", tr, "DAY", 28f, Gold, TextAlignmentOptions.Left, FontStyles.Bold);
        timeTitle.rectTransform.Set(new Vector2(0, 1), new Vector2(0, 1), new Vector2(120, -12), new Vector2(200, 34));
        TextMeshProUGUI timeText = NewText("TimeText", tr, "10:00", 48f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
        timeText.rectTransform.Set(new Vector2(0, 1), new Vector2(0, 1), new Vector2(120, -42), new Vector2(200, 54));
        Image timeBg = NewImage("TimeBarBg", tr, barBg, Color.white, true);
        timeBg.rectTransform.Set(new Vector2(0, 1), new Vector2(0, 1), new Vector2(120, -100), new Vector2(200, 14));
        Image timeFill = NewImage("TimeBarFill", timeBg.transform, barFill, new Color(1f, 0.78f, 0.25f, 1f), false);
        timeFill.preserveAspect = false;
        timeFill.type = Image.Type.Filled; timeFill.fillMethod = Image.FillMethod.Horizontal; timeFill.fillOrigin = 0; timeFill.fillAmount = 1f;
        timeFill.rectTransform.anchorMin = Vector2.zero; timeFill.rectTransform.anchorMax = Vector2.one;
        timeFill.rectTransform.offsetMin = new Vector2(2, 2); timeFill.rectTransform.offsetMax = new Vector2(-2, -2);

        // ---- bottom centre: tool hotbar
        RectTransform bar = NewRect("Hotbar", root);
        Place(bar, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 28), new Vector2(4 * 104 + 3 * 14, 104));
        string[] toolNames = { "Axe", "Pickaxe", "Sword", "" };           // 4th slot: empty, no icon, no name
        Sprite[] toolIcons = { axe, pick, sword, null };
        GameObject[] selected = new GameObject[4];
        for (int i = 0; i < 4; i++)
        {
            RectTransform s = NewRect("Tool_" + (i + 1), bar);
            Place(s, new Vector2(0, 0), new Vector2(0, 0), new Vector2(i * 118, 0), new Vector2(104, 104));
            NewImage("Slot", s, slot, Color.white, true, true);
            Image sel = NewImage("Selected", s, slotSel, Color.white, true);
            sel.rectTransform.Set(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(136, 136));
            selected[i] = sel.gameObject;
            sel.gameObject.SetActive(i == 0);
            if (toolIcons[i] != null)
            {
                Image ic = NewImage("Icon", s, toolIcons[i], Color.white, false);
                ic.rectTransform.Set(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 8), new Vector2(70, 70));
            }
            TextMeshProUGUI key = NewText("Key", s, (i + 1).ToString(), 24f, Gold, TextAlignmentOptions.Left, FontStyles.Bold);
            key.rectTransform.Set(new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -6), new Vector2(30, 30));
            if (toolNames[i].Length > 0)
            {
                TextMeshProUGUI nm = NewText("Name", s, toolNames[i], 18f, new Color(0.9f, 0.87f, 0.8f, 1f), TextAlignmentOptions.Center, FontStyles.Normal);
                nm.rectTransform.Set(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 6), new Vector2(100, 24));
            }
        }

        // ---- right side, under the clock: resources (a column, so it never collides with the hotbar on narrow screens)
        ItemType[] items = { ItemType.Wood, ItemType.Stone, ItemType.Herb, ItemType.Ruby, ItemType.Emerald, ItemType.Sapphire, ItemType.Amber };
        float rowH = 46f;
        RectTransform rp = NewRect("ResourcePanel", root);
        Place(rp, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-28, -164), new Vector2(170, items.Length * rowH + 24));
        NewImage("Frame", rp, panel, Color.white, true, true);
        RectTransform[] cells = new RectTransform[items.Length];
        TMP_Text[] counts = new TMP_Text[items.Length];
        Image[] icons = new Image[items.Length];
        for (int k = 0; k < items.Length; k++)
        {
            RectTransform cell = NewRect(items[k].ToString(), rp);
            Place(cell, new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(85f, -(12f + k * rowH + rowH * 0.5f)), new Vector2(150, 42));
            Sprite itemSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Inventory/Icons/Item_" + items[k] + ".png");
            Image icon = NewImage("Icon", cell, itemSprite, Color.white, false);
            icon.rectTransform.Set(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(40, 40));
            TextMeshProUGUI cnt = NewText("Count", cell, "0", 28f, Color.white, TextAlignmentOptions.Right, FontStyles.Bold);
            cnt.rectTransform.Set(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-10, 0), new Vector2(80, 38));
            cells[k] = cell; counts[k] = cnt; icons[k] = icon;
        }

        // ---- hook everything up
        hud.hpFill = hpFill; hud.hpText = hpText;
        hud.mapText = mapText; hud.mapPips = pips;
        hud.timeIcon = timeIcon; hud.sunSprite = sun; hud.moonSprite = moon;
        hud.timeTitle = timeTitle; hud.timeText = timeText; hud.timeBar = timeFill;
        hud.toolSelected = selected;
        hud.resourceItems = items; hud.resourceCells = cells; hud.resourceCounts = counts; hud.resourceIcons = icons;

        // ---- hide the old plain texts (kept in the scene, only switched off) and style the hint
        foreach (string n in new[] { "MapPiecesText", "HealthText", "PhaseText", "InventoryText", "ToolText" })
        {
            Transform t = hudCanvas.transform.Find(n);
            if (t != null) t.gameObject.SetActive(false);
        }
        Transform hint = hudCanvas.transform.Find("HintText");
        if (hint != null)
        {
            RectTransform hr = hint.GetComponent<RectTransform>();
            hr.anchorMin = hr.anchorMax = new Vector2(0.5f, 0f); hr.pivot = new Vector2(0.5f, 0f);
            hr.anchoredPosition = new Vector2(0f, 150f);
            TMP_Text ht = hint.GetComponent<TMP_Text>();
            if (ht != null) { ht.color = Gold; ht.fontSize = 40f; ht.fontStyle = FontStyles.Bold; }
        }

        EditorUtility.SetDirty(hud);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(hudCanvas.scene);
        Debug.Log("HUDBuilder: HUD built.");
    }

    // ================================================================== UI helpers

    static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    static void Set(this RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        Place(rt, anchor, pivot, pos, size);
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static Image NewImage(string name, Transform parent, Sprite sprite, Color color, bool sliced, bool stretch = false)
    {
        RectTransform rt = NewRect(name, parent);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite; img.color = color;
        img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        img.preserveAspect = !sliced;
        img.raycastTarget = false;
        if (stretch) Stretch(rt);
        return img;
    }

    static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align, FontStyles style)
    {
        RectTransform rt = NewRect(name, parent);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = color; t.alignment = align; t.fontStyle = style;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    // ================================================================== drawn sprites

    static Sprite Save(Texture2D t, string path, Vector4 border)
    {
        File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.filterMode = FilterMode.Bilinear;
        imp.maxTextureSize = 512;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.spriteBorder = border;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static float RoundedSD(float px, float py, float hx, float hy, float r)
    {
        float qx = Mathf.Abs(px) - (hx - r), qy = Mathf.Abs(py) - (hy - r);
        float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
        return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    // dark rounded panel with a gold border; glow > 0 adds a soft gold glow outside the border
    static Sprite Rounded(string path, int w, int h, float radius, float border, Color fill, float glow, float glowStrength)
    {
        Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        float inset = glow;                                   // keep space for the glow
        float hx = w * 0.5f - inset, hy = h * 0.5f - inset;
        Color gTop = new Color(1f, 0.85f, 0.45f, 1f), gBot = new Color(0.7f, 0.45f, 0.12f, 1f);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float px = x + 0.5f - w * 0.5f, py = y + 0.5f - h * 0.5f;
                float d = RoundedSD(px, py, hx, hy, radius);
                float cover = Mathf.Clamp01(0.5f - d);
                float s = -d;
                float bm = Mathf.Clamp01(border - s + 0.5f) * cover;
                Color gold = Color.Lerp(gBot, gTop, Mathf.Clamp01((py + h * 0.5f) / h));
                Color c = Color.Lerp(fill, gold, bm);
                float a = cover * Mathf.Lerp(fill.a, 1f, bm);
                if (glow > 0f && d > 0f)
                {
                    float g = Mathf.Exp(-d / (glow * 0.35f)) * glowStrength;
                    c = new Color(1f, 0.8f, 0.3f, 1f);
                    a = Mathf.Max(a, g);
                }
                t.SetPixel(x, y, new Color(c.r, c.g, c.b, a));
            }
        t.Apply();
        float b = radius + border;
        float bd = glow > 0f ? 0f : b;
        return Save(t, path, glow > 0f ? Vector4.zero : new Vector4(bd, bd, bd, bd));
    }

    static Sprite Pill(string path)
    {
        int w = 64, h = 32;
        Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float px = x + 0.5f - w * 0.5f, py = y + 0.5f - h * 0.5f;
                float d = RoundedSD(px, py, w * 0.5f - 0.5f, h * 0.5f - 0.5f, h * 0.5f - 0.5f);
                float cover = Mathf.Clamp01(0.5f - d);
                float v = Mathf.Lerp(0.72f, 1f, y / (float)h);                  // lighter on top
                float shine = Mathf.Clamp01((y - h * 0.62f) / (h * 0.2f)) * 0.18f;
                t.SetPixel(x, y, new Color(Mathf.Clamp01(v + shine), Mathf.Clamp01(v + shine), Mathf.Clamp01(v + shine), cover));
            }
        t.Apply();
        return Save(t, path, Vector4.zero);
    }

    static Sprite Diamond(string path)
    {
        int n = 32;
        Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float px = Mathf.Abs(x + 0.5f - n * 0.5f), py = Mathf.Abs(y + 0.5f - n * 0.5f);
                float d = (px + py) - 14f;
                float cover = Mathf.Clamp01(0.5f - d);
                float inner = Mathf.Clamp01(-d - 2.5f);
                Color c = Color.Lerp(new Color(0.18f, 0.1f, 0.02f, 1f), Color.white, inner);
                t.SetPixel(x, y, new Color(c.r, c.g, c.b, cover));
            }
        t.Apply();
        return Save(t, path, Vector4.zero);
    }

    static float CircleSD(float px, float py, float cx, float cy, float r) { return Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy)) - r; }

    static float CapsuleSD(float px, float py, float ax, float ay, float bx, float by, float r)
    {
        float pax = px - ax, pay = py - ay, bax = bx - ax, bay = by - ay;
        float hh = Mathf.Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
        float dx = pax - bax * hh, dy = pay - bay * hh;
        return Mathf.Sqrt(dx * dx + dy * dy) - r;
    }

    static Sprite Sun(string path)
    {
        int n = 128;
        Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float px = x + 0.5f, py = y + 0.5f, c = n * 0.5f;
                float d = CircleSD(px, py, c, c, 26f);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4f;
                    float rd = CapsuleSD(px, py, c + Mathf.Cos(a) * 38f, c + Mathf.Sin(a) * 38f, c + Mathf.Cos(a) * 54f, c + Mathf.Sin(a) * 54f, 5.5f);
                    d = Mathf.Min(d, rd);
                }
                float cover = Mathf.Clamp01(0.5f - d);
                float inner = Mathf.Clamp01(-d - 2.5f);
                Color fillC = Color.Lerp(new Color(1f, 0.72f, 0.15f, 1f), new Color(1f, 0.93f, 0.5f, 1f), Mathf.Clamp01((py - 20f) / 90f));
                Color col = Color.Lerp(new Color(0.5f, 0.25f, 0.03f, 1f), fillC, inner);
                t.SetPixel(x, y, new Color(col.r, col.g, col.b, cover));
            }
        t.Apply();
        return Save(t, path, Vector4.zero);
    }

    static Sprite Moon(string path)
    {
        int n = 128;
        Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float px = x + 0.5f, py = y + 0.5f, c = n * 0.5f;
                float d1 = CircleSD(px, py, c - 4f, c, 44f);
                float d2 = CircleSD(px, py, c + 20f, c + 12f, 38f);
                float d = Mathf.Max(d1, -d2);
                float cover = Mathf.Clamp01(0.5f - d);
                float inner = Mathf.Clamp01(-d - 2.5f);
                Color fillC = Color.Lerp(new Color(0.72f, 0.8f, 1f, 1f), new Color(0.95f, 0.97f, 1f, 1f), Mathf.Clamp01((py - 10f) / 100f));
                Color col = Color.Lerp(new Color(0.18f, 0.24f, 0.5f, 1f), fillC, inner);
                t.SetPixel(x, y, new Color(col.r, col.g, col.b, cover));
            }
        t.Apply();
        return Save(t, path, Vector4.zero);
    }

    // ================================================================== 3D icons

    static Sprite ModelIcon(string path, string modelPath, Quaternion extra, bool redraw)
    {
        if (!redraw && File.Exists(path)) return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (src == null) { Debug.LogWarning("HUDBuilder: model not found: " + modelPath); return null; }
        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
        inst.transform.position = Vector3.zero;
        inst.transform.rotation = extra * inst.transform.rotation;
        RenderIcon(inst, path);
        return Importer(path);
    }

    static Sprite PickaxeIcon(string path, bool redraw)
    {
        if (!redraw && File.Exists(path)) return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        ToolController tc = Object.FindAnyObjectByType<ToolController>();
        if (tc == null || tc.pickaxeModel == null) return null;
        GameObject inst = Object.Instantiate(tc.pickaxeModel);
        inst.SetActive(true);
        inst.transform.SetParent(null);
        inst.transform.localScale = Vector3.one;                       // in the hand it is scaled by the wrist bone, here we use metres
        inst.transform.rotation = Quaternion.Euler(0f, 0f, 35f) * Quaternion.Euler(0f, 90f, 0f);   // shaft to the right, head up, seen from the side
        RenderIcon(inst, path);
        return Importer(path);
    }

    static Sprite Importer(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.filterMode = FilterMode.Bilinear;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // renders the object on a transparent background (it is destroyed afterwards)
    static void RenderIcon(GameObject inst, string path)
    {
        const int layer = 31, size = 256;
        Vector3 far = new Vector3(5000f, 5000f, 5000f);
        inst.transform.position += far;
        foreach (Transform t in inst.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;

        Renderer[] rs = inst.GetComponentsInChildren<Renderer>();
        Bounds bb = rs[0].bounds;
        foreach (Renderer r in rs) bb.Encapsulate(r.bounds);

        GameObject camGO = new GameObject("IconCam");
        Camera cam = camGO.AddComponent<Camera>();
        cam.cullingMask = 1 << layer;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.orthographic = true;
        cam.orthographicSize = Mathf.Max(bb.extents.x, bb.extents.y) * 1.1f;
        cam.nearClipPlane = 0.1f; cam.farClipPlane = 60f;
        camGO.transform.position = bb.center + new Vector3(0f, 0f, -20f);
        camGO.transform.rotation = Quaternion.identity;

        // light the icon only: switch the scene lights off for a moment
        Light[] sceneLights = Object.FindObjectsByType<Light>();
        bool[] was = new bool[sceneLights.Length];
        for (int i = 0; i < sceneLights.Length; i++) { was[i] = sceneLights[i].enabled; sceneLights[i].enabled = false; }
        GameObject lg = new GameObject("IconLight");
        Light l = lg.AddComponent<Light>();
        l.type = LightType.Directional; l.intensity = 1.5f; l.color = Color.white;
        lg.transform.rotation = Quaternion.Euler(30f, -30f, 0f);
        UnityEngine.Rendering.AmbientMode am = RenderSettings.ambientMode;
        Color al = RenderSettings.ambientLight;
        bool fog = RenderSettings.fog;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.62f, 0.62f, 0.66f);
        RenderSettings.fog = false;

        RenderTexture rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;
        File.WriteAllBytes(path, tex.EncodeToPNG());

        RenderSettings.ambientMode = am; RenderSettings.ambientLight = al; RenderSettings.fog = fog;
        for (int i = 0; i < sceneLights.Length; i++) if (sceneLights[i] != null) sceneLights[i].enabled = was[i];
        Object.DestroyImmediate(tex);
        rt.Release(); Object.DestroyImmediate(rt);
        Object.DestroyImmediate(lg);
        Object.DestroyImmediate(camGO);
        Object.DestroyImmediate(inst);
    }
}
