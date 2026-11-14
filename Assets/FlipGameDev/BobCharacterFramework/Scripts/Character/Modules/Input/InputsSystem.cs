using FlipGameDev.BobCharacterFramework.Common;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FlipGameDev.BobCharacterFramework.Character
{
    [RequireComponent(typeof(PlayerInput))]
    public class InputsSystem : MonoBehaviour, ICharacterModule, IInputProvider
    {
        private CharacterBrain brain;
        private PlayerInput playerInput;
        
        public Vector3 MoveInput { get; private set; }
        public Vector3 LookInput { get; private set; }
        public bool IsAnalogMovement { get; private set; }
        public bool IsMouseInput { get; private set; }
        
        public bool SprintHold { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool CrouchHold { get; private set; }
        public bool InteractPressed { get; private set; }
        public bool ChangeModePressed { get; private set; }
        
        public bool PedestalUp { get; private set; }
        public bool PedestalDown { get; private set; }
        public float ZoomDeltaY { get; private set; }
        
        private void Awake()
        {
            playerInput = GetComponent<PlayerInput>();
        }

        private void OnEnable()
        {
            // O PlayerInput gerencia o Enable/Disable sozinho, então não precisamos chamar Enable()
            // Nos inscrevemos nos eventos buscando a ação pelo nome exato dela:
            
            playerInput.actions["ChangeMode"].started += OnChangeModePressed;
            playerInput.actions["Interact"].started += OnInteractPressed;
            playerInput.actions["Jump"].started += OnJumpPressed;
            
            playerInput.actions["Move"].performed += OnMovePerformed;
            playerInput.actions["Look"].performed += OnLookPerformed;
            playerInput.actions["Sprint"].performed += OnSprintPerformed;
            playerInput.actions["Crouch"].performed += OnCrouchPerformed;
            playerInput.actions["Pedestal_Up"].performed += OnPedestalUpPerformed;
            playerInput.actions["Pedestal_Down"].performed += OnPedestalDownPerformed;
            playerInput.actions["ZoomDeltaY"].performed += ZoomDeltaYOnperformed;
            playerInput.actions["ZoomIn"].performed += ZoomInOnperformed;
            playerInput.actions["ZoomOut"].performed += ZoomOutOnperformed;
            
            playerInput.actions["Move"].canceled += OnMoveCanceled;
            playerInput.actions["Look"].canceled += OnLookCanceled;
            playerInput.actions["Sprint"].canceled += OnSprintCanceled;
            playerInput.actions["Crouch"].canceled += OnCrouchCanceled;
            playerInput.actions["Pedestal_Up"].canceled += OnPedestalUpCanceled;
            playerInput.actions["Pedestal_Down"].canceled += OnPedestalDownCanceled;
            playerInput.actions["ZoomDeltaY"].canceled += ZoomDeltaYCanceled;
            playerInput.actions["ZoomIn"].canceled += ZoomInCanceled;
            playerInput.actions["ZoomOut"].canceled += ZoomOutCanceled;
        }

        private void OnDisable()
        {
            playerInput.actions["ChangeMode"].started -= OnChangeModePressed;
            playerInput.actions["Interact"].started -= OnInteractPressed;
            playerInput.actions["Jump"].started -= OnJumpPressed;
            
            playerInput.actions["Move"].performed -= OnMovePerformed;
            playerInput.actions["Look"].performed -= OnLookPerformed;
            playerInput.actions["Sprint"].performed -= OnSprintPerformed;
            playerInput.actions["Crouch"].performed -= OnCrouchPerformed;
            playerInput.actions["Pedestal_Up"].performed -= OnPedestalUpPerformed;
            playerInput.actions["Pedestal_Down"].performed -= OnPedestalDownPerformed;
            playerInput.actions["ZoomDeltaY"].performed -= ZoomDeltaYOnperformed;
            playerInput.actions["ZoomIn"].performed -= ZoomInOnperformed;
            playerInput.actions["ZoomOut"].performed -= ZoomOutOnperformed;
            
            playerInput.actions["Move"].canceled -= OnMoveCanceled;
            playerInput.actions["Look"].canceled -= OnLookCanceled;
            playerInput.actions["Sprint"].canceled -= OnSprintCanceled;
            playerInput.actions["Crouch"].canceled -= OnCrouchCanceled;
            playerInput.actions["Pedestal_Up"].canceled -= OnPedestalUpCanceled;
            playerInput.actions["Pedestal_Down"].canceled -= OnPedestalDownCanceled;
            playerInput.actions["ZoomDeltaY"].canceled -= ZoomDeltaYCanceled;
            playerInput.actions["ZoomIn"].canceled -= ZoomInCanceled;
            playerInput.actions["ZoomOut"].canceled -= ZoomOutCanceled;
        }

        #region STARTED ACTIONS
        private void OnChangeModePressed(InputAction.CallbackContext obj) => ChangeModePressed = true;
        private void OnInteractPressed(InputAction.CallbackContext obj) => InteractPressed = true;
        private void OnJumpPressed(InputAction.CallbackContext obj) => JumpPressed = true;
        #endregion

        #region PERFORMED ACTIONS
        private void OnMovePerformed(InputAction.CallbackContext ctx)
        {
            Vector2 input = ctx.ReadValue<Vector2>();
            MoveInput = new Vector3(input.x, 0, input.y);
            IsAnalogMovement = !(ctx.control.device is Keyboard);
        }
        private void OnLookPerformed(InputAction.CallbackContext ctx)
        {
            Vector2 input = ctx.ReadValue<Vector2>();
            LookInput = new Vector3(input.x, 0, input.y);
            IsMouseInput = (ctx.control.device is Mouse);
        } 
        private void OnSprintPerformed(InputAction.CallbackContext ctx) =>  SprintHold = true;   
        private void OnCrouchPerformed(InputAction.CallbackContext obj) => CrouchHold = true;
        private void OnPedestalUpPerformed(InputAction.CallbackContext obj) => PedestalUp = true;
        private void OnPedestalDownPerformed(InputAction.CallbackContext obj) => PedestalDown = true;
        private void ZoomDeltaYOnperformed(InputAction.CallbackContext ctx) => ZoomDeltaY = ctx.ReadValue<float>();
        private void ZoomInOnperformed(InputAction.CallbackContext obj) => ZoomDeltaY = -1f;
        private void ZoomOutOnperformed(InputAction.CallbackContext obj) => ZoomDeltaY = 1f;
        #endregion

        #region CANCELED ACTIONS
        private void OnMoveCanceled(InputAction.CallbackContext obj) => MoveInput = Vector3.zero;
        private void OnLookCanceled(InputAction.CallbackContext obj) => LookInput = Vector3.zero;  
        private void OnSprintCanceled(InputAction.CallbackContext obj) => SprintHold = false;
        private void OnCrouchCanceled(InputAction.CallbackContext obj) => CrouchHold = false;
        private void OnPedestalUpCanceled(InputAction.CallbackContext obj) => PedestalUp = false;
        private void OnPedestalDownCanceled(InputAction.CallbackContext obj) => PedestalDown = false;
        private void ZoomDeltaYCanceled(InputAction.CallbackContext obj) => ZoomDeltaY = 0f;
        private void ZoomInCanceled(InputAction.CallbackContext obj) => ZoomDeltaY = 0f;
        private void ZoomOutCanceled(InputAction.CallbackContext obj) => ZoomDeltaY = 0f;
        #endregion

        private void LateUpdate()
        {
            ChangeModePressed = false;
            InteractPressed = false;
            JumpPressed = false;
        }

        public void Initialize(CharacterBrain brain)
        {
            this.brain = brain;
        }

        public void Tick()
        {
            this.brain.State.MoveInput = MoveInput;
            this.brain.State.LookInput = LookInput; 
            this.brain.State.IsAnalogMovement = IsAnalogMovement;
            this.brain.State.IsMouseInput = IsMouseInput;
            this.brain.State.SprintHold = SprintHold;
            this.brain.State.JumpPressed = JumpPressed;
            this.brain.State.CrouchHold = CrouchHold;
            this.brain.State.InteractPressed = InteractPressed;
            this.brain.State.ChangeModePressed = ChangeModePressed;
            this.brain.State.PedestalUp = PedestalUp;
            this.brain.State.PedestalDown = PedestalDown;
            this.brain.State.ZoomDeltaY = ZoomDeltaY;

            CalcInputMagnitude();
        }

        private void CalcInputMagnitude()
        {
            float magnitude = MoveInput.magnitude;

            if (magnitude < 0.001f) 
            {
                brain.State.InputMagnitude = 1f;
            }
            else 
            {
                brain.State.InputMagnitude = IsAnalogMovement ? magnitude : 1f;
            }
        }
    }
}