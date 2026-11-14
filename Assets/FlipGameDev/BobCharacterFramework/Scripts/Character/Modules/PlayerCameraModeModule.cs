using FlipGameDev.BobCharacterFramework.Camera;
using FlipGameDev.BobCharacterFramework.Common;
using Unity.Cinemachine;
using UnityEngine;

namespace FlipGameDev.BobCharacterFramework.Character
{
    public class PlayerCameraModeModule : MonoBehaviour, ICharacterModule
    {
        [SerializeField]private CameraMode startMode = CameraMode.TPS;
        
        [Header("Targets")]
        [SerializeField]private CinemachineBrain mainCamera;
        [SerializeField]private Transform fpsCamTarget;
        [SerializeField]private Transform tpsCamTarget;

        [Header("Instantiation")]
        [SerializeField]private GameObject vCamsPrefab;
        private CharacterBrain brain;
        public CameraBrain CameraBrain { get; private set; }

        public void Initialize(CharacterBrain brain)
        {
            if (vCamsPrefab != null)
            {
                if(CameraBrain != null) return;
                
                this.brain = brain;
                brain.State.CurrentMode = startMode;
                
                brain.State.mainCamera = mainCamera;
                brain.State.fpsCamTarget = fpsCamTarget;
                brain.State.tpsCamTarget = tpsCamTarget;

                GameObject camInstance = Instantiate(vCamsPrefab, transform.position, Quaternion.identity);
                CameraBrain = camInstance.GetComponent<CameraBrain>();
                CameraBrain.OnInstantiate(brain);
            }
            else
            {
                Debug.LogWarning("CharacterBrain: No camera prefab assigned. Please assign one to ensure cameras are set up correctly.");
            }
        }

        public void Tick()
        {
            if (brain.State.ChangeModePressed)
            {
                brain.ChangeMode();
            }
        }

    }
}