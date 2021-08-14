using Rails.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rails.Systems
{
    public static class Deck
    {
        // The number of Demand cards to generate
        public const int DemandCardCount = 136;
        // The minumum distance a Demand City can be
        // from the general location of a Demand Good
        private const float MinimumDistance = 10.0f;
        private const int MinReward = 15;
        private const int MaxReward = 50;

        // An integer array representing the number of Demands to generate per City type.
        // Medium cities have the most preference while major cities have the least.
        private static readonly int[] CityTypePreference = new int[] { 3, 5, 2 };

        private static List<DemandCard> _drawPile;
        private static List<DemandCard> _discardPile;
        private static Manager _manager;

        #region Public Methods 

        /// <summary>
        /// Initializes the Deck, creating all cards
        /// associated with the `Manager`'s `MapData`
        /// </summary>
        public static void Initialize()
        {
            _drawPile = new List<DemandCard>();
            _discardPile = new List<DemandCard>();
            _manager = Manager.Singleton;
            GenerateDemandCards(GenerateDemands());
        }

        /// <summary>
        /// Draws a single Demand card
        /// </summary>
        /// <returns>An array of 3 demands, 
        /// representing the card contents</returns>
        public static DemandCard DrawOne()
        {
            // If there are no cards in the draw pile,
            // shuffle the discards and readd them to the draw pile.
            if (_drawPile.Count == 0)
                ShuffleDiscards();

            var index = _drawPile.Count - 1;
            var card = _drawPile[index];

            _drawPile.RemoveAt(index);
            Debug.Log(card.ToString());
            return card;
        }

        /// <summary>
        /// Discard a single Demand card into the discard pile
        /// </summary>
        /// <param name="demandCard">The card to add to the discard pile</param>
        public static void Discard(DemandCard demandCard) => _discardPile.Add(demandCard);

        #endregion

        #region Private Methods 

        // Randomly reinserts all discard Demand cards
        // into the draw pile
        private static void ShuffleDiscards()
        {
            while (_discardPile.Count > 0)
            {
                int cardIndex = UnityEngine.Random.Range(0, _discardPile.Count);
                _drawPile.Add(_discardPile[cardIndex]);
                _discardPile.RemoveAt(cardIndex);
            }
        }

        /// <summary>
        /// Generate a list of demands to be arranged on Cards.
        /// </summary>
        private static List<Demand> GenerateDemands()
        {
            var demands = new List<Demand>();

            // Grab all cities, grouping them by type
            var cities = new City[][]
            {
                _manager.MapData.AllCitiesOfType(NodeType.SmallCity),
                _manager.MapData.AllCitiesOfType(NodeType.MediumCity),
                _manager.MapData.AllCitiesOfType(NodeType.MajorCity)
            };


            // Grab all used Goods
            var goods = _manager.MapData.Goods.Where(g => _manager.MapData.LocationsOfGood(g).Length > 0).ToArray();

            // Create a map determining the general, localized position of a Good
            // (ie. find a city's NodeId with that good).
            var goodsPositions = new List<NodeId>();
            for (int i = 0; i < goods.Length; ++i)
            {
                var ids = _manager.MapData.LocationsOfGood(goods[i]);
                goodsPositions.Add(ids.Aggregate(new NodeId(0, 0), (total, current) => total + current) / ids.Length);
            }

            // Ensures an even selection of the cities per Demand
            // by removing used ones
            var citySelectionLists = new List<City>[3]
            {
                new List<City>(cities[0].Length),
                new List<City>(cities[1].Length),
                new List<City>(cities[2].Length),
            };

            // Generate 3x the amount of Demands
            // Each Demand card has three different Demands on them
            while (demands.Count < DemandCardCount * 3)
            {
                // Cycle through all cities per City type
                for (int i = 0; i < cities.Length; ++i)
                {
                    // And repeat Demand generation per City preference count
                    for (int j = 0; j < CityTypePreference[i]; ++j)
                    {
                        // If all cities have been recently chosen,
                        // readd them to the pool
                        if (citySelectionLists[i].Count == 0)
                        {
                            foreach (var city in cities[i])
                                citySelectionLists[i].Add(city);
                        }

                        // Select a random City
                        var selectedCity = citySelectionLists[i][UnityEngine.Random.Range(0, citySelectionLists[i].Count)];

                        int goodIndex = -1;
                        var distance = 0.0f; // Arbitrary small number,
                                             // to ensure the following while loop executes

                        // Create a way of detecting how many Good selection attempts
                        // have happened, to avoid infinite loops
                        float minDist = MinimumDistance;
                        int checkCounter = 0;

                        // Select a random City from the current group.
                        // While the distance between the city and the selected Good
                        // is less than the MinimumDistance, or if the City holds
                        // the Good being sought, reselect a City
                        while (
                            distance < minDist ||
                            selectedCity.Goods.Any(g => g.x == goodIndex)
                        )
                        {
                            goodIndex = UnityEngine.Random.Range(0, goods.Length);
                            distance = NodeId.Distance(
                                _manager.MapData.LocationsOfCity(selectedCity).First(),
                                _manager.MapData.LocationsOfGood(goods[goodIndex]).First()
                            );

                            // If there have been several attempts to find a Good
                            // that meets the parameters with the given City,
                            // detract from the minimum distance to expand possible Citys.
                            checkCounter += 1;
                            if (checkCounter == cities.Sum(cs => cs.Length))
                            {
                                minDist -= 5.0f;
                                checkCounter = 0;
                            }
                        }

                        // Remove the selected City from the potential choices
                        citySelectionLists[i].Remove(selectedCity);

                        // Determine the reward by City NodeType, with distance considered
                        int reward = DetermineReward(distance, i);
                        demands.Add(new Demand(selectedCity, goods[goodIndex], reward));

                        if (demands.Count >= DemandCardCount * 3) break;
                    }
                    if (demands.Count >= DemandCardCount * 3) break;
                }
            }
            return demands;
        }
        /// <summary>
        /// Randomly generates a reward amount for a demand based on distance with a gaussian distribution.
        /// </summary>
        /// <param name="distance"></param>
        /// <param name="i"></param>
        /// <returns></returns>
        private static int DetermineReward(float distance, int cityType)
        {
            var bounds = _manager.MapData.MapNodeBounds;

            float distFactor = (MaxReward - MinReward)
                / Mathf.Sqrt(Mathf.Pow(bounds.size.x, 2) + Mathf.Pow(bounds.size.y, 2));

            int reward = (int)(distance * distFactor * 1.5f) + (cityType * 5);
            reward += UnityEngine.Random.Range(0, 5);
            reward = Mathf.Clamp(reward, MinReward, MaxReward);

            return reward;
        }

        /// <summary>
        /// Creates a deck of Demand cards from a list of Demands.
        /// Three Demands per card.
        /// </summary>
        private static void GenerateDemandCards(List<Demand> demands)
        {
            var deckBuilder = new List<List<Demand>>();
            for (int i = 0; i < DemandCardCount; ++i)
                deckBuilder.Add(new List<Demand>());

            int deckIndex = 0;

            // Sort demands by City
            SortRandom(demands);

            // Add each demand to the deckBuilder in turn. Add one per card, 
            // iterating through the whole deck before adding to the same card
            // again, to avoid repeating cities on the same card.
            for (int i = 0; i < DemandCardCount * 3; ++i)
            {
                var demand = demands.Last();
                deckBuilder[deckIndex].Add(demand);

                deckIndex += 1;
                if (deckIndex >= DemandCardCount)
                    deckIndex = 0;

                demands.RemoveAt(demands.Count - 1);
            }

            foreach (var card in deckBuilder)
                _drawPile.Add(new DemandCard(card));

            // Add all draw cards to the discard pile, to
            // ensure the deck is properly shuffled on start
            for (int i = _drawPile.Count - 1; i >= 0; --i)
            {
                _discardPile.Add(_drawPile[i]);
                _drawPile.RemoveAt(i);
            }
        }

        private static void SortRandom(List<Demand> demands)
        {
            var shuffleChar = (char)(97 + UnityEngine.Random.Range(0, 26));
            demands.Sort(
                (first, second) =>
                    (shuffleChar + first.City.Name.Substring(1)).CompareTo(
                        shuffleChar + second.City.Name.Substring(1).ToString()
                    )
            );
        }
    }

    #endregion
}
