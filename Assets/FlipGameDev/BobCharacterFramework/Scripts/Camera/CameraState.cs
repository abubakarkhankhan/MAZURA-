using FlipGameDev.BobCharacterFramework.Common;
using Unity.Cinemachine;
using UnityEngine;

namespace FlipGameDev.BobCharacterFramework.Camera
{
    public class CameraState: IInputProvider
    {
        public CameraMode CurrentMode;
        public float yaw;
        public float pitch;
        public Quaternion targetRotation;
        
        #region Input Properties
        public Vector3 MoveInput { get; set; }
        public Vector3 LookInput { get; set; }
        public bool IsAnalogMovement { get; set; }
        public bool IsMouseInput { get; set; }
        public bool SprintHold { get; set; }
        public bool JumpPressed { get; set; }
        public bool CrouchHold { get; set; }
        public bool InteractPressed { get; set; }
        public bool ChangeModePressed { get; set; }
        public bool PedestalUp { get; set; }
        public bool PedestalDown { get; set; }
        public float ZoomDeltaY { get; set; }
        #endregion

        #region Camera Properties
            public CinemachineBrain mainCamera;
            public Transform fpsCamTarget;
            public Transform tpsCamTarget;
        #endregion
    }
}