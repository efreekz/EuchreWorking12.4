using System;
using System.Collections.Generic;
using GamePlay.Cards;
using GamePlay.Player;
using Network;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

namespace Data
{
    #region Data Classes

    #region Error Data

    [Serializable]
    public class ErrorResponse
    {
        public string code;
        public string message;
        public ErrorData data;
    }

    [Serializable]
    public class ErrorData
    {
        public int status;
    }
    
    #endregion
    
    [Serializable]
    public class LoginResponse
    {
        public string message;
        public string access_token;
        public string token;
        public UserData user;
        public string promo_code;
        public float balance;
    }

    [Serializable]
    public class UserData
    {
        public string id;
        public string username;
        public string email;
        public string promo_code;
        public int balance;
        public int games_played;
        public int games_won;
        public string created_at;
    }
    
    [Serializable]
    public class Transaction
    {
        public int id;
        public int amount;
        public string type;
        public string description;
        public string created_at;
        public string formatted_amount;
    }

    [Serializable]
    public class Pagination
    {
        public int total;
        public int limit;
        public int offset;
        public bool has_more;
    }

    [Serializable]
    public class Balance
    {
        public int current;
        public string formatted;
    }

    [Serializable]
    public class TransactionResponse
    {
        public bool success;
        public List<Transaction> transactions;
        public Pagination pagination;
        public Balance balance;
        public Filters filters;
    }

    [Serializable]
    public class Filters
    {
        public string type;
    }

    [Serializable]
    public class GameResult
    {
        [Serializable]
        public class TeamData
        {
            public string teamName;
            public List<PlayerInfo> players;
            public int score;
            public bool isMyTeam;

            public TeamData(NetworkTeamData teamData, int localPlayerIndex, List<PlayerBase> allPlayers)
            {
                players = new List<PlayerInfo>();
                score = teamData.score;
                isMyTeam = false;
                
                players.Add(allPlayers[teamData.player0Index].PlayerInfo);
                players.Add(allPlayers[teamData.player1Index].PlayerInfo);

                if (allPlayers[teamData.player0Index].PlayerIndex == localPlayerIndex || allPlayers[teamData.player1Index].PlayerIndex == localPlayerIndex)
                    isMyTeam = true;
                
                teamName = isMyTeam ? "Us" : "Them";
            }
        }
        
        public TeamData teamA;
        public TeamData teamB;
        
        public bool isLocalPlayerWinner;
        public int reward;
        
    }
    #endregion
}
