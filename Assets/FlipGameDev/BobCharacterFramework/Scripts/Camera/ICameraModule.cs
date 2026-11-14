using FlipGameDev.BobCharacterFramework.Common;

namespace FlipGameDev.BobCharacterFramework.Camera
{
    public interface ICameraModule
    {
        bool HandlesMode(CameraMode mode);
        void OnEnter(CameraBrain brain);
        void UpdateModule(CameraBrain brain);
        void OnExit(CameraBrain brain);
    }
}