namespace FlipGameDev.ModularCharacterCtrl.UI
{
    public interface IUiModule
    {
        void Initialize(UiSystemBrain brain);
        void Tick();
        void CleanUp();
    }
}