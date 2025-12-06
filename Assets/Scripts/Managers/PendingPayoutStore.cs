using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Managers
{
    public static class PendingPayoutStore
    {
        private const string StorageKey = "pending_payouts";

        [Serializable]
        public class PendingPayoutEntry
        {
            public string id;
            public string lobbyId;
            public int fee;
            public int reward;
            public bool won;
            public long createdAt;
        }

        [Serializable]
        private class PendingPayoutWrapper
        {
            public List<PendingPayoutEntry> entries = new List<PendingPayoutEntry>();
        }

        private static readonly List<PendingPayoutEntry> Cache;

        static PendingPayoutStore()
        {
            Cache = Load();
        }

        private static List<PendingPayoutEntry> Load()
        {
            try
            {
                if (!PlayerPrefs.HasKey(StorageKey))
                    return new List<PendingPayoutEntry>();

                var json = PlayerPrefs.GetString(StorageKey, string.Empty);
                if (string.IsNullOrEmpty(json))
                    return new List<PendingPayoutEntry>();

                var wrapper = JsonConvert.DeserializeObject<PendingPayoutWrapper>(json);
                return wrapper?.entries ?? new List<PendingPayoutEntry>();
            }
            catch (Exception ex)
            {
                GameLogger.LogNetwork($"Failed to load pending payouts: {ex.Message}", GameLogger.LogType.Error);
                return new List<PendingPayoutEntry>();
            }
        }

        private static void Save()
        {
            try
            {
                var wrapper = new PendingPayoutWrapper { entries = Cache };
                var json = JsonConvert.SerializeObject(wrapper);
                PlayerPrefs.SetString(StorageKey, json);
                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                GameLogger.LogNetwork($"Failed to save pending payouts: {ex.Message}", GameLogger.LogType.Error);
            }
        }

        public static PendingPayoutEntry AddOrUpdate(string lobbyId, int fee, int reward, bool won)
        {
            var existing = Cache.FirstOrDefault(entry => entry.lobbyId == lobbyId && entry.reward == reward && entry.won == won);
            if (existing != null)
            {
                existing.fee = fee;
                Save();
                return existing;
            }

            var entry = new PendingPayoutEntry
            {
                id = Guid.NewGuid().ToString(),
                lobbyId = lobbyId,
                fee = fee,
                reward = reward,
                won = won,
                createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            Cache.Add(entry);
            Save();
            return entry;
        }

        public static void Remove(string id)
        {
            var removed = Cache.RemoveAll(entry => entry.id == id);
            if (removed > 0)
                Save();
        }

        public static IReadOnlyList<PendingPayoutEntry> GetAll()
        {
            return Cache.ToArray();
        }
    }
}
