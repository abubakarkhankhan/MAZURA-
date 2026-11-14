using FlipGameDev.BobCharacterFramework.Common;

namespace FlipGameDev.BobCharacterFramework.Camera
{
    public class TopDownCameraModule : ICameraModule
    {
        public bool HandlesMode(CameraMode mode)
        {
            return mode == CameraMode.TopDown;
        }
        
        public void OnEnter(CameraBrain brain)
        {
            brain.topDownCam.Priority = 10;
            brain.State.mainCamera.SetCameraPriority(brain.topDownCam, brain.topDownCam);

            //brain.topDownCam.Follow = brain.State.tpsCamTarget;
        }

        public void UpdateModule(CameraBrain brain)
        {
            // Without any input logic, Cinemachine performs Target Tracking automatically.
        }

        public void OnExit(CameraBrain brain)
        {
            brain.topDownCam.Priority = 0;
        }
    }
}