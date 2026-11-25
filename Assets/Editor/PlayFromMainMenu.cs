using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Pressing Play in the editor always starts the game like the real build:
/// loading screen -> main menu -> game. Turn it off with Tools > Mazura > Play From Main Menu.
/// </summary>
[InitializeOnLoad]
public static class PlayFromMainMenu
{
    const string Key = "Mazura.PlayFromMainMenu";
    const string Menu = "Tools/Mazura/Play From Main Menu";
    const string ScenePath = "Assets/Scenes/MainMenu.unity";

    static PlayFromMainMenu() { EditorApplication.delayCall += Apply; }

    static bool On { get { return EditorPrefs.GetBool(Key, true); } }

    static void Apply()
    {
        EditorSceneManager.playModeStartScene = On ? AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) : null;
        Menu_Check();
    }

    [MenuItem(Menu)]
    static void Toggle()
    {
        EditorPrefs.SetBool(Key, !On);
        Apply();
    }

    static void Menu_Check() { UnityEditor.Menu.SetChecked(Menu, On); }
}
