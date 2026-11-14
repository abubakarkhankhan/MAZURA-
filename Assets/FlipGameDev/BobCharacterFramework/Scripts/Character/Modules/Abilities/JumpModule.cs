using FlipGameDev.BobCharacterFramework.Common;
using UnityEngine;

namespace FlipGameDev.BobCharacterFramework.Character
{
    public class JumpModule : MonoBehaviour, ICharacterModule
    {
        private CharacterBrain brain;

        [Header("State Thresholds")]
        public float jumpTimeout = 0.1f;
        public float fallTimeout = 0.15f;
        
        public float jumpHeight = 1.2f;
        public float gravity = -15f;

        private float _fallTimeoutDelta;
        private float _jumpTimeoutDelta;
        private float _verticalVelocity;
        private float _terminalVelocity = 53.0f;
        
        public void Initialize(CharacterBrain brain)
        {
            this.brain = brain;
        }

        public void Tick()
        {
            bool canJump = !brain.State.CrouchHold;
            bool grounded = brain.State.Grounded;
            bool isPhotoMode = brain.State.CurrentMode == CameraMode.PhotoMode;

            if (grounded)
            {
                _fallTimeoutDelta = fallTimeout;
                brain.State.Jump = false;
                brain.State.FreeFall = false;

                if (_verticalVelocity < 0.0f) _verticalVelocity = -2f;

                if (brain.State.JumpPressed && _jumpTimeoutDelta <= 0.0f && canJump && !isPhotoMode)
                {
                    _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                    brain.State.Jump = true;
                }

                if (_jumpTimeoutDelta >= 0.0f) _jumpTimeoutDelta -= Time.deltaTime;
            }
            else
            {
                _jumpTimeoutDelta = jumpTimeout;
                if (_fallTimeoutDelta >= 0.0f) _fallTimeoutDelta -= Time.deltaTime;
                else brain.State.FreeFall = true;
            }

            if (_verticalVelocity < _terminalVelocity) _verticalVelocity += gravity * Time.deltaTime;
            brain.State.VerticalVelocity = _verticalVelocity;
        }
    }
}