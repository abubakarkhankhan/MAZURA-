using UnityEngine;
using TMPro;
using FlipGameDev.BobCharacterFramework.Common;

namespace FlipGameDev.ModularCharacterCtrl.UI
{
    public class CameraModeUiModule : MonoBehaviour, IUiModule
    {[Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI modeText;
        [SerializeField] private GameObject rotateIcon;
        [SerializeField] private GameObject jumpIcon;
        [SerializeField] private GameObject crouchIcon;
        [SerializeField] private GameObject sprintIcon;
        [SerializeField] private GameObject elevationIcon;
        [SerializeField] private GameObject zoomIcon;

        private UiSystemBrain brain;

        public void Initialize(UiSystemBrain brain)
        {
            this.brain = brain;
            brain.State.OnCameraModeChanged += UpdateLayoutForCameraMode;
        }

        public void Tick()
        {
            if (brain.CharacterBrain != null)
            {
                brain.State.SetCameraMode(brain.CharacterBrain.State.CurrentMode);
            }
        }

        public void CleanUp()
        {
            if (brain != null)
                brain.State.OnCameraModeChanged -= UpdateLayoutForCameraMode;
        }

        private void UpdateLayoutForCameraMode(CameraMode newMode)
        {
            if (modeText != null) modeText.text = newMode.ToString();

            SetVisibility(rotateIcon, newMode != CameraMode.TopDown);
            SetVisibility(jumpIcon, newMode != CameraMode.PhotoMode);
            SetVisibility(crouchIcon, newMode != CameraMode.PhotoMode);
            SetVisibility(sprintIcon, newMode != CameraMode.PhotoMode);
            SetVisibility(elevationIcon, newMode == CameraMode.PhotoMode);
            SetVisibility(zoomIcon, newMode == CameraMode.TPS);
        }

        private void SetVisibility(GameObject element, bool isVisible)
        {
            if (element != null && element.activeSelf != isVisible)
                element.SetActive(isVisible);
        }
    }
}