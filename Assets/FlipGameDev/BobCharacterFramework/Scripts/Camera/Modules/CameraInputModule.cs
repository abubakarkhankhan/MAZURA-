using FlipGameDev.BobCharacterFramework.Common;

namespace FlipGameDev.BobCharacterFramework.Camera
{
    public class CameraInputModule : ICameraModule
    {
        public bool HandlesMode(CameraMode mode)
        {
            return true;
        }

        public void OnEnter(CameraBrain brain)
        {
            
        }

        public void OnExit(CameraBrain brain)
        {
            
        }

        public void UpdateModule(CameraBrain brain)
        {
            if(!brain.CharacterBrain) return;
            
            brain.State.MoveInput = brain.CharacterBrain.State.MoveInput;
            brain.State.LookInput = brain.CharacterBrain.State.LookInput;
            brain.State.IsAnalogMovement = brain.CharacterBrain.State.IsAnalogMovement;
            brain.State.IsMouseInput = brain.CharacterBrain.State.IsMouseInput;
            brain.State.SprintHold = brain.CharacterBrain.State.SprintHold;
            brain.State.JumpPressed = brain.CharacterBrain.State.JumpPressed;
            brain.State.CrouchHold = brain.CharacterBrain.State.CrouchHold;
            brain.State.InteractPressed = brain.CharacterBrain.State.InteractPressed;
            brain.State.ChangeModePressed = brain.CharacterBrain.State.ChangeModePressed;
            brain.State.PedestalUp = brain.CharacterBrain.State.PedestalUp;
            brain.State.PedestalDown = brain.CharacterBrain.State.PedestalDown;
            brain.State.ZoomDeltaY = brain.CharacterBrain.State.ZoomDeltaY;

        }
    }
}