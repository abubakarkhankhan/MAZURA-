namespace FlipGameDev.BobCharacterFramework.Character
{
    public interface ICharacterModule
    {
        void Initialize(CharacterBrain brain);
        void Tick();
    }
}