using FlipGameDev.ModularCharacterCtrl.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FlipGameDev.BobCharacterFramework.Character
{
    public class UiSpawnerModule : MonoBehaviour, ICharacterModule
    {

        [SerializeField] private GameObject uiPrefab;
        private UiSystemBrain uiBrain;

        public void Initialize(CharacterBrain brain)
        {
            if(uiBrain != null) return;
            GameObject instance = Instantiate(uiPrefab, transform.position, Quaternion.identity);
            Canvas canvas = instance.GetComponent<Canvas>();
            uiBrain = instance.GetComponent<UiSystemBrain>();

            canvas.worldCamera = brain.State.mainCamera.OutputCamera;
            uiBrain.CharacterBrain = brain;
            uiBrain.PlayerInput = brain.GetComponent<PlayerInput>();
        }

        public void Tick()
        {
        }
    }
}