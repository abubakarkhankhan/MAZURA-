using System;
using FlipGameDev.BobCharacterFramework.Common;
using UnityEngine;

namespace FlipGameDev.BobCharacterFramework.Character
{
    public class AnimationModule : MonoBehaviour, ICharacterModule
    {
        public Animator animator;
        private CharacterBrain brain;

        public void Initialize(CharacterBrain brain)
        {
            this.brain = brain;
            if (!animator)
            {
                try
                {
                    animator = GetComponentInChildren<Animator>();
                }
                catch (Exception e)
                {
                    Debug.LogError($" AnimationModule: Animator not found on {gameObject.name} or its children. Exception: {e}");
                    return;
                }
            }
        }

        public void Tick()
        {
            bool isPhotoMode = brain.State.CurrentMode == CameraMode.PhotoMode;

            animator.SetFloat("Speed", brain.State.AnimationBlend);
            animator.SetFloat("MotionSpeed", brain.State.InputMagnitude);
            animator.SetBool("Grounded", brain.State.Grounded);
            animator.SetBool("isCrouch", brain.State.CrouchHold && !isPhotoMode);

            animator.SetBool("Jump", brain.State.Jump);
            animator.SetBool("FreeFall", brain.State.FreeFall);
            
        }
    }
}