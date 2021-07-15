using System.Collections;
using System.Collections.Generic;
using Rails.ScriptableObjects;
using UnityEngine;

namespace Rails.ScriptableObjects
{
    [CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/Map/MapData", order = 1)]
    public class MapData : ScriptableObject
    {
        [SerializeField]
        public MapTokenTemplate DefaultTokenTemplate;
        [SerializeField]
        public GameObject Board;
        [SerializeField]
        public PlayerTokenTemplate DefaultPlayerTemplate;

        [SerializeField]
        public Node[] Nodes;
        [SerializeField]
        public NodeSegment[] Segments;
        [SerializeField]
        public List<City> Cities = new List<City>();
        [SerializeField]
        public List<Good> Goods = new List<Good>();

        public MapData()
        {

        }

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
    }
}
