using UnityEngine;

namespace FlipGameDev.BobCharacterFramework.HairMovement
{
    [System.Serializable]
    public class SecondaryBone
    {
        public Transform bone;
        
        [HideInInspector] public Vector3 currentTipPos;
        [HideInInspector] public Vector3 previousTipPos;
        [HideInInspector] public float boneLength;
        [HideInInspector] public Quaternion initialLocalRotation;
    }
}