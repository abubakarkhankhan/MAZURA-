using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XInput;
using UnityEngine.InputSystem.DualShock;

namespace FlipGameDev.ModularCharacterCtrl.UI
{
    public class DeviceDetectionModule : MonoBehaviour, IUiModule
    {
        private UiSystemBrain brain;

        public void Initialize(UiSystemBrain brain)
        {
            this.brain = brain;
            
            if (brain.PlayerInput != null)
            {
                brain.PlayerInput.onControlsChanged += OnControlsChanged;
                OnControlsChanged(brain.PlayerInput); 
            }
        }

        public void Tick() { }

        public void CleanUp()
        {
            if (brain != null && brain.PlayerInput != null)
            {
                brain.PlayerInput.onControlsChanged -= OnControlsChanged;
            }
        }

        private void OnControlsChanged(PlayerInput input)
        {
            if (input.devices.Count == 0) return;

            InputDevice activeDevice = input.devices[0];

            foreach (var device in input.devices)
            {
                if (device is Gamepad || device is Keyboard)
                {
                    activeDevice = device;
                    break;
                }
            }

            ControllerType detectedType = DetermineControllerType(activeDevice);
            brain.State.SetController(detectedType);
        }

        private ControllerType DetermineControllerType(InputDevice device)
        {
            if (device is Keyboard || device is Mouse) return ControllerType.Keyboard;
            if (device is XInputController) return ControllerType.XboxGamepad;
            if (device is DualShockGamepad || device.name.Contains("DualSense")) return ControllerType.PlaystationGamepad;

            if (device is Gamepad gamepad)
            {
                string displayName = gamepad.displayName.ToLower();
                if (displayName.Contains("playstation") || displayName.Contains("ps")) return ControllerType.PlaystationGamepad;
                return ControllerType.XboxGamepad;
            }

            return ControllerType.Unknown;
        }
    }
}