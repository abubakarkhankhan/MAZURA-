using FlipGameDev.BobCharacterFramework.Common;
using UnityEngine;

namespace FlipGameDev.BobCharacterFramework.Character
{
    public class TopDownMovement : MonoBehaviour, IMovementModule
    {
        public float rotationSmoothTime = 0.1f;
        
        private float _rotationVelocity;
        
        private CharacterBrain brain;

        public bool CanHandle(CameraMode mode)
        {
            return mode == CameraMode.TopDown;
        }

        public void Initialize(CharacterBrain brain)
        {
            this.brain = brain;
            
            bool active = CanHandle(brain.State.CurrentMode);
            if (active) Cursor.lockState = CursorLockMode.Locked;
        }

        public void Tick()
        {
            if (!CanHandle(brain.State.CurrentMode)) return;

            var input = brain.State.MoveInput;

            if (input == Vector3.zero)
            {
                brain.State.MoveDirection = Vector3.zero;
                return;
            }

            Vector3 direction = new Vector3(input.x, 0, input.z);
            
            float targetRotation = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetRotation, ref _rotationVelocity, rotationSmoothTime);
            transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);

            brain.State.MoveDirection = direction;
        }
    }
}