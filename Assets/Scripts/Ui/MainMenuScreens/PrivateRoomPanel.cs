using TMPro;
using UIArchitecture;
using UnityEngine;
using UnityEngine.UI;

namespace Ui.MainMenuScreens
{
    public class PrivateRoomPanel : MonoBehaviour
    {
        public Button creatPrivateRoomButton;
        public Button joinPrivateRoomButton;
        public TMP_InputField privateRoomCodeInput;


        private void OnEnable()
        {
            
            creatPrivateRoomButton.onClick.AddListener(OnClickCreatRoomButton);
            joinPrivateRoomButton.onClick.AddListener(OnClickJoinRoomButton);
        }

        private void OnDisable()
        {
            creatPrivateRoomButton.onClick.RemoveListener(OnClickCreatRoomButton);
            joinPrivateRoomButton.onClick.RemoveListener(OnClickJoinRoomButton);
        }

        private void OnClickJoinRoomButton()
        {
            if (string.IsNullOrEmpty(privateRoomCodeInput.text))
            {
                UiManager.Instance.ShowToast("Please enter room code.");
                return;
            }
            
            UiManager.Instance.ShowPanel(UiScreenName.MatchMakingPanel,
                new MatchMakingPanelData() { IsPrivate = true, RoomName = privateRoomCodeInput.text.Trim() });
        }

        private void OnClickCreatRoomButton()
        {
            UiManager.Instance.ShowPanel(UiScreenName.MatchMakingPanel,
                new MatchMakingPanelData() { IsPrivate = true, RoomName = "" });
        }
    }
}