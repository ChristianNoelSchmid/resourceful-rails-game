using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Rails
{
    public class Manager : MonoBehaviour
    {
        /// <summary>
        /// Map size.
        /// </summary>
        public const int Size = 64;

        /// <summary>
        /// The Cost for a player to use another player's track
        /// </summary>
        public const int AltTrackCost = 10;

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
        private Dictionary<NodeId, int[]> Tracks = new Dictionary<NodeId, int[]>();

        [SerializeField]
        private Text _text;

        private NodeId _selectedId;
        private NodeMarker _selectedNode = null;

        private NodeId _targetedId;
        private NodeMarker _targetedNode = null;

        private Dictionary<NodeId, NodeMarker[]> TrackMarkers = new Dictionary<NodeId, NodeMarker[]>();
        private Dictionary<NodeId, NodeMarker> NodeMarkers = new Dictionary<NodeId, NodeMarker>();

        #endregion

        #region Unity Events

        private void Awake()
        {
            // set singleton reference on awake
            _singleton = this;
            for(int x = 0; x < Size; ++x)
            {
                for(int y = 0; y < Size; ++y)
                {
                    var nodeId = new NodeId(x, y);
                    var marker = Instantiate(Map.DefaultTokenTemplate.Clear, GetPosition(nodeId), Quaternion.identity);
                    NodeMarkers.Add(nodeId, marker.GetComponent<NodeMarker>());

                    NodeMarkers[nodeId].PrimaryColor = Color.black;
                    NodeMarkers[nodeId].SetColor(Color.black);
                }
            }
            InsertRandomTracks();
        }

        private void Update()
        { 
            Vector3 point = new Vector3();
            Vector2 mousePos = new Vector2();

            // Get the mouse position from Event.
            // Note that the y position from Event is inverted.
            mousePos.x = Input.mousePosition.x;
            mousePos.y = Input.mousePosition.y;

            point = Camera.main.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, 0.0f));
            if(_selectedNode != null)
            {
                _selectedNode.ResetColor();
            }
            if(NodeMarkers.ContainsKey(GetNodeId(point)))
            { 
                _selectedNode = NodeMarkers[GetNodeId(point)];
                _selectedNode.SetColor(Color.yellow);
                _selectedId = GetNodeId(point);

                if(Input.GetMouseButtonDown(0))
                {                           
                    if(_targetedNode != null)
                        _targetedNode.SetColor(Color.white);
                    _targetedNode = _selectedNode;
                    _targetedNode.SetColor(Color.green);
                    _targetedId = GetNodeId(point);
                }
                if(Input.GetMouseButtonDown(1) && _targetedNode != null && _selectedNode != null && _targetedNode != _selectedNode)
                {                        
                    var tracks = Pathfinding.BestTracks(Tracks, 1, 5, _selectedId, _targetedId);

                    if(tracks != null && tracks.Count != 0)
                    {
                        foreach(var n in TrackMarkers.Keys)
                        {
                            for(int i = 0; i < TrackMarkers[n].Length; ++i)
                            {
                                if(TrackMarkers[n][i] != null)
                                    TrackMarkers[n][i].ResetColor();
                            }
                        }
                        _text.text = "";
                        for(int t = tracks.Count - 1; t >= 0; --t)
                        { 
                            for(int i = 0; i < tracks[t].Nodes.Count - 1; ++i)
                            {
                                Cardinal c = Utilities.CardinalBetween(tracks[t].Nodes[i], tracks[t].Nodes[i+1]);

                                TrackMarkers[tracks[t].Nodes[i]][(int)c].SetColor(t switch
                                {
                                    0 => Color.white,
                                    1 => Color.magenta,
                                    2 => new Color(0.0f, 1.0f, 1.0f),
                                    _ => new Color(0.5f, 0.5f, 0.5f),
                                });
                            }
                            _text.text += $"Track {t + 1}: Dist {tracks[t].Distance} : Cost {tracks[t].Cost}\n";
                        }
                        _text.text = _text.text.Substring(0, _text.text.Length - 1);
                    }
                } 
            }
            if(_selectedId != GetNodeId(point)) 
                Debug.Log(GetNodeId(point));
            Debug.Log(GetNodeId(point));
        }

        private void OnDrawGizmos()
        {
            if (Map == null || Map.Nodes == null || Map.Nodes.Length == 0)
                return;

            Gizmos.color = Color.black;
            for (int x = 0; x < Size; x++)
            {
                for (int y = 0; y < Size; y++)
                {
                    Gizmos.DrawSphere(GetPosition(Map.Nodes[(y * Size) + x].Id), WSSize * 0.1f);
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

        public NodeId GetNodeId(Vector3 position)
        {
            var w = 2 * WSSize;
            var h = Mathf.Sqrt(3) * WSSize;
            var wspace = 0.75f * w;

            int posX = Mathf.RoundToInt(position.x / wspace);
            if(posX % 2 == 1)
                position.z -= h / 2;

            return new NodeId(posX, Mathf.RoundToInt(position.z / h));
        }

        #endregion 

        public void InsertTrack(int player, NodeId position, Cardinal towards) =>
            InsertTrack(player, position, towards, true);

        /// <summary>
        /// Inserts a new track onto the Map, based on position and direction.
        /// </summary>
        /// <param name="player">The player who owns the track</param>
        /// <param name="position">The position the track is placed</param>
        /// <param name="towards">The cardinal direction the track moves towards</param>
        private void InsertTrack(int player, NodeId position, Cardinal towards, bool reverse)
        {
            if(position.X < 0 || position.Y < 0 || position.X > Size || position.Y > Size)
                return;

            // If Cardinal data doesn't exist for the point yet,
            // insert and initialize the data
            if(!Tracks.ContainsKey(position))
            {
                Tracks[position] = new int[(int)Cardinal.MAX_CARDINAL];
                for(int i = 0; i < (int)Cardinal.MAX_CARDINAL; ++i)
                    Tracks[position][i] = -1;
            }

            Tracks[position][(int)towards] = player;

            if(reverse)
                // As Tracks is undirected, insert a track moving the opposite way from the
                // target node as well.
                InsertTrack(player, Utilities.PointTowards(position, towards), Utilities.ReflectCardinal(towards), false);
        }

        private float GetRotation(Cardinal c) => c switch
        {
            Cardinal.N => 0.0f,
            Cardinal.NE => 60.0f,
            Cardinal.SE => 120.0f,
            Cardinal.S => 180.0f,
            Cardinal.SW => 240.0f,
            _ => 300.0f,
        };
    
        private void InsertRandomTracks()
        {
            for(int p = 1; p <= 4; ++p)
            {
                for(int t = 0; t < 10; t++)
                {
                    NodeId current;
                    int c;
                    do {
                        current = new NodeId(Random.Range(0, 48), Random.Range(0, 48));
                        c = Random.Range(0, (int)Cardinal.MAX_CARDINAL);
                    } while(Tracks.ContainsKey(current) && Tracks[current][c] != -1);

                    for(int i = 0; i < 50; ++i)
                    {
                        InsertTrack(p, current, (Cardinal)c);
                        var track = Instantiate(Map.DefaultPlayerTemplate.RailToken, GetPosition(current) + new Vector3(0, 1.0f, 0), Quaternion.Euler(0.0f, GetRotation((Cardinal)c), 0.0f)).GetComponent<NodeMarker>();
                        track.PrimaryColor = p switch {
                            1 => Color.Lerp(Color.red, Color.black, 0.5f),
                            2 => Color.Lerp(Color.blue, Color.black, 0.5f),
                            3 => Color.Lerp(Color.green, Color.black, 0.5f),
                            _ => Color.Lerp(Color.yellow, Color.black, 0.5f),
                        };
                        track.ResetColor();

                        if(!TrackMarkers.ContainsKey(current)) TrackMarkers.Add(current, new NodeMarker[6]);
                        TrackMarkers[current][c] = track;

                        if(!TrackMarkers.ContainsKey(Utilities.PointTowards(current, (Cardinal)c))) 
                            TrackMarkers.Add(Utilities.PointTowards(current, (Cardinal)c), new NodeMarker[6]);
                        TrackMarkers[Utilities.PointTowards(current, (Cardinal)c)][(int)Utilities.ReflectCardinal((Cardinal)c)] = track;


                        current = Utilities.PointTowards(current, (Cardinal)c);
                        if(current.X < 0 || current.Y < 0 || current.X > 48 || current.Y > 48)
                            break;
                        if(Tracks.ContainsKey(current) && !Tracks[current].Any(pl => pl == -1))
                            break;
                        else if(Tracks.ContainsKey(current))
                        {
                            do {
                                int val = Random.Range(0, (int)Cardinal.MAX_CARDINAL + 5);
                                if(val >= (int)Cardinal.MAX_CARDINAL) val = c; 
                                c = val;
                            } while(Tracks[current][c] != -1);
                        }
                        else c = Random.Range(0, (int)Cardinal.MAX_CARDINAL);
                    }
                }
            }
        } 
    }
}
