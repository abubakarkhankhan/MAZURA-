using UnityEngine;
using System.Collections;

namespace FlipGameDev.BobCharacterFramework.EyesController
{
    public class EyeController : MonoBehaviour{
        [Header("References - Eyes (Bones)")]
        public Transform leftEye;
        public Transform rightEye;
        
        [Header("Bone Orientation (Blender)")]
        public Vector3 eyeForwardAxis = new Vector3(0, 1, 0);
        public Vector3 eyeUpAxis = new Vector3(0, 0, 1);

        [Header("Foco / Alvo")]
        public Transform lookTarget;
        public float headForwardDistance = 5f; 
        public Transform headBone;
        
        [Header("Limites de Visão (Ângulos)")]
        public float maxHorizontalAngle = 45f;
        public float maxVerticalAngle = 30f;
        
        [Header("Saccades (Micro-movimentos)")]
        public float saccadeRadius = 0.1f;
        public float minSaccadeTime = 0.5f;
        public float maxSaccadeTime = 2.0f;
        private Vector3 targetSaccadeOffset = Vector3.zero;
        private Vector3 saccadeVelocity = Vector3.zero; 
        public float saccadeSmoothTime = 0.05f;
        public float eyeMoveSpeed = 15f;

        [Header("Referências - Pálpebras (Ossos)")]
        public Transform leftUpperEyelid;
        public Transform leftLowerEyelid;
        public Transform rightUpperEyelid;
        public Transform rightLowerEyelid;

        [Header("Configuração do Piscar")]
        public Vector3 upperLidCloseOffset = new Vector3(45f, 0f, 0f);
        public Vector3 lowerLidCloseOffset = new Vector3(-10f, 0f, 0f);

        public float minBlinkInterval = 2f;
        public float maxBlinkInterval = 6f;
        public float blinkSpeed = 20f;

        private Vector3 currentSaccadeOffset = Vector3.forward;
        private float saccadeTimer;
        
        private bool isBlinking = false;
        private float currentBlinkWeight = 0f;

        private Quaternion lUpperOpen, lLowerOpen;
        private Quaternion rUpperOpen, rLowerOpen;

        private Quaternion lUpperClosed, lLowerClosed;
        private Quaternion rUpperClosed, rLowerClosed;

        void Start()
        {
            InitializeEyelids();

            saccadeTimer = Random.Range(minSaccadeTime, maxSaccadeTime);
            StartCoroutine(BlinkRoutine());
        }

        void LateUpdate()
        {
            HandleEyeMovement();
            HandleBlinking();
        }

        private void InitializeEyelids()
        {
            Quaternion upperOffset = Quaternion.Euler(upperLidCloseOffset);
            Quaternion lowerOffset = Quaternion.Euler(lowerLidCloseOffset);

            if (leftUpperEyelid)
            {
                lUpperOpen = leftUpperEyelid.localRotation;
                lUpperClosed = lUpperOpen * upperOffset;
            }
            if (leftLowerEyelid)
            {
                lLowerOpen = leftLowerEyelid.localRotation;
                lLowerClosed = lLowerOpen * lowerOffset;
            }
            if (rightUpperEyelid)
            {
                rUpperOpen = rightUpperEyelid.localRotation;
                rUpperClosed = rUpperOpen * upperOffset;
            }
            if (rightLowerEyelid)
            {
                rLowerOpen = rightLowerEyelid.localRotation;
                rLowerClosed = rLowerOpen * lowerOffset;
            }
        }

        private void HandleEyeMovement()
        {
            if (leftEye == null || rightEye == null || headBone == null) return;

            Vector3 mainTargetPos = lookTarget != null 
                ? lookTarget.position 
                : headBone.position + (headBone.forward * headForwardDistance);

            float distToTarget = Vector3.Distance(headBone.position, mainTargetPos);

            saccadeTimer -= Time.deltaTime;
            if (saccadeTimer <= 0)
            {
                Vector2 randomCircle = Random.insideUnitCircle * saccadeRadius;
                
                targetSaccadeOffset = (headBone.right * randomCircle.x + headBone.up * randomCircle.y) * (distToTarget * 0.2f);
                
                saccadeTimer = Random.Range(minSaccadeTime, maxSaccadeTime);
            }

            currentSaccadeOffset = Vector3.SmoothDamp(currentSaccadeOffset, targetSaccadeOffset, ref saccadeVelocity, saccadeSmoothTime);

            Vector3 finalLookPosition = mainTargetPos + currentSaccadeOffset;

            Vector3 leftSafeDirection = ClampEyeDirection(leftEye.position, finalLookPosition);
            Vector3 rightSafeDirection = ClampEyeDirection(rightEye.position, finalLookPosition);
            
            RotateEye(leftEye, leftSafeDirection);
            RotateEye(rightEye, rightSafeDirection);
        }

        private Vector3 ClampEyeDirection(Vector3 eyePosition, Vector3 targetPosition)
        {
            Vector3 rawDirection = (targetPosition - eyePosition).normalized;
            if (rawDirection == Vector3.zero) return headBone.forward;

            Vector3 localDir = headBone.InverseTransformDirection(rawDirection);

            float yaw = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Asin(Mathf.Clamp(localDir.y, -1f, 1f)) * Mathf.Rad2Deg;

            yaw = Mathf.Clamp(yaw, -maxHorizontalAngle, maxHorizontalAngle);
            pitch = Mathf.Clamp(pitch, -maxVerticalAngle, maxVerticalAngle);

            Vector3 clampedLocalDir = Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward;

            return headBone.TransformDirection(clampedLocalDir);
        }
        
        private void RotateEye(Transform eye, Vector3 direction)
        {
            if (direction == Vector3.zero) return;

            Quaternion standardLook = Quaternion.LookRotation(direction, headBone.up);
            Quaternion axisCorrection = Quaternion.Inverse(Quaternion.LookRotation(eyeForwardAxis, eyeUpAxis));
            Quaternion targetRotation = standardLook * axisCorrection;

            float t = 1f - Mathf.Exp(-eyeMoveSpeed * Time.deltaTime);

            eye.rotation = Quaternion.Slerp(eye.rotation, targetRotation, t);
        }

        private void HandleBlinking()
        {
            float targetWeight = isBlinking ? 1f : 0f;
            
            float t = 1f - Mathf.Exp(-blinkSpeed * Time.deltaTime);
            currentBlinkWeight = Mathf.Lerp(currentBlinkWeight, targetWeight, t);

            if (leftUpperEyelid) 
                leftUpperEyelid.localRotation = Quaternion.Slerp(lUpperOpen, lUpperClosed, currentBlinkWeight);
                
            if (leftLowerEyelid) 
                leftLowerEyelid.localRotation = Quaternion.Slerp(lLowerOpen, lLowerClosed, currentBlinkWeight);
                
            if (rightUpperEyelid) 
                rightUpperEyelid.localRotation = Quaternion.Slerp(rUpperOpen, rUpperClosed, currentBlinkWeight);
                
            if (rightLowerEyelid) 
                rightLowerEyelid.localRotation = Quaternion.Slerp(rLowerOpen, rLowerClosed, currentBlinkWeight);
        }

        private IEnumerator BlinkRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(minBlinkInterval, maxBlinkInterval));

                isBlinking = true;
                yield return new WaitForSeconds(0.1f);
                isBlinking = false;

                if (Random.value < 0.2f)
                {
                    yield return new WaitForSeconds(0.15f);
                    isBlinking = true;
                    yield return new WaitForSeconds(0.1f);
                    isBlinking = false;
                }
            }
        }
    }
}