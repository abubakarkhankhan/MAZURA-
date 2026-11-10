using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Builds the Inventory UI in the open scene (edit mode, everything stays editable in the Hierarchy).
///   Tools > Mazura > Build Inventory UI          : (re)creates InventoryCanvas + item icons
///   Tools > Mazura > Apply Inventory Panel Sprite: puts Assets/UI/Inventory/InventoryPanel.png on the panel
/// The layout is measured on the 1221 x 1288 design image, so the slots line up with the artwork.
/// </summary>
public static class InventoryUIBuilder
{
    const float RefW = 1221f, RefH = 1288f;      // size of the design image
    const float PanelW = 860f;                    // panel width in canvas units (1920x1080 reference)
    const string Dir = "Assets/UI/Inventory";
    const string IconDir = Dir + "/Icons";
    const string PanelSpritePath = Dir + "/InventoryPanel.png";

    static readonly Color Gold = new Color(0.86f, 0.62f, 0.15f, 1f);
    static readonly Color Dark = new Color(0.055f, 0.055f, 0.065f, 1f);
    static readonly Color TextGold = new Color(1f, 0.83f, 0.45f, 1f);
    static readonly Color TextSoft = new Color(0.84f, 0.81f, 0.74f, 1f);

    // ------------------------------------------------------------------ build

