using UnityEngine;
using Unity.Cinemachine;
using FlipGameDev.BobCharacterFramework.Common;

namespace FlipGameDev.BobCharacterFramework.Camera
{
    public class TPSCameraModule : ICameraModule
    {
        private float mouseSensitivity = 0.3f;
        private float stickSensitivity = 200f;
        private float topClamp = 70f;
        private float bottomClamp = -30f;
        
        //Zoom
        private float zoomSpeed = 20f;
        private float minZoom = 1.5f;
        private float maxZoom = 5f;

        public bool HandlesMode(CameraMode mode)
        {
            return mode == CameraMode.TPS;
        }

        public void OnEnter(CameraBrain brain)
        {

            brain.tpsCam.Priority = 10;
            brain.State.mainCamera.SetCameraPriority(brain.tpsCam, brain.tpsCam);
            //brain.tpsCam.Follow = brain.State.tpsCamTarget;
            
            Vector3 euler = brain.State.tpsCamTarget.eulerAngles;
            brain.State.yaw = euler.y;
            brain.State.pitch = euler.x;
        }

        public void UpdateModule(CameraBrain brain)
        {
            CameraState state = brain.State;
            
            Rotation(state);
            Zoom(brain, state);
        }

        public void OnExit(CameraBrain brain) => brain.tpsCam.Priority = 0;

        private void Rotation(CameraState state)
        {
            float sensitivity = state.IsMouseInput ? mouseSensitivity : stickSensitivity;
            float deltaTimeMultiplier = state.IsMouseInput ? 1.0f : Time.deltaTime;

            state.yaw += state.LookInput.x * sensitivity * deltaTimeMultiplier;
            state.pitch -= state.LookInput.z * sensitivity * deltaTimeMultiplier;

            state.yaw = CameraUtils.ClampAngle(state.yaw, float.MinValue, float.MaxValue);
            state.pitch = CameraUtils.ClampAngle(state.pitch, bottomClamp, topClamp);

            state.tpsCamTarget.rotation = Quaternion.Euler(state.pitch, state.yaw, 0);
        }

        private void Zoom(CameraBrain brain, CameraState state)
        {
            if (Mathf.Abs(state.ZoomDeltaY) > 0.01f)
            {
                var thirdPersonFollow = brain.tpsCam.GetComponent<CinemachineThirdPersonFollow>();

                if (thirdPersonFollow != null)
                {
                    thirdPersonFollow.CameraDistance -= state.ZoomDeltaY * zoomSpeed * Time.deltaTime;
                    thirdPersonFollow.CameraDistance = Mathf.Clamp(thirdPersonFollow.CameraDistance, minZoom, maxZoom);
                }
            }
        }
    }
}