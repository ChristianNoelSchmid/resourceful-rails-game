using System;

namespace Rails.Data
{
    [Serializable]
    public struct Settings
    {
        /// <summary>
        /// The number of players playing this game.
        /// </summary>
        public int maxPlayers;
        /// <summary>
        /// The amount of money each player starts with.
        /// </summary>
        public int moneyStart;
        /// <summary>
        /// The max amount of money that can be spent building.
        /// </summary>
        public int maxBuild;
        /// <summary>
        /// The cost to for players to upgrade their train.
        /// </summary>
        public int trainUpgrade;
        /// <summary>
        /// The number of major cities that must be connected to win.
        /// </summary>
        public int winMajorCities;
        /// <summary>
        /// The amount of money needed to win.
        /// </summary>
        public int winMoney;
    }
}
