using FlipGameDev.BobCharacterFramework.Character;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FlipGameDev.ModularCharacterCtrl.UI
{
    public class UiSystemBrain : MonoBehaviour
    {
        public PlayerInput PlayerInput;
        public CharacterBrain CharacterBrain;

        public UiSystemState State { get; private set; } = new UiSystemState();

        private IUiModule[] modules;

        private void Awake()
        {
            modules = GetComponentsInChildren<IUiModule>();
        }

        void Start()
        {
            foreach (var module in modules)
            {
                module.Initialize(this);
            }
        }

        private void Update()
        {
            foreach (var module in modules)
            {
                module.Tick();
            }
        }

        private void OnDestroy()
        {
            if (modules == null) return;
            foreach (var module in modules)
            {
                module.CleanUp();
            }
        }

        public T GetModule<T>() where T : class, IUiModule
        {
            foreach (var module in modules)
            {
                if (module is T typedModule)
                    return typedModule;
            }
            return null;
        }
    }
}