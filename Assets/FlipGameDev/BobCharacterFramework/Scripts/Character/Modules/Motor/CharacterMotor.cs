using FlipGameDev.BobCharacterFramework.Common;
using UnityEngine;

namespace FlipGameDev.BobCharacterFramework.Character
{
    public class CharacterMotor : MonoBehaviour, ICharacterModule
    {
        public float walkSpeed = 3.0f;
        public float sprintSpeed = 6.0f;
        public float speedChangeRate = 10.0f; 
        
        private CharacterBrain brain;
        private CharacterController controller;

        public void Initialize(CharacterBrain brain)
        {
            this.brain = brain;
            controller = GetComponent<CharacterController>();
        }

        public void Tick()
        {
            if (!brain.State.CrouchHold)
            {
                if (brain.State.SprintHold) 
                {
                    brain.State.TargetSpeed = sprintSpeed;
                }else
                {
                    brain.State.TargetSpeed = walkSpeed;
                }
            }

            if (brain.State.MoveInput.x == 0 && brain.State.MoveInput.z == 0) brain.State.TargetSpeed = 0;
            
            SmoothSpeed();

            Vector3 moveDirection = brain.State.MoveDirection;
            
            controller.Move(moveDirection.normalized * (brain.State.Speed * Time.deltaTime) + 
                            new Vector3(0.0f, brain.State.VerticalVelocity, 0.0f) * Time.deltaTime);
            
        }

        private void SmoothSpeed()
        {
            if(brain.State.CurrentMode == CameraMode.PhotoMode) 
            {
                brain.State.Speed = 0f; 
                return;
            }

            float speedOffset = 0.1f;
            float currentHorizontalSpeed = new Vector3(controller.velocity.x, 0.0f, controller.velocity.z).magnitude;

            if (currentHorizontalSpeed < brain.State.TargetSpeed - speedOffset ||
                currentHorizontalSpeed > brain.State.TargetSpeed + speedOffset)
            {
                brain.State.Speed = Mathf.Lerp(
                    brain.State.Speed, 
                    brain.State.TargetSpeed * brain.State.InputMagnitude, 
                    Time.deltaTime * speedChangeRate);

                brain.State.Speed = Mathf.Round(brain.State.Speed * 1000f) / 1000f;
            }
            else
            {
                brain.State.Speed = brain.State.TargetSpeed;
            }

            brain.State.AnimationBlend = Mathf.Lerp(brain.State.AnimationBlend, brain.State.TargetSpeed, Time.deltaTime * speedChangeRate);
                if (brain.State.AnimationBlend < 0.01f) brain.State.AnimationBlend = 0f;
        }
    }
}