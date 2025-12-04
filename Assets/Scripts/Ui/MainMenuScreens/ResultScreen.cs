using Cysharp.Threading.Tasks;
using Data;
using MainMenu;
using Managers;
using TMPro;
using UIArchitecture;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Object = System.Object;

namespace Ui.MainMenuScreens
{
    public class ResultScreen : FullScreenView
    {
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text winLoseText;
        [SerializeField] private TeamCardUi teamA;
        [SerializeField] private TeamCardUi teamB;

        private GameResult _resultData; 
        
        [SerializeField]
        private Color[] winColors = new Color[]
        {
            new Color(0.2f, 0.8f, 0.3f), // Green
            new Color(1f, 0.84f, 0f),   // Gold
            new Color(0.3f, 0.7f, 1f),  // Blue
            new Color(0.9f, 0.3f, 0.9f) // Purple
        };
        
        protected override void Initialize(Object obj)
        {
            if (obj is GameResult gameResult)
            {
                _resultData = gameResult;
                teamA.Setup(_resultData.teamA);
                teamB.Setup(_resultData.teamB);
                
                // Credit winner or log loser using Supabase currency system
                var lobbyData = GameManager.JoinedSessionData;
                if (lobbyData != null)
                {
                    CurrencyManager.CreditGameReward(
                        lobbyData.ID,
                        lobbyData.Fee,
                        _resultData.reward,
                        _resultData.isLocalPlayerWinner
                    ).Forget();
                }
                
                if (_resultData.isLocalPlayerWinner)
                {
                    winLoseText.text = $"You Won!";
                    if (_resultData.reward > 0)
                    {
                        var randomColor = winColors[Random.Range(0, winColors.Length)];
                        string colorHex = ColorUtility.ToHtmlStringRGB(randomColor);
                        winLoseText.text = $"You Won! <color=#{colorHex}>+{_resultData.reward} FZ</color>";
                    }
                }
                else
                {
                    winLoseText.text = $"You Lost";
                }
            }
            else
            {
                GameLogger.ShowLog($"Game Result not found", GameLogger.LogType.Error);
            }
            closeButton.onClick.AddListener(OnClickClose);
        }

        protected override void Cleanup()
        {
            closeButton.onClick.RemoveListener(OnClickClose);
        }

        private async void OnClickClose()
        {
            await GameManager.RefreshPlayerData();
            UiManager.Instance.HidePanel(this);
        }
        
        
    }
}
