using UIArchitecture;
using UnityEngine;

namespace Ui.MainMenuScreens
{
    /// <summary>
    /// Simple Views wrapper for AccountDetailsPopup MonoBehaviour.
    /// This allows the UiManager to control the popup via the Views system
    /// while keeping AccountDetailsPopup as a standard MonoBehaviour.
    /// </summary>
    public class AccountDetailsPopupView : Views
    {
        protected override void Initialize(object obj)
        {
            // Just activate the GameObject - AccountDetailsPopup.Start() will handle initialization
            gameObject.SetActive(true);
        }

        protected override void Cleanup()
        {
            // Deactivate - AccountDetailsPopup.OnDestroy() will handle cleanup
            gameObject.SetActive(false);
        }

        public override void SetDefault()
        {
            // Ensure starts inactive to prevent flash on scene load
            gameObject.SetActive(false);
        }
    }
}
