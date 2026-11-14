using FlipGameDev.BobCharacterFramework.Common;
using UnityEngine;
using UnityEngine.Rendering;

namespace FlipGameDev.BobCharacterFramework.Character
{
    public class FPSMovement : MonoBehaviour, IMovementModule
    {
        public SkinnedMeshRenderer headMesh;
        
        private CharacterBrain brain;

        public bool CanHandle(CameraMode mode)
        {
            return mode == CameraMode.FPS;
        }

        public void Initialize(CharacterBrain brain)
        {
            this.brain = brain;
            
            bool active = CanHandle(brain.State.CurrentMode);
            if (active) Cursor.lockState = CursorLockMode.Locked;
            if(headMesh) headMesh.shadowCastingMode = !active ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly;
        }

        public void Tick()
        {
            if (!CanHandle(brain.State.CurrentMode)) return;

            var input = brain.State.MoveInput;

            brain.State.MoveDirection =
                transform.right * input.x +
                transform.forward * input.z;
        }
    }
}