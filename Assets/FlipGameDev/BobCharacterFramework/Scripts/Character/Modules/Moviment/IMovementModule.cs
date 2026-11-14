using FlipGameDev.BobCharacterFramework.Common;

namespace FlipGameDev.BobCharacterFramework.Character
{
    public interface IMovementModule : ICharacterModule
    {
        bool CanHandle(CameraMode mode);
    }
}