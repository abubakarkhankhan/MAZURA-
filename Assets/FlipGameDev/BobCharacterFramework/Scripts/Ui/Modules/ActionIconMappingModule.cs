using UnityEngine;
using UnityEngine.UI;

namespace FlipGameDev.ModularCharacterCtrl.UI
{
    public class ActionIconMappingModule : MonoBehaviour, IUiModule
    {
        [System.Serializable]
        public struct IconMapping
        {
            public string actionName;
            public Image targetImage;
        }

        [SerializeField] private IconMapping[] mappings;

        private UiSystemBrain brain;
        private IconDatabaseModule database;

        public void Initialize(UiSystemBrain brain)
        {
            this.brain = brain;
            this.database = brain.GetModule<IconDatabaseModule>();

            if (database == null)
            {
                Debug.LogError("[ActionIconMappingModule] Required IconDatabaseModule not found in UiSystemBrain.");
                return;
            }

            brain.State.OnControllerChanged += UpdateIcons;
            UpdateIcons(brain.State.CurrentController);
        }

        public void Tick() { }

        public void CleanUp()
        {
            if (brain != null)
                brain.State.OnControllerChanged -= UpdateIcons;
        }

        private void UpdateIcons(ControllerType currentController)
        {
            foreach (var mapping in mappings)
            {
                if (mapping.targetImage == null) continue;

                Sprite newSprite = database.GetSpriteForAction(mapping.actionName, currentController);
                if (newSprite != null)
                {
                    mapping.targetImage.sprite = newSprite;
                }
            }
        }
    }
}