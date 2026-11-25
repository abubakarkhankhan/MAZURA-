using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Many systems create themselves with [RuntimeInitializeOnLoadMethod(AfterSceneLoad)]. Unity calls those
/// only once, when the game starts. When a scene is loaded again (Try Again / Play Again / from the main menu)
/// this calls them again, so every system is rebuilt for the new scene.
/// </summary>
public static class SceneBoot
{
    static int loads;
    static MethodInfo[] boots;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        loads = 0;
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
    }

    static void OnLoaded(Scene s, LoadSceneMode mode)
    {
        loads++;
        if (loads == 1 || mode != LoadSceneMode.Single) return;   // Unity already ran them for the first scene
        if (Object.FindAnyObjectByType<ToolController>() == null) return;   // the main menu has no player: no game systems there
        if (boots == null) Collect();
        foreach (MethodInfo m in boots)
        {
            try { m.Invoke(null, null); }
            catch (System.Exception e) { Debug.LogWarning("SceneBoot: " + m.DeclaringType.Name + "." + m.Name + " failed: " + (e.InnerException != null ? e.InnerException.Message : e.Message)); }
        }
    }

    static void Collect()
    {
        System.Collections.Generic.List<MethodInfo> list = new System.Collections.Generic.List<MethodInfo>();
        foreach (System.Type t in typeof(SceneBoot).Assembly.GetTypes())
            foreach (MethodInfo m in t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (m.GetParameters().Length != 0) continue;
                object[] a = m.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                if (a.Length == 0) continue;
                RuntimeInitializeOnLoadMethodAttribute r = (RuntimeInitializeOnLoadMethodAttribute)a[0];
                if (r.loadType == RuntimeInitializeLoadType.AfterSceneLoad) list.Add(m);
            }
        boots = list.ToArray();
    }
}