    [MenuItem("Tools/Mazura/Build Inventory UI")]
    public static void Build()
    {
        if (Application.isPlaying) { Debug.LogError("InventoryUIBuilder: stop Play mode first."); return; }

        EnsureFolder("Assets/UI");
        EnsureFolder(Dir);
        EnsureFolder(IconDir);

        // 1. item icons
        string[] names = Enum.GetNames(typeof(ItemType));
        Sprite[] icons = new Sprite[names.Length];
        for (int i = 0; i < names.Length; i++) icons[i] = MakeIconSprite((ItemType)i);

        // 2. EventSystem (needed for clicks on UI)
        if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        // 3. canvas
        var old = GameObject.Find("InventoryCanvas");
        if (old != null) UnityEngine.Object.DestroyImmediate(old);

        var canvasGO = new GameObject("InventoryCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.layer = LayerMask.NameToLayer("UI");
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // root (fade) + dimmer
        var root = NewRect("Root", canvasGO.transform);
        Stretch(root);
        var group = root.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f; group.interactable = false; group.blocksRaycasts = false;

        var dimmer = NewImage("Dimmer", root, new Color(0f, 0f, 0f, 0.6f), true);
        Stretch(dimmer.rectTransform);

        // panel
        var panelRT = NewRect("Panel", root);
        panelRT.anchorMin = panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.pivot = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta = new Vector2(PanelW, PanelW * RefH / RefW);
        var panelImg = panelRT.gameObject.AddComponent<Image>();
        panelImg.color = Color.white;
        panelImg.preserveAspect = true;
        panelImg.enabled = false;          // enabled by "Apply Inventory Panel Sprite"

        // placeholder look (hidden once the real artwork is applied)
        var ph = NewRect("Placeholder", panelRT);
        Stretch(ph);
        Frame(ph, "Panel_Frame", 0, 0, RefW, RefH, 7);
        Frame(ph, "Title_Frame", 300, 145, 935, 228, 3);
        Frame(ph, "Detail_Frame", 830, 275, 1155, 898, 3);
        Frame(ph, "DetailIcon_Frame", 910, 310, 1075, 478, 3);
        for (int i = 0; i < 20; i++)
        {
            SlotRect(i, out float x0, out float y0);
            Frame(ph, "Slot_Frame_" + i.ToString("00"), x0, y0, x0 + 133f, y0 + 133f, 2);
        }
        float[] toolX = { 256f, 448f, 638f, 827f };
        float[] toolW = { 140f, 140f, 140f, 139f };
        for (int i = 0; i < 4; i++) Frame(ph, "Tool_Frame_" + i, toolX[i], 946f, toolX[i] + toolW[i], 1083f, 2);

        // title
        var title = NewText("Title", panelRT, "INVENTORY", 40f, TextAlignmentOptions.Center, TextGold, FontStyles.Bold);
        SetRef(title.rectTransform, 330, 150, 920, 225);
        title.characterSpacing = 8f;

        // grid
        var slotsParent = NewRect("Slots", panelRT);
        Stretch(slotsParent);
        var slotUIs = new InventorySlotUI[20];
        for (int i = 0; i < 20; i++)
        {
            SlotRect(i, out float x0, out float y0);
            var rt = NewRect("Slot_" + i.ToString("00"), slotsParent);
            SetRef(rt, x0, y0, x0 + 133f, y0 + 133f);
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;

            var hl = NewImage("Highlight", rt, Color.clear, false);
            Stretch(hl.rectTransform, 3f);

            var icon = NewImage("Icon", rt, Color.white, false);
            icon.rectTransform.anchorMin = new Vector2(0.14f, 0.14f);
            icon.rectTransform.anchorMax = new Vector2(0.86f, 0.86f);
            icon.rectTransform.offsetMin = icon.rectTransform.offsetMax = Vector2.zero;
            icon.preserveAspect = true;
            icon.enabled = false;

            var count = NewText("Count", rt, "", 22f, TextAlignmentOptions.BottomRight, Color.white, FontStyles.Bold);
            count.rectTransform.anchorMin = new Vector2(0.35f, 0.02f);
            count.rectTransform.anchorMax = new Vector2(0.94f, 0.34f);
            count.rectTransform.offsetMin = count.rectTransform.offsetMax = Vector2.zero;

            var ui = rt.gameObject.AddComponent<InventorySlotUI>();
            ui.icon = icon; ui.countText = count; ui.highlight = hl;
            slotUIs[i] = ui;
        }

        // detail panel
        var detail = NewRect("Detail", panelRT);
        Stretch(detail);
        var dIcon = NewImage("Icon", detail, Color.white, false);
        SetRef(dIcon.rectTransform, 924, 324, 1061, 464);
        dIcon.preserveAspect = true;
        dIcon.enabled = false;
        var dName = NewText("Name", detail, "", 32f, TextAlignmentOptions.Center, TextGold, FontStyles.Bold);
        SetRef(dName.rectTransform, 858, 534, 1130, 590);
        var dCount = NewText("Count", detail, "", 24f, TextAlignmentOptions.Center, TextSoft);
        SetRef(dCount.rectTransform, 858, 590, 1130, 630);
        var dDesc = NewText("Description", detail, "", 22f, TextAlignmentOptions.TopLeft, TextSoft);
        SetRef(dDesc.rectTransform, 862, 645, 1126, 880);

        // tool bar
        var tools = NewRect("ToolBar", panelRT);
        Stretch(tools);
        ToolType[] toolTypes = { ToolType.Axe, ToolType.Pickaxe, ToolType.Weapon, ToolType.None };
        string[] toolNames = { "Axe", "Pickaxe", "Weapon", "Empty" };
        var toolUIs = new ToolSlotUI[4];
        for (int i = 0; i < 4; i++)
        {
            var rt = NewRect("Tool_" + (i + 1), tools);
            SetRef(rt, toolX[i], 946f, toolX[i] + toolW[i], 1083f);
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;

            var hl = NewImage("Highlight", rt, Color.clear, false);
            Stretch(hl.rectTransform, 3f);

            var key = NewText("Key", rt, (i + 1).ToString(), 20f, TextAlignmentOptions.TopLeft, TextGold, FontStyles.Bold);
            key.rectTransform.anchorMin = new Vector2(0.08f, 0.62f);
            key.rectTransform.anchorMax = new Vector2(0.5f, 0.97f);
            key.rectTransform.offsetMin = key.rectTransform.offsetMax = Vector2.zero;

            var label = NewText("Label", rt, toolNames[i], 22f, TextAlignmentOptions.Center, TextSoft, FontStyles.Bold);
            Stretch(label.rectTransform, 4f);

            var ts = rt.gameObject.AddComponent<ToolSlotUI>();
            ts.tool = toolTypes[i];
            ts.highlight = hl;
            toolUIs[i] = ts;
        }

        // 4. wire the controller
        var inv = canvasGO.AddComponent<InventoryUI>();
        inv.rootGroup = group;
        inv.panel = panelRT;
        inv.slots = slotUIs;
        inv.detailIcon = dIcon;
        inv.detailName = dName;
        inv.detailCount = dCount;
        inv.detailDescription = dDesc;
        inv.itemIcons = icons;
        inv.toolSlots = toolUIs;

        // 5. artwork, if it is already in the project
        if (File.Exists(ProjectPath(PanelSpritePath))) ApplyPanelSprite();
        else Debug.Log("InventoryUIBuilder: built with the placeholder look. Put the design image at " + PanelSpritePath + " and run Tools > Mazura > Apply Inventory Panel Sprite.");

        Selection.activeGameObject = canvasGO;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("InventoryUIBuilder: InventoryCanvas built (" + names.Length + " item icons).");
    }

    // ------------------------------------------------------------------ artwork

    [MenuItem("Tools/Mazura/Apply Inventory Panel Sprite")]
    public static void ApplyPanelSprite()
    {
        string abs = ProjectPath(PanelSpritePath);
        if (!File.Exists(abs)) { Debug.LogError("InventoryUIBuilder: " + PanelSpritePath + " not found."); return; }

        AssetDatabase.ImportAsset(PanelSpritePath, ImportAssetOptions.ForceUpdate);
        var imp = AssetImporter.GetAtPath(PanelSpritePath) as TextureImporter;
        if (imp != null)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.maxTextureSize = 2048;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PanelSpritePath);
        var panel = GameObject.Find("InventoryCanvas/Root/Panel");
        if (sprite == null || panel == null) { Debug.LogError("InventoryUIBuilder: sprite or panel missing."); return; }

        var img = panel.GetComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Simple;
        img.preserveAspect = true;
        img.color = Color.white;
        img.enabled = true;

        var ph = panel.transform.Find("Placeholder");
        if (ph != null) ph.gameObject.SetActive(false);

        float ratio = (float)sprite.texture.width / sprite.texture.height;
        float expected = RefW / RefH;
        if (Mathf.Abs(ratio - expected) / expected > 0.02f)
            Debug.LogWarning("InventoryUIBuilder: the image is " + sprite.texture.width + "x" + sprite.texture.height + ", the layout was measured on 1221x1288. Slots may be slightly off.");
        else
            Debug.Log("InventoryUIBuilder: panel artwork applied (" + sprite.texture.width + "x" + sprite.texture.height + ").");

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    // ------------------------------------------------------------------ helpers

    static string ProjectPath(string assetPath)
    {
        return Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static void SlotRect(int i, out float x0, out float y0)
    {
        x0 = 67f + 147.5f * (i % 5);
        y0 = 288f + 153.5f * (i / 5);
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    /// <summary>Places a rect using pixel coordinates of the design image (origin top-left).</summary>
    static void SetRef(RectTransform rt, float x0, float y0, float x1, float y1)
    {
        rt.anchorMin = new Vector2(x0 / RefW, 1f - y1 / RefH);
        rt.anchorMax = new Vector2(x1 / RefW, 1f - y0 / RefH);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static Image NewImage(string name, Transform parent, Color color, bool raycast)
    {
        var rt = NewRect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, TextAlignmentOptions align, Color color, FontStyles style = FontStyles.Normal)
    {
        var rt = NewRect(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.alignment = align;
        t.color = color;
        t.fontStyle = style;
        t.raycastTarget = false;
        return t;
    }

    /// <summary>Simple gold-bordered dark box, used only until the real artwork is applied.</summary>
    static void Frame(Transform parent, string name, float x0, float y0, float x1, float y1, float border)
    {
        var outer = NewImage(name, parent, Gold, false);
        SetRef(outer.rectTransform, x0, y0, x1, y1);
        var inner = NewImage("Fill", outer.transform, Dark, false);
        Stretch(inner.rectTransform, border);
    }

    // ------------------------------------------------------------------ item icons (generated)

    static Sprite MakeIconSprite(ItemType item)
    {
        const int N = 128;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        var px = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N * 2f - 1f;
                float v = (y + 0.5f) / N * 2f - 1f;     // v points up
                px[y * N + x] = ShadePixel(item, u, v);
            }
        tex.SetPixels(px);
        tex.Apply();

        string path = IconDir + "/Item_" + item + ".png";
        File.WriteAllBytes(ProjectPath(path), tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.maxTextureSize = 128;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static float Cov(float signedDist) { return Mathf.Clamp01(0.5f - signedDist * 64f); }   // 1 px soft edge

    static Color ShadePixel(ItemType item, float u, float v)
    {
        switch (item)
        {
            case ItemType.Wood: return Log(u, v);
            case ItemType.Stone: return Rock(u, v);
            case ItemType.Herb: return Herb(u, v);
            case ItemType.Ruby: return Gem(u, v, new Color(0.9f, 0.1f, 0.22f));
            case ItemType.Emerald: return Gem(u, v, new Color(0.1f, 0.74f, 0.36f));
            case ItemType.Sapphire: return Gem(u, v, new Color(0.15f, 0.4f, 0.96f));
            case ItemType.Amber: return Gem(u, v, new Color(0.98f, 0.6f, 0.1f));
            default: return Color.clear;
        }
    }

    static Color Log(float u, float v)
    {
        float r = Mathf.Sqrt(u * u + v * v);
        float a = Cov(r - 0.82f);
        if (a <= 0f) return Color.clear;
        Color bark = new Color(0.33f, 0.2f, 0.1f);
        Color wood = new Color(0.82f, 0.62f, 0.36f);
        float rings = 0.5f + 0.5f * Mathf.Sin(r * 26f + 0.6f);
        wood = Color.Lerp(wood, wood * 0.78f, rings * 0.6f);
        if (r < 0.07f) wood = new Color(0.45f, 0.28f, 0.14f);
        Color c = Color.Lerp(wood, bark, Mathf.SmoothStep(0.64f, 0.7f, r));
        c *= 1f + 0.12f * (-u + v) * 0.5f;
        c.a = a;
        return c;
    }

    static Color Rock(float u, float v)
    {
        float r = Mathf.Sqrt(u * u + v * v);
        float th = Mathf.Atan2(v, u);
        float R = 0.78f + 0.07f * Mathf.Sin(3f * th + 1f) + 0.045f * Mathf.Sin(5f * th + 2.3f) + 0.03f * Mathf.Sin(8f * th);
        float a = Cov((r - R) * 0.9f);
        if (a <= 0f) return Color.clear;
        float light = Mathf.Clamp(-u * 0.5f + v * 0.8f, -1f, 1f);
        Color c = new Color(0.58f, 0.58f, 0.62f) * (0.78f + 0.3f * light * Mathf.Clamp01(r / R));
        if (Mathf.Abs(Mathf.Sin(u * 9f + v * 5f)) < 0.04f && r < R * 0.9f) c *= 0.75f;
        c *= Mathf.Lerp(1f, 0.7f, Mathf.SmoothStep(0.8f * R, R, r));
        c.a = a;
        return c;
    }

    static float LeafDist(float u, float v, float cx, float cy, float deg, out float along)
    {
        float th = deg * Mathf.Deg2Rad;
        float px = u - cx, py = v - cy;
        along = px * Mathf.Sin(th) + py * Mathf.Cos(th);
        float perp = px * Mathf.Cos(th) - py * Mathf.Sin(th);
        const float L = 0.78f, W = 0.36f;
        float w = W * Mathf.Max(0f, 1f - (along / L) * (along / L));
        return Mathf.Max(Mathf.Abs(along) - L, (Mathf.Abs(perp) - w) * 0.8f);
    }

    static Color Herb(float u, float v)
    {
        float a1, a2;
        float d1 = LeafDist(u, v, -0.2f, 0.1f, 30f, out a1);
        float d2 = LeafDist(u, v, 0.2f, 0.1f, -30f, out a2);
        // stem: capsule from (0,-0.88) to (0,-0.1)
        float sy = Mathf.Clamp(v, -0.88f, -0.1f);
        float ds = Mathf.Sqrt(u * u + (v - sy) * (v - sy)) - 0.045f;

        float sd = Mathf.Min(d1, Mathf.Min(d2, ds));
        float a = Cov(sd);
        if (a <= 0f) return Color.clear;

        Color c;
        if (ds <= d1 && ds <= d2)
        {
            c = new Color(0.4f, 0.5f, 0.2f);
        }
        else
        {
            bool first = d1 <= d2;
            float along = first ? a1 : a2;
            float perpAbs = Mathf.Abs(first ? (u + 0.2f) * Mathf.Cos(30f * Mathf.Deg2Rad) - (v - 0.1f) * Mathf.Sin(30f * Mathf.Deg2Rad)
                                            : (u - 0.2f) * Mathf.Cos(-30f * Mathf.Deg2Rad) - (v - 0.1f) * Mathf.Sin(-30f * Mathf.Deg2Rad));
            c = Color.Lerp(new Color(0.18f, 0.5f, 0.2f), new Color(0.5f, 0.9f, 0.4f), (along / 0.78f + 1f) * 0.5f);
            if (perpAbs < 0.018f) c = new Color(0.72f, 1f, 0.62f);
        }
        c.a = a;
        return c;
    }

    static readonly Vector2[] GemPts =
    {
        new Vector2(-0.88f, 0.28f), new Vector2(-0.5f, 0.78f), new Vector2(0.5f, 0.78f),
        new Vector2(0.88f, 0.28f), new Vector2(0f, -0.88f)
    };

    static Color Gem(float u, float v, Color baseColor)
    {
        // convex polygon, clockwise: outward normal of an edge A->B is (-dy, dx)
        float sd = -999f;
        for (int i = 0; i < GemPts.Length; i++)
        {
            Vector2 A = GemPts[i], B = GemPts[(i + 1) % GemPts.Length];
            Vector2 d = (B - A).normalized;
            Vector2 n = new Vector2(-d.y, d.x);
            sd = Mathf.Max(sd, Vector2.Dot(new Vector2(u, v) - A, n));
        }
        float a = Cov(sd);
        if (a <= 0f) return Color.clear;

        float k;
        if (v > 0.28f)
        {
            int seg = Mathf.Clamp(Mathf.FloorToInt((u + 0.88f) / 0.44f), 0, 3);
            float[] crown = { 0.85f, 1.15f, 1.0f, 0.8f };
            k = crown[seg];
            float f = ((u + 0.88f) / 0.44f) - Mathf.Floor((u + 0.88f) / 0.44f);
            if (f < 0.03f || f > 0.97f) k *= 0.75f;
        }
        else
        {
            k = u < 0f ? 0.78f : 0.98f;
            if (Mathf.Abs(u) < 0.12f) k = 1.2f;
            if (Mathf.Abs(v - 0.28f) < 0.014f) k *= 0.7f;
        }
        k *= 0.9f + 0.2f * (v + 1f) * 0.5f;
        if (sd > -0.05f) k *= 0.65f;

        Color c = baseColor * k;
        float spec = Mathf.Clamp01(1f - Mathf.Sqrt((u + 0.28f) * (u + 0.28f) + (v - 0.52f) * (v - 0.52f)) / 0.11f);
        c = Color.Lerp(c, Color.white, spec * 0.6f);
        c.a = a;
        return c;
    }
}
