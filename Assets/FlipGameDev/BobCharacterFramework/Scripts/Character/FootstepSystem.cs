using UnityEngine;

namespace FlipGameDev.BobCharacterFramework.Character
{
    public class FootstepSystem : MonoBehaviour
    {
        public AudioClip landingClip;
        public AudioClip[] footstepClips;
        public AudioSource footstepSource;
        public AudioSource LandSource;
        
        private void OnFootstep(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                if (footstepClips.Length > 0)
                {
                    var index = Random.Range(0, footstepClips.Length);
                    footstepSource.PlayOneShot(footstepClips[index]);
                }
            }
        }

        private void OnLand(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                LandSource.PlayOneShot(landingClip);
            }
        }
    }
}