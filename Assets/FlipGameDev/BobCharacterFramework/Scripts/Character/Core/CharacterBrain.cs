using System;
using FlipGameDev.BobCharacterFramework.Common;
using UnityEngine;

namespace FlipGameDev.BobCharacterFramework.Character
{
    public class CharacterBrain : MonoBehaviour
    {
        private ICharacterModule[] modules;
        public event Action<CameraMode> OnModeChanged;
        public CharacterState State { get; private set; }
        
        private void Awake()
        {
            State = new CharacterState();
            modules = GetComponents<ICharacterModule>();
        }

        private void Start()
        {
            InitModules();
        }

        void Update()
        {
            foreach (var module in modules)
                module.Tick();
        }

        private void InitModules()
        {
            foreach (var module in modules)
            {
                module.Initialize(this);
            }
        }

        public void ChangeMode()
        {
            int modesCount = Enum.GetValues(typeof(CameraMode)).Length;
            State.CurrentMode = (CameraMode)(((int)State.CurrentMode + 1) % modesCount);

            InitModules();
            
            OnModeChanged?.Invoke(State.CurrentMode);
        }
    }
}