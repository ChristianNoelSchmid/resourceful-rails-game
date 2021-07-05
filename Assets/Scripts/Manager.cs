using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rails
{
    public class Manager : MonoBehaviour
    {
        /// <summary>
        /// Map size.
        /// </summary>
        public const int Size = 64;

        #region Singleton

        private static Manager _singleton = null;

        /// <summary>
        /// Manager singleton
        /// </summary>
        public static Manager Singleton
        {
            get
            {
                if (_singleton)
                    return _singleton;

                _singleton = FindObjectOfType<Manager>();
                if (_singleton)
                    return _singleton;

                GameObject go = new GameObject("Manager");
                return go.AddComponent<Manager>();
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// 
        /// </summary>
        public float WSSize = 1f;

        /// <summary>
        /// 
        /// </summary>
        [SerializeField]
        public MapData Map;

        /// <summary>
        ///
        /// </summary>
        [SerializeField]
        private Dictionary<Vector2Int, int[]> Tracks = new Dictionary<Vector2Int, int[]>();

        #endregion

        #region Unity Events

        private void Awake()
        {
            // set singleton reference on awake
            _singleton = this;
        }

        private void OnDrawGizmos()
        {
            if (Map == null || Map.Nodes == null || Map.Nodes.Length == 0)
                return;

            for (int x = 0; x < Size; x++)
            {
                for (int y = 0; y < Size; y++)
                {
                    // draw node
                    var node = Map.Nodes[(y * Size) + x];
                    var pos = GetPosition(node.Id);
                    Gizmos.color = MapEditorUtils.GetNodeColor(node.Type);
                    Gizmos.DrawSphere(pos, WSSize * 0.1f);

                    // draw segments
                    var segments = Map.GetNodeSegments(node.Id);
                    for (Cardinal c = 0; c < Cardinal.MAX_CARDINAL; ++c)
                    {
                        var segment = segments[(int)c];
                        var nextNodeId = new NodeId(-1, -1);

                        switch (c)
                        {
                            case Cardinal.S:
                                {
                                    nextNodeId = new NodeId(node.Id.X, node.Id.Y + 1);
                                    break;
                                }
                        }

                        if (nextNodeId.InBounds)
                        {
                            var nextNode = Map.Nodes[nextNodeId.GetSingleId()];
                            Gizmos.color = MapEditorUtils.GetSegmentColor(segment.Type);
                            Gizmos.DrawLine(pos, GetPosition(nextNode.Id));
                        }
                    }
                }
            }
        }

        #endregion

        #region Utilities

        public Vector3 GetPosition(NodeId id)
        {
            var w = 2 * WSSize;
            var h = Mathf.Sqrt(3) * WSSize;
            var wspace = 0.75f * w;
            var pos = new Vector3(id.X * wspace, 0, id.Y * h);
            int parity = id.X & 1;
            if (parity == 1)
                pos.z += h / 2;

            return pos;
        }

        #endregion 
        
        /// <summary>
        /// Inserts a new track onto the Map, based on position and direction.
        /// </summary>
        /// <param name="player">The player who owns the track</param>
        /// <param name="position">The position the track is placed</param>
        /// <param name="towards">The cardinal direction the track moves towards</param>
        private void InsertTrack(int player, Vector2Int position, Cardinal towards)
        {
            // If Cardinal data doesn't exist for the point yet,
            // insert and initialize the data
            if(!Tracks.ContainsKey(position))
            {
                Tracks[position] = new int[(int)Cardinal.MAX_CARDINAL];
                for(int i = 0; i < (int)Cardinal.MAX_CARDINAL; ++i)
                    Tracks[position][i] = -1;
            }

            Tracks[position][(int)towards] = player;

            // As Tracks is undirected, insert a track moving the opposite way from the
            // target node as well.
            InsertTrack(player, Utilities.PointTowards(position, towards), Utilities.ReflectCardinal(towards));
        }
    }
}
