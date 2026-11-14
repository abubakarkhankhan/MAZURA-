using FlipGameDev.BobCharacterFramework.Common;
using UnityEngine;

namespace FlipGameDev.BobCharacterFramework.Character
{
    public class PhotomodeModule : MonoBehaviour, IMovementModule
    {
        private CharacterBrain brain;

        public bool CanHandle(CameraMode mode)
        {
            return mode == CameraMode.PhotoMode;
        }

        public void Initialize(CharacterBrain brain)
        {
            this.brain = brain;
        }

        public void Tick()
        {
            if (!CanHandle(brain.State.CurrentMode)) return;
            brain.State.MoveDirection = Vector3.zero;
            
        }
    }
}