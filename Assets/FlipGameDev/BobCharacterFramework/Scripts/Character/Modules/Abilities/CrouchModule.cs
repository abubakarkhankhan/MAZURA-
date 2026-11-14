using FlipGameDev.BobCharacterFramework.Common;
using UnityEngine;

namespace FlipGameDev.BobCharacterFramework.Character
{
    public class CrouchModule : MonoBehaviour, ICharacterModule
    {
        public float crouchSpeed = 1.5f;
        
        [Space(20)]
        public float crouchHeight = 1.0f;
        public float standingHeight = 2.0f;
        public Vector3 crouchCenter = new Vector3(0, 0.5f, 0);
        public Vector3 standingCenter = new Vector3(0, 1.0f, 0);
        
        [Space(20)]
        [Tooltip("Collider height transition velocity")]
        public float crouchSmoothTime = 10f;
        
        private CharacterBrain brain;
        private CharacterController controller;

        public void Initialize(CharacterBrain brain)
        {
            this.brain = brain;
            controller = GetComponent<CharacterController>();
            controller.height = standingHeight;
            controller.center = standingCenter;
        }

        public void Tick()
        {
            bool isPhotoMode = brain.State.CurrentMode == CameraMode.PhotoMode;
            if (isPhotoMode) return;

            if (brain.State.CrouchHold) brain.State.TargetSpeed = crouchSpeed;
            
            float targetHeight = brain.State.CrouchHold ? crouchHeight : standingHeight;
            Vector3 targetCenter = brain.State.CrouchHold ? crouchCenter : standingCenter;
            
            controller.height = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * crouchSmoothTime);
            controller.center = Vector3.Lerp(controller.center, targetCenter, Time.deltaTime * crouchSmoothTime);
            
        }
    }
}