using FlipGameDev.BobCharacterFramework.Common;
using UnityEngine;

namespace FlipGameDev.BobCharacterFramework.Character
{
    public class TPSMovement : MonoBehaviour, IMovementModule
    {
        [SerializeField] private float rotationSmoothTime = 0.12f;

        private float _targetRotation = 0.0f;
        private float _rotationVelocity;
        private CharacterBrain brain;

        public bool CanHandle(CameraMode mode)
        {
            return mode == CameraMode.TPS;
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
            
            _targetRotation = Mathf.Atan2(input.x, input.z) * Mathf.Rad2Deg + brain.State.mainCamera.transform.eulerAngles.y;
            float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity, rotationSmoothTime);
            
            transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);

            brain.State.MoveDirection = Quaternion.Euler(0.0f, _targetRotation, 0.0f) * Vector3.forward;
        }
    }
}