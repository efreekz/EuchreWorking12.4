using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Ui.MainMenuScreens;
using UnityEngine;
using UnityEngine.Networking;

namespace Network
{
    public static class FirebaseLobbyAPI
    {
        private const string BaseURL = "https://eucher-freekz-default-rtdb.firebaseio.com/"; // replace with your Firebase URL

        public static async UniTask CreateLobby(SessionData session)
        {
            var url = $"{BaseURL}/lobbies/{session.ID}.json";

            var json = JsonConvert.SerializeObject(session);
            var bodyRaw = Encoding.UTF8.GetBytes(json);

            var request = new UnityWebRequest(url, "PUT");
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            await request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
                Debug.Log("✅ Lobby created on Firebase");
            else
                Debug.LogError("❌ Firebase create failed: " + request.error);
        }
        
        public static async UniTask<List<SessionData>> GetLobbies()
        {
            string url = $"{BaseURL}/lobbies.json";
            var request = UnityWebRequest.Get(url);
            await request.SendWebRequest();
    
            if (request.result == UnityWebRequest.Result.Success)
            {
                var json = request.downloadHandler.text;
                var dict = JsonConvert.DeserializeObject<Dictionary<string, SessionData>>(json);
                return dict?.Values.ToList() ?? new List<SessionData>();
            }
    
            Debug.LogError("❌ Failed to get lobbies: " + request.error);
            return new List<SessionData>();
        }
    }
    

}