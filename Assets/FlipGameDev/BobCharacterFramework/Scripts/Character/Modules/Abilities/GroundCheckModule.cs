using UnityEngine;

namespace FlipGameDev.BobCharacterFramework.Character
{
    public class GroundCheckModule : MonoBehaviour, ICharacterModule
    {
        public float GroundedOffset = -0.14f;
        public float GroundedRadius = 0.28f;
        public LayerMask GroundLayers;
        
        private CharacterBrain brain;


        public void Initialize(CharacterBrain brain)
        {
            this.brain = brain;
        }

        public void Tick()
        {
            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);
            bool groundedCheck = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);

            if (brain.State.Grounded != groundedCheck)
            {
                brain.State.Grounded = groundedCheck;
            }
        }
        
        private void OnDrawGizmosSelected()
        {
            Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
            Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

            if (brain == null) return;
            if (brain.State.Grounded) Gizmos.color = transparentGreen;
            else Gizmos.color = transparentRed;

            Gizmos.DrawSphere(
                new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z),
                GroundedRadius);
        }
    }
}