using UnityEngine;
using UnityEngine.InputSystem;

namespace FlipGameDev.BobCharacterFramework.PlayerManager
{
    public class PlayerSpawn : MonoBehaviour
    {
        public Transform[] spawnPoints;
        public GameObject auxiliaryCamera;
        public static int playerCount = 0;

        public void OnPlayerJoined(PlayerInput playerInput)
        {
            playerInput.transform.position = spawnPoints[playerCount].position;
            playerInput.transform.rotation = spawnPoints[playerCount].rotation;
            playerCount++;

            if (auxiliaryCamera != null && auxiliaryCamera.activeSelf)
            {
                auxiliaryCamera.SetActive(false);
            }
        }

        private void OnDisable()
        {
            playerCount = 0;
        }
    }
}