using UnityEngine;

namespace FlipGameDev.BobCharacterFramework.Common
{
    public interface IInputProvider
    {
        Vector3 MoveInput { get; }
        Vector3 LookInput { get; }
        bool IsAnalogMovement { get; }
        bool IsMouseInput { get; }
        
        bool SprintHold { get; }
        bool JumpPressed { get; }
        bool CrouchHold { get; }
        bool InteractPressed { get; }
        bool ChangeModePressed { get; }
        
        bool PedestalUp { get; }
        bool PedestalDown { get; }
        float ZoomDeltaY { get; }
    }
}