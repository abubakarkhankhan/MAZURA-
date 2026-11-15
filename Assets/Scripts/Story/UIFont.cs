using System.Reflection;
using TMPro;
using UnityEngine;

/// <summary>The game's UI font (the Workshop font), shared by the runtime-built screens.</summary>
public static class UIFont
{
    static TMP_FontAsset cached;

    public static TMP_FontAsset Get()
    {
        if (cached != null) return cached;
        if (WorkshopUI.Instance != null)
        {
            FieldInfo f = typeof(WorkshopUI).GetField("font", BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null) cached = f.GetValue(WorkshopUI.Instance) as TMP_FontAsset;
        }
        if (cached == null)
        {
            Font src = Resources.Load<Font>("WorkshopUI/Alegreya-SemiBold");
            if (src != null) cached = TMP_FontAsset.CreateFontAsset(src);
        }
        if (cached == null) cached = TMP_Settings.defaultFontAsset;
        return cached;
    }
}
