using UnityEngine;

namespace FlipGameDev.BobCharacterFramework.HairMovement
{
    public class SecondaryMotionController : MonoBehaviour
    {
        public SecondaryBoneChain[] chains;

        void Start()
        {
            foreach (var c in chains)
                c.Initialize();
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;

            foreach (var c in chains)
                c.UpdateChain(dt);
        }
        
    }
}