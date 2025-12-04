using Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MainMenu
{
    public class TeamCardUi : MonoBehaviour
    {
        [Header("UI References")]
        public TMP_Text teamNameText;
        public Image bgImage;

        [Header("Colors")]
        public Color myTeamColor;
        public Color myTeamNameColor;
        public Color defaultColor;
        public Color defaultTeamNameColor;

        [Header("Player Card Setup")]
        public Transform container;
        public LobbyPlayerCard playerCardPrefab;

        private GameResult.TeamData _teamData;

        public void Setup(GameResult.TeamData teamData)
        {
            _teamData = teamData;
            teamNameText.text = teamData.teamName;
            if (teamData.isMyTeam)
            {
                bgImage.color = myTeamColor;
                teamNameText.color = myTeamNameColor;
            }
            else
            {
                bgImage.color = defaultColor;
                teamNameText.color = defaultTeamNameColor;
            }

            foreach (Transform child in container)
                Destroy(child.gameObject);

            foreach (var playerInfo in teamData.players)
            {
                var card = Instantiate(playerCardPrefab, container);
                card.Setup(playerInfo);
            }
        }
    }
}