using UnityEngine;

namespace GamePlay.Ui
{
    /// <summary>
    /// Makes UI elements always face the camera, regardless of parent rotation.
    /// Attach this to any UI element (like message panels, text displays) that should always be readable.
    /// </summary>
    public class FaceCameraUI : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("If true, will flip the text 180 degrees (useful if text appears upside down)")]
        public bool flipText = false;

        [Tooltip("Update in LateUpdate (after all other updates) for smoother rotation")]
        public bool updateInLateUpdate = true;

        private Camera _mainCamera;
        private RectTransform _rectTransform;

        private void Start()
        {
            _mainCamera = Camera.main;
            _rectTransform = GetComponent<RectTransform>();

            if (_mainCamera == null)
            {
                Debug.LogWarning("FaceCameraUI: No main camera found. Component disabled.");
                enabled = false;
                return;
            }

            // Initial rotation to face camera
            FaceCamera();
        }

        private void Update()
        {
            if (!updateInLateUpdate)
            {
                FaceCamera();
            }
        }

        private void LateUpdate()
        {
            if (updateInLateUpdate)
            {
                FaceCamera();
            }
        }

        private void FaceCamera()
        {
            if (_mainCamera == null || _rectTransform == null)
                return;

            // Make the UI element face the camera
            Vector3 directionToCamera = _mainCamera.transform.position - transform.position;
            
            // Calculate rotation to face camera
            Quaternion targetRotation = Quaternion.LookRotation(-directionToCamera);

            // If flipText is enabled, add 180 degree rotation
            if (flipText)
            {
                targetRotation *= Quaternion.Euler(0, 180, 0);
            }

            // Apply rotation
            transform.rotation = targetRotation;
        }

        /// <summary>
        /// Call this to manually update the facing direction
        /// </summary>
        public void UpdateFacing()
        {
            FaceCamera();
        }
    }
}
