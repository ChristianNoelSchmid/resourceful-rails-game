using System;
using System.Collections.Generic;
using System.Linq;
using Rails.Data;
using UnityEngine;

namespace Rails.ScriptableObjects
{
    [CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/Map/MapData", order = 1)]
    public class MapData : ScriptableObject
    {
        public MapTokenTemplate DefaultTokenTemplate;
        public PlayerTokenTemplate DefaultPlayerTemplate;
        public GameObject Board;

        public GameRules DefaultRules;
        
        [HideInInspector]
        public Node[] Nodes;
        [HideInInspector]
        public NodeSegment[] Segments;
        [HideInInspector]
        public List<City> Cities = new List<City>();
        [HideInInspector]
        public List<Good> Goods = new List<Good>();


        public Node GetNodeAt(NodeId id)
        {
            var index = id.GetSingleId();
            if (index < 0 || index >= Nodes.Length)
                return null;

            return Nodes[index];
        }

        public Node GetNodeAt(int index)
        {
            if (index < 0 || index >= Nodes.Length)
                return null;

            return Nodes[index];
        }

        public NodeSegment[] GetNodeSegments(NodeId id)
        {
            int index = id.GetSingleId() * 6;
            return new NodeSegment[]
            {
                GetNodeSegmentAt(index + 0),
                GetNodeSegmentAt(index + 1),
                GetNodeSegmentAt(index + 2),
                GetNodeSegmentAt(index + 3),
                GetNodeSegmentAt(index + 4),
                GetNodeSegmentAt(index + 5),
            };
        }

        public NodeSegment GetNodeSegmentAt(int index)
        {
            if (index < 0 || index >= Segments.Length)
                return null;

            return Segments[index];
        }
    
        /// <summary>
        /// Returns all `NodeId`s adjacent to the given `NodeId`, if
        /// they fall within the game bounds (`Manger.Size`).
        /// </summary>
        /// <param name="nodeId">The `NodeId` to check</param>
        /// <returns>All adjacent `NodeId`s and the `Node` being stored there.</returns>
        public Tuple<NodeId, Node>[] GetNeighborNodes(NodeId nodeId)
        {
            var node = Nodes[nodeId.GetSingleId()];
            var cityId = Nodes[nodeId.GetSingleId()].CityId;
            var cardinalRange = Enumerable.Range((int)Cardinal.N, (int)Cardinal.MAX_CARDINAL);

            return
                 cardinalRange
                .Select(c => Utilities.PointTowards(nodeId, (Cardinal)c))
                .Where(nId => nId.InBounds)
                .Select(nId => Tuple.Create(nId, Nodes[nId.GetSingleId()]))
                .ToArray();
        }
        
        /// <summary>
        /// Retrieves All `City`s of a specified type.
        /// </summary>
        /// <param name="nodeType">The type of city wished to retrieve.</param>
        /// <returns>All `City`s of the type provided.</returns>
        public City[] AllCitiesOfType(NodeType nodeType) 
            => nodeType switch
            {
                NodeType.Clear | NodeType.Mountain | NodeType.Water => null,
                _ => Nodes
                    .Where(n => n.Type == nodeType)
                    .Select(n => n.CityId)
                    .Select(i => Cities[i]).ToArray()
            };
        
        /// <summary>
        /// Returns all `NodeId`s where a specified `Good` is found.
        /// </summary>
        /// <param name="good">The `Good` wished to be localized</param>
        /// <returns>All `NodeId`s of the locations of the `Good`</returns>
        public NodeId[] LocationsOfGood(Good good)
            => Enumerable.Range(0, Nodes.Length)
            .Where(i => 
                (Nodes[i].Type == NodeType.SmallCity ||
                 Nodes[i].Type == NodeType.MediumCity || 
                 Nodes[i].Type == NodeType.MajorCity) && Cities[Nodes[i].CityId].Goods
                    .Any(g => g.x == Goods.IndexOf(good)
            ))
            .Select(i => NodeId.FromSingleId(i))
            .ToArray();
        
         /// <summary>
        /// Returns all `NodeId`s where a specified `City` is found.
        /// </summary>
        /// <param name="city">The `City` wished to be localized</param>
        /// <returns>All `NodeId`s of the locations of the `City`</returns>
        public NodeId[] LocationsOfCity(City city) 
            => Enumerable.Range(0, Nodes.Length)
            .Where(i => Nodes[i].CityId == Cities.IndexOf(city))
            .Select(i => NodeId.FromSingleId(i))
            .ToArray();

        public int[] GetGoodsAtCity(City city)
            => Cities
                .Where(c => c == city).First()?
                .Goods.Select(g => g.x).ToArray() ?? new int[0];

        public int[] GetCitiesWithGood(Good good)
        {
            int goodIndex = Goods.IndexOf(good);
            var cities = new HashSet<City>(Cities.Where(c => c.Goods.Select(g => g.x).Contains(goodIndex)));

            return cities.Select(c => Cities.IndexOf(c)).ToArray();
        }

        // Cache for MapData bounds
        private Bounds ? _mapNodeBounds = null;
        /// <summary>
        /// Returns the Bounds of the map - pertaining to the min and max
        /// non-water Node, rather than the actual min-max Node.
        /// </summary>
        public Bounds MapNodeBounds 
        {
            get
            {
                if (_mapNodeBounds.HasValue) 
                    return _mapNodeBounds.Value;

                int minX = Manager.Size, minY = Manager.Size, maxX = 0, maxY = 0;

                for(int i = 0; i < Manager.Size * Manager.Size; ++i)
                {
                    if(Nodes[i].Type != NodeType.Water)
                    {
                        var nodeId = NodeId.FromSingleId(i);

                        if (nodeId.X < minX) minX = nodeId.X;
                        if (nodeId.Y < minY) minY = nodeId.Y;

                        if (nodeId.X > maxX) maxX = nodeId.X;
                        if (nodeId.Y > maxY) maxY = nodeId.Y;
                    }
                }
                _mapNodeBounds = new Bounds
                {
                    min = Utilities.GetPosition(new NodeId(minX, minY)),
                    max = Utilities.GetPosition(new NodeId(maxX, maxY))
                };

                return _mapNodeBounds.Value;
            }
        }
    }
}
