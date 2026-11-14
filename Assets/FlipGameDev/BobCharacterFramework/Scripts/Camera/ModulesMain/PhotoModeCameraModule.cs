using FlipGameDev.BobCharacterFramework.Common;
using UnityEngine;

namespace FlipGameDev.BobCharacterFramework.Camera
{
    public class PhotoModeCameraModule : ICameraModule
    {
        private Vector3 _initialPosition;
        private Quaternion _initialRotation;
        private Vector3 _savedPosition;
        private Quaternion _savedRotation;
        
        
        private float moveSpeed = 2.5f;
        private float fastMoveMultiplier = 2f;
        private float mouseRotationSensitivity = 0.3f;
        private float stickRotationSensitivity = 200f;

        private Vector3 photoModeOffset = new Vector3(0f, 1.8f, -3.5f);
        private Vector3 playerLookOffset = new Vector3(0f, 1.2f, 0f);
        
        // Limites de Física
        private float maxDistanceLimit = 50f;
        private float sphereCastRadius = 0.3f;

        public bool HandlesMode(CameraMode mode)
        {
            return mode == CameraMode.PhotoMode;
        }

        public void OnEnter(CameraBrain brain)
        {
            Transform camTransform = brain.photoModeCam.transform;
            Transform playerTransform = brain.CharacterBrain != null ? brain.CharacterBrain.transform : null;

            _savedPosition = camTransform.position;
            _savedRotation = camTransform.rotation;

            if (playerTransform != null)
            {
                Vector3 lookTarget = playerTransform.position + playerTransform.up * playerLookOffset.y;
                _initialPosition = playerTransform.position + playerTransform.up * photoModeOffset.y - playerTransform.forward * Mathf.Abs(photoModeOffset.z);
                camTransform.position = _initialPosition;
                camTransform.rotation = Quaternion.LookRotation(lookTarget - _initialPosition, Vector3.up);
                _initialRotation = camTransform.rotation;
            }
            else
            {
                _initialPosition = camTransform.position;
                _initialRotation = camTransform.rotation;
            }
            
            //Synchronizes state to prevent rotational mouse jumps.
            brain.State.yaw = _initialRotation.eulerAngles.y;
            brain.State.pitch = _initialRotation.eulerAngles.x;

            brain.photoModeCam.Priority = 10;
            brain.State.mainCamera.SetCameraPriority(brain.photoModeCam, brain.photoModeCam);
        }

        public void UpdateModule(CameraBrain brain)
        {
            CameraState state = brain.State;
            Transform camTransform = brain.photoModeCam.transform;

            Rotation(state, camTransform);

            Vector3 targetPosition = MovimentBase(state, camTransform);
            targetPosition = DistCalc(targetPosition);
            targetPosition = CollisionCalc(brain, camTransform, targetPosition);

            camTransform.position = targetPosition;
        }

        public void OnExit(CameraBrain brain)
        {
            brain.photoModeCam.Priority = 0;

            Transform camTransform = brain.photoModeCam.transform;
            camTransform.position = _savedPosition;
            camTransform.rotation = _savedRotation;
        }

        private Vector3 CollisionCalc(CameraBrain brain, Transform camTransform, Vector3 targetPosition)
        {
            Vector3 deltaMove = targetPosition - camTransform.position;
            if (Physics.SphereCast(camTransform.position, sphereCastRadius, deltaMove.normalized, out RaycastHit hit, deltaMove.magnitude, brain.collisionMask))
            {
                targetPosition = camTransform.position + deltaMove.normalized * (hit.distance - 0.05f);
            }

            return targetPosition;
        }
        private Vector3 DistCalc(Vector3 targetPosition)
        {
            if (Vector3.Distance(_initialPosition, targetPosition) > maxDistanceLimit)
            {
                Vector3 directionFromStart = (targetPosition - _initialPosition).normalized;
                targetPosition = _initialPosition + directionFromStart * maxDistanceLimit;
            }

            return targetPosition;
        }
        private Vector3 MovimentBase(CameraState state, Transform camTransform)
        {
            Vector3 moveDirection = camTransform.forward * state.MoveInput.z + camTransform.right * state.MoveInput.x;

            if (state.PedestalUp) moveDirection = (moveDirection + Vector3.up) / 2;
            if (state.PedestalDown) moveDirection = (moveDirection + Vector3.down) / 2;

            if (moveDirection.sqrMagnitude > 1f) moveDirection.Normalize();

            float speed = moveSpeed * (state.IsMouseInput ? 1f : fastMoveMultiplier);
            Vector3 targetPosition = camTransform.position + moveDirection * speed * Time.deltaTime;
            return targetPosition;
        }
        private void Rotation(CameraState state, Transform camTransform)
        {
            float sensitivity = state.IsMouseInput ? mouseRotationSensitivity : stickRotationSensitivity;
            float deltaTimeMultiplier = state.IsMouseInput ? 1.0f : Time.deltaTime;
            state.yaw += state.LookInput.x * sensitivity * deltaTimeMultiplier;
            state.pitch -= state.LookInput.z * sensitivity * deltaTimeMultiplier;

            state.pitch = CameraUtils.ClampAngle(state.pitch, -89f, 89f); // Nao permite dar piruetas de ponta-cabeça
            camTransform.rotation = Quaternion.Euler(state.pitch, state.yaw, 0);
        }
    }
}