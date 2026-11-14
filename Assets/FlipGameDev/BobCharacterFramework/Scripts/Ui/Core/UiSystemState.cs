using System;
using FlipGameDev.BobCharacterFramework.Common;

namespace FlipGameDev.ModularCharacterCtrl.UI
{
    public class UiSystemState
    {
        public ControllerType CurrentController { get; private set; } = ControllerType.Unknown;
        public CameraMode CurrentCameraMode { get; private set; }
        public int PlayerCount { get; set; } = 0;

        public event Action<ControllerType> OnControllerChanged;
        public event Action<CameraMode> OnCameraModeChanged;

        public void SetController(ControllerType newController)
        {
            if (CurrentController != newController)
            {
                CurrentController = newController;
                OnControllerChanged?.Invoke(CurrentController);
            }
        }

        public void SetCameraMode(CameraMode newMode)
        {
            if (CurrentCameraMode != newMode)
            {
                CurrentCameraMode = newMode;
                OnCameraModeChanged?.Invoke(CurrentCameraMode);
            }
        }
    }
}