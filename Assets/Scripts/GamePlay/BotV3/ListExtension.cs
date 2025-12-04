using System;
using System.Collections.Generic;

namespace GamePlay.BotV3
{
    public static class ListExtensions
    {
        private static readonly Random rng = new Random();

        /// <summary>
        /// Returns a random element from the list, or null if list is null/empty.
        /// </summary>
        public static T GetRandom<T>(this List<T> list) where T : class
        {
            if (list == null || list.Count == 0)
                return null;

            int index = rng.Next(list.Count);
            return list[index];
        }
    }
}