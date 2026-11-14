using System;
using System.Collections;
using System.Collections.Generic;
using FlipGameDev.BobCharacterFramework.Character;
using UnityEngine;
using Random = UnityEngine.Random;

namespace FlipGameDev.BobCharacterFramework.Dialogue
{
    public class DialogueManager : MonoBehaviour
    {
        public AudioSource audioSource;

        [Header("Playlist of Speeches")]
        public List<AudioClip> dialogues;
        public float startDelay = 3f;
        public float delayBetweenAudios = 1.5f;
        public bool playRandomly = false;

        public bool playMutedClip = true;

        [Header("Interaction (Mute / Unmute)")]
        public KeyCode muteKey = KeyCode.X;
        public AudioClip farewellClip;
        public AudioClip welcomeClip;

        private bool isMuted = false;
        private int currentIndex = -1;
        private AudioClip interruptedClip;
        private Coroutine activeRoutine;
        private CharacterBrain characterBrain;

        private void Awake()
        {
            characterBrain = GetComponentInParent<CharacterBrain>();
        }

        void Start()
        {
            if (dialogues.Count > 0)
            {
                activeRoutine = StartCoroutine(StartSequence());
            }
            else
            {
                Debug.LogWarning("DIALOGUE MANAGER: The dialogue list is empty!");
            }
        }

        void Update()
        {
            if (characterBrain.State.InteractPressed) 
            {
                ToggleMute();
            }
        }

        private void ToggleMute()
        {
            isMuted = !isMuted;

            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
            }

            if (isMuted)
            {
                if (audioSource.isPlaying && audioSource.clip != welcomeClip && audioSource.clip != farewellClip)
                {
                    interruptedClip = audioSource.clip;
                }

                activeRoutine = StartCoroutine(MuteSequence());
            }
            else
            {
                activeRoutine = StartCoroutine(UnmuteSequence());
            }
        }

        private IEnumerator StartSequence()
        {
            yield return new WaitForSeconds(startDelay);
            
            yield return NormalPlaylistRoutine();
        }

        private IEnumerator MuteSequence()
        {
            audioSource.Stop();

            if (farewellClip != null && playMutedClip)
            {
                audioSource.clip = farewellClip;
                audioSource.Play();
                
                yield return new WaitWhile(() => audioSource.isPlaying);
            }
        }

        private IEnumerator UnmuteSequence()
        {
            audioSource.Stop();

            if (welcomeClip != null)
            {
                audioSource.clip = welcomeClip;
                audioSource.Play();
                
                yield return new WaitWhile(() => audioSource.isPlaying);
            }

            yield return NormalPlaylistRoutine();
        }

        private IEnumerator NormalPlaylistRoutine()
        {
            if (interruptedClip != null)
            {
                audioSource.clip = interruptedClip;
                audioSource.Play();
                interruptedClip = null; // Limpa a memória
                
                yield return new WaitWhile(() => audioSource.isPlaying);
                yield return new WaitForSeconds(delayBetweenAudios);
            }

            while (true)
            {
                if (playRandomly)
                {
                    currentIndex = Random.Range(0, dialogues.Count);
                }
                else
                {
                    currentIndex = (currentIndex + 1) % dialogues.Count;
                }

                audioSource.clip = dialogues[currentIndex];
                audioSource.Play();

                yield return new WaitWhile(() => audioSource.isPlaying);

                yield return new WaitForSeconds(delayBetweenAudios);
            }
        }
    }
}