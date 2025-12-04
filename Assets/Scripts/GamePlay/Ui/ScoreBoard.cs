using DG.Tweening;
using GamePlay.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GamePlay.Ui
{
    public class ScoreBoard : MonoBehaviour
    {
        [SerializeField] private TMP_Text teamAName;
        [SerializeField] private TMP_Text teamBName;
        [SerializeField] private TMP_Text teamAScore;
        [SerializeField] private TMP_Text teamBScore;
        [SerializeField] private TMP_Text teamATrickCount;
        [SerializeField] private TMP_Text teamBTrickCount;

        public void SetScore(NetworkTeamData teamA, NetworkTeamData teamB, bool localPlayerIsInTeamA)
        {
            
            if (localPlayerIsInTeamA)
            {
                teamAName.text = "Us";
                teamBName.text = "Them";
            }
            else
            {
                teamAName.text = "Them";
                teamBName.text = "Us";
            }
            
            var scoreA = Mathf.Clamp(teamA.score, 0, 10);
            var scoreB = Mathf.Clamp(teamB.score, 0, 10);
            
            UpdateValue(teamAScore, scoreA);
            UpdateValue(teamBScore, scoreB);
        }

        public void SetCurrentTricksForTeamA(int scoreTeamA)
        {
            UpdateValue(teamATrickCount, scoreTeamA);
        }
        public void SetCurrentTricksForTeamB(int scoreTeamB)
        {
            UpdateValue(teamBTrickCount, scoreTeamB);
        }

        private void UpdateValue(TMP_Text text, int value)
        {
            if (text.text == value.ToString())
                return;
    
            text.text = value.ToString();
            text.transform.localScale = Vector3.one;
            text.transform
                .DOScale(1.5f, 0.5f)
                .SetEase(Ease.OutBack)
                .OnComplete(() =>
                    text.transform.DOScale(1f, 0.5f).SetEase(Ease.OutBack));
        }
    }
}