using Managers;
using Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MainMenu
{
    public class LobbyPlayerCard : MonoBehaviour
    {
        public TMP_Text username;
        public Image bgColorImage;
        public CanvasGroup canvasGroup;
        public Color localPlayerColor;

        public PlayerInfo PlayerInfo { get; private set; }

        public void Setup(PlayerInfo playerInfo)
        {
            PlayerInfo = playerInfo;
            username.text = playerInfo.Name;

            if (playerInfo.Id == GameManager.UserData.id.ToString())
                bgColorImage.color = localPlayerColor;
        }
    }
}