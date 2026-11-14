using FlipGameDev.BobCharacterFramework.Common;
using UnityEngine;

namespace FlipGameDev.BobCharacterFramework.Camera
{
    public class FPSCameraModule : ICameraModule
    {
        private float mouseSensitivity = 0.3f;
        private float stickSensitivity = 200f;
        private float topClamp = 90f;
        private float bottomClamp = -90f;
        private const float _threshold = 0.01f;

        public bool HandlesMode(CameraMode mode)
        {
            return mode == CameraMode.FPS;
        }

        public void OnEnter(CameraBrain brain)
        {
            brain.fpsCam.Priority = 10;
            brain.State.mainCamera.SetCameraPriority(brain.fpsCam, brain.fpsCam);
            //brain.fpsCam.Follow = brain.State.fpsCamTarget;
            
            Vector3 euler = brain.State.fpsCamTarget.eulerAngles;
            brain.State.yaw = euler.y;
            brain.State.pitch = euler.x;
        }

        public void UpdateModule(CameraBrain brain)
        {
            Vector2 lookInput = new Vector2(brain.State.LookInput.x, brain.State.LookInput.z);
            if (lookInput.sqrMagnitude >= _threshold)
            {
                CameraState state = brain.State;

                float sensitivity = state.IsMouseInput ? mouseSensitivity : stickSensitivity;
                float deltaTimeMultiplier = state.IsMouseInput ? 1.0f : Time.deltaTime;

                state.pitch -= state.LookInput.z * sensitivity * deltaTimeMultiplier;
                state.yaw = state.LookInput.x * sensitivity * deltaTimeMultiplier;

                state.pitch = CameraUtils.ClampAngle(state.pitch, bottomClamp, topClamp);

                brain.State.fpsCamTarget.localRotation = Quaternion.Euler(state.pitch, 0, 0);
                brain.CharacterBrain.transform.Rotate(Vector3.up * state.yaw);
            }
        }

        public void OnExit(CameraBrain brain) => brain.fpsCam.Priority = 0;

    }
}