using UnityEngine;

namespace FlipGameDev.ModularCharacterCtrl.UI
{
    public class IconDatabaseModule : MonoBehaviour, IUiModule
    {
        [System.Serializable]
        public class ButtonSprites
        {
            public string actionName;
            public Sprite keyboardSprite;
            public Sprite xboxSprite;
            public Sprite playstationSprite;

            public Sprite GetSprite(ControllerType type) => type switch
            {
                ControllerType.XboxGamepad => xboxSprite,
                ControllerType.PlaystationGamepad => playstationSprite,
                _ => keyboardSprite
            };
        }

        [SerializeField] private ButtonSprites[] buttonSprites = new ButtonSprites[0];

        public void Initialize(UiSystemBrain brain) { }
        public void Tick() { }
        public void CleanUp() { }

        public Sprite GetSpriteForAction(string actionName, ControllerType controllerType)
        {
            var data = System.Array.Find(buttonSprites, b => b.actionName == actionName);
            return data?.GetSprite(controllerType);
        }
    }
}