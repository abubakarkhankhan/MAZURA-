using UnityEngine;

namespace FlipGameDev.BobCharacterFramework.Dialogue
{
    public class JawAudioSync : MonoBehaviour
    {
        [Header("References")]
        public Transform jawBone;
        public AudioSource audioSource;

        [Header("Rotation Config")]
        public Vector3 jawOpenOffset = new Vector3(20f, 0f, 0f);
        
        [Header("Audio Config (Lip Sync)")]
        public float sensitivity = 15f;
        public float smoothSpeed = 15f;
        public float noiseThreshold = 0.002f;

        private Quaternion closedRotation;
        private Quaternion openRotation;
        private float currentJawWeight = 0f;

        private float[] audioSamples = new float[256];

        void Start()
        {
            if (jawBone != null)
            {
                closedRotation = jawBone.localRotation;
                openRotation = closedRotation * Quaternion.Euler(jawOpenOffset);
            }
        }

        void LateUpdate()
        {
            if (jawBone == null) return;

            float targetWeight = 0f;

            if (audioSource.isPlaying)
            {
                audioSource.GetOutputData(audioSamples, 0);

                float sum = 0f;
                for (int i = 0; i < audioSamples.Length; i++)
                {
                    sum += audioSamples[i] * audioSamples[i];
                }
                float rmsValue = Mathf.Sqrt(sum / audioSamples.Length);

                if (rmsValue > noiseThreshold)
                {
                    targetWeight = rmsValue * sensitivity;
                }
            }

            targetWeight = Mathf.Clamp01(targetWeight);

            currentJawWeight = Mathf.Lerp(currentJawWeight, targetWeight, Time.deltaTime * smoothSpeed);

            jawBone.localRotation = Quaternion.Slerp(closedRotation, openRotation, currentJawWeight);
        }
    }
}