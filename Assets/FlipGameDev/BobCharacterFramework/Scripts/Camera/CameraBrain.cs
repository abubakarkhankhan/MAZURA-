using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using FlipGameDev.BobCharacterFramework.Character;
using FlipGameDev.BobCharacterFramework.Common;

namespace FlipGameDev.BobCharacterFramework.Camera
{
    public class CameraBrain : MonoBehaviour
    {
        public CameraState State { get; private set; } = new CameraState();
        
        [SerializeField] private CharacterBrain characterBrain;

        [Header("Cinemachine Cameras")]
        public CinemachineCamera fpsCam;
        public CinemachineCamera tpsCam;
        public CinemachineCamera topDownCam;
        public CinemachineCamera photoModeCam;
        
        [Header("Settings")]
        public LayerMask collisionMask;
        private enum CallType { OnEnter, Update, OnExit }

        private List<ICameraModule> modules = new List<ICameraModule>();
        public CharacterBrain CharacterBrain{  get => characterBrain; private set{ characterBrain = value; }}
        
        public void OnInstantiate(CharacterBrain characterBrain)
        {
            CharacterBrain = characterBrain;
        }

        private void Start()
        {
            modules.Add(new FPSCameraModule());
            modules.Add(new TPSCameraModule());
            modules.Add(new TopDownCameraModule());
            modules.Add(new PhotoModeCameraModule());
            modules.Add(new CameraInputModule());

            SyncInitialProperties();

            CallModules(CallType.OnEnter);

        }

        private void LateUpdate()
        {
            CallModules(CallType.Update);
        }

        private void OnDestroy()
        {
            if (CharacterBrain != null)
                CharacterBrain.OnModeChanged -= SwitchMode;
        }

        private void SwitchMode(CameraMode newMode)
        {
            if (State.CurrentMode == newMode) return;
            
            CallModules(CallType.OnExit);

            State.CurrentMode = newMode; // <<<<< 

            CallModules(CallType.OnEnter);
        }

        private void CallModules(CallType callType)
        {
            foreach (var module in modules)
            {
                if (module.HandlesMode(State.CurrentMode)){
                    if (callType == CallType.OnEnter) module.OnEnter(this);
                    else if (callType == CallType.Update) module.UpdateModule(this);
                    else if (callType == CallType.OnExit) module.OnExit(this);
                }
            }
        }

        private void SyncInitialProperties()
        {
            ResetAllCameras();

            if (CharacterBrain != null)
            {
                State.CurrentMode = CharacterBrain.State.CurrentMode;

                State.mainCamera = CharacterBrain.State.mainCamera;
                State.fpsCamTarget = CharacterBrain.State.fpsCamTarget;
                State.tpsCamTarget = CharacterBrain.State.tpsCamTarget;

                fpsCam.Follow = State.fpsCamTarget;
                tpsCam.Follow = State.tpsCamTarget;
                topDownCam.Follow = State.tpsCamTarget;

                CharacterBrain.OnModeChanged += SwitchMode;
            }
            else
            {
                State.CurrentMode = CameraMode.PhotoMode;
            }
        }

        private void ResetAllCameras()
        {
            if (fpsCam != null) fpsCam.Priority = 0;
            if (tpsCam != null) tpsCam.Priority = 0;
            if (topDownCam != null) topDownCam.Priority = 0;
            if (photoModeCam != null) photoModeCam.Priority = 0;
        }
    }
}