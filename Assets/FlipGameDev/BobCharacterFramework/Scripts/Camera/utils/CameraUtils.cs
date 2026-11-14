using Unity.Cinemachine;
using UnityEngine;

namespace FlipGameDev.BobCharacterFramework.Camera
{
    public static class CameraUtils
    {
        public static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }
        public static void SetCameraPriority(this CinemachineBrain brain, ICinemachineCamera camA, ICinemachineCamera camB)
        {
            int layer = 1;
            int priority = 1;
            float weight = 1f;
            float blendTime = 1f;
            brain.SetCameraOverride(layer, priority, camA, camB, weight, blendTime);
        }
    }
}