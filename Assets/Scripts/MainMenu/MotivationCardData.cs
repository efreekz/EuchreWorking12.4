using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace MainMenu
{
    [System.Serializable]
    public class MotivationCard
    {
        public string title;
        [TextArea] public string message;
        public Sprite icon;
    }
    
    [CreateAssetMenu(fileName = "MotivationCardData", menuName = "ScriptableObject/Data/MotivationCardData", order = 0)]
    public class MotivationCardData : ScriptableObject
    {
        public List<MotivationCard> motivationCards;
        private List<int> _availableIndexes;

        /// <summary>
        /// Returns a random MotivationCard ensuring no repeat until all cards are used.
        /// </summary>
        public MotivationCard GetNextCard()
        {
            if (motivationCards == null || motivationCards.Count == 0)
                return null;

            // Initialize tracking pool if needed
            if (_availableIndexes == null || _availableIndexes.Count == 0)
            {
                _availableIndexes = new List<int>();
                for (int i = 0; i < motivationCards.Count; i++)
                    _availableIndexes.Add(i);
            }

            // Pick random remaining index
            int randomPoolIndex = Random.Range(0, _availableIndexes.Count);
            int cardIndex = _availableIndexes[randomPoolIndex];

            // Remove from pool so it’s not reused
            _availableIndexes.RemoveAt(randomPoolIndex);

            // Return the card
            return motivationCards[cardIndex];
        }

        
    }
}