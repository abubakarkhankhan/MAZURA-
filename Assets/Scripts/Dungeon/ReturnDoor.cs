using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>The way back up to the forest, next to where the player arrives in the dungeon.</summary>
public class ReturnDoor : MonoBehaviour
{
    WorldPrompt prompt;

    void Start() { prompt = new WorldPrompt(transform, 2.5f, WorldPrompt.Key("Return to the forest")); }

    void Update()
    {
        bool near = WorldPrompt.PlayerNear(transform.position, 2.4f);
        prompt.Tick(near);
        if (near && WorldPrompt.CanAct() && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            if (DungeonQuest.Instance != null) DungeonQuest.Instance.LeaveDungeon();
    }
}
