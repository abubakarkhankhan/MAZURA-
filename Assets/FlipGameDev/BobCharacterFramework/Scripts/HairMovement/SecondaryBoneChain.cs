using UnityEngine;

namespace FlipGameDev.BobCharacterFramework.HairMovement
{
    [System.Serializable]
    public class SecondaryBoneChain
    {
        public Transform root;
        public SecondaryBone[] bones;

        [Header("Bone Axis")]
        public Vector3 boneAxis = Vector3.up; 
        
        [Header("Physics")]
        [Range(0f, 1f)] public float stiffness = 0.2f;
        [Range(0f, 1f)] public float damping = 0.1f;
        public Vector3 gravity = new Vector3(0, -0.01f, 0);

        [Header("Bounding")]
        [Range(0f, 90f)] public float maxSwingAngle = 30f; // cone angle

        [Header("Wind")]
        public float windStrength = 0.05f;
        public float windSpeed = 2f;

        public void Initialize()
        {
            for (int i = 0; i < bones.Length; i++)
            {
                var b = bones[i];
                if (b.bone == null) continue;

                b.initialLocalRotation = b.bone.localRotation;

                // Calculates bone size based on the child (if any).
                if (b.bone.childCount > 0)
                {
                    b.boneLength = Vector3.Distance(b.bone.position, b.bone.GetChild(0).position);
                }
                else
                {
                    b.boneLength = 0.1f; // Default value for the last bone
                }

                // Define the initial positions
                Vector3 tipPos = b.bone.position + (b.bone.rotation * boneAxis * b.boneLength);
                b.currentTipPos = tipPos;
                b.previousTipPos = tipPos;
            }
        }

        public void UpdateChain(float deltaTime)
        {
            if (deltaTime == 0) return;

            for (int i = 0; i < bones.Length; i++)
            {
                var b = bones[i];
                if (b.bone == null) continue;

                // 1. Discover where the bone would be if there were no physics (Rest pose)
                b.bone.localRotation = b.initialLocalRotation;
                Vector3 restTipPos = b.bone.position + (b.bone.rotation * boneAxis * b.boneLength);

                // 2. Calculate Wind (now uses the bone's position to create organic waves)
                float noiseX = Mathf.PerlinNoise(Time.time * windSpeed, b.bone.position.x) - 0.5f;
                float noiseZ = Mathf.PerlinNoise(b.bone.position.z, Time.time * windSpeed) - 0.5f;
                Vector3 wind = new Vector3(noiseX, 0, noiseZ) * windStrength;

                // 3. Physics Simulation (Verlet Integration)
                Vector3 velocity = (b.currentTipPos - b.previousTipPos) * (1f - damping);
                b.previousTipPos = b.currentTipPos; // Updates the previous position

                // Applies Forces (Spring + Gravity + Wind)
                Vector3 force = gravity + wind;
                force += (restTipPos - b.currentTipPos) * stiffness; // Spring effect pulling back to the original pose.

                b.currentTipPos += velocity + force;

                // 4. Balance Limit (Cone)
                Vector3 restDirection = (restTipPos - b.bone.position).normalized;
                Vector3 simulatedDirection = (b.currentTipPos - b.bone.position).normalized;
                
                float angle = Vector3.Angle(restDirection, simulatedDirection);
                if (angle > maxSwingAngle)
                {
                    // The movement is locked if the maximum angle is exceeded.
                    simulatedDirection = Vector3.RotateTowards(restDirection, simulatedDirection, maxSwingAngle * Mathf.Deg2Rad, 0f);
                }

                // 5. Length restriction (Keeps the tip at the same distance from the bone)
                b.currentTipPos = b.bone.position + (simulatedDirection * b.boneLength);

                // 6. Apply the Rotation to the Bone
                Quaternion targetRotation = Quaternion.FromToRotation(b.bone.rotation * boneAxis, simulatedDirection) * b.bone.rotation;
                b.bone.rotation = targetRotation;
            }
        }
    }
}