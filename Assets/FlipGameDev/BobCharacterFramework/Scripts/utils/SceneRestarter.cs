using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace FlipGameDev.BobCharacterFramework.Utils
{
    public class SceneRestarter : MonoBehaviour
    {
        private InputAction reloadAction;

        private void OnEnable()
        {
            reloadAction ??= new InputAction("ReloadScene", binding: "<Keyboard>/p");
            reloadAction.performed += OnReloadPerformed;
            reloadAction.Enable();
        }

        private void OnDisable()
        {
            if (reloadAction == null)
                return;

            reloadAction.performed -= OnReloadPerformed;
            reloadAction.Disable();
        }

        private void OnReloadPerformed(InputAction.CallbackContext context)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}