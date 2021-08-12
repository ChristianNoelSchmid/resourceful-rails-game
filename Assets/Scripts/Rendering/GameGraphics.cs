using Assets.Scripts.Data;
using Rails.Collections;
using Rails.Data;
using Rails.ScriptableObjects;
using Rails.Systems;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rails.Rendering
{
    public class GameGraphics : MonoBehaviour
    {
        #region Singleton
        private static GameGraphics _singleton;
        private void Awake()
        {
            if (_singleton != null)
                Debug.LogError("Error: more than one GameGraphics Monobehaviour found. Please have only one per Scene.");
            _singleton = this;
        }
        #endregion

        // A collection of all node GameTokens on the map
        private static Dictionary<NodeId, GameToken> _mapTokens;
        // A collection of all track GameTokens on the map
        private static TrackGraph<GameToken> _trackTokens;
        // A collection of route GameTokens
        private static Dictionary<Route, List<GameToken>> _potentialTracks;

        private static GameToken[] _playerTrains;

        // An object pool for the track GameTokens
        private static ObjectPool<GameToken> _trackPool;

        // A set of all currently running trains.
        // Used to avoid coroutine issues when if run on the same train
        // more than once at a time.
        private static HashSet<int> _currentlyRunningTrains;

        public static EventHandler OnTrainMovementFinished;

        public static void Initialize(MapData mapData, int playerCount, Color [] playerColors)
        {
            _mapTokens = new Dictionary<NodeId, GameToken>();
            _trackTokens = new TrackGraph<GameToken>();
            _potentialTracks = new Dictionary<Route, List<GameToken>>();

            _trackPool = new ObjectPool<GameToken>(mapData.DefaultPlayerTemplate.RailToken, 20, 20);
            _currentlyRunningTrains = new HashSet<int>();

            GenerateBoard(mapData);
            GenerateNodes(mapData);
            GenerateTrains(mapData, playerCount, playerColors);
        }

        #region Public Methods

        /// <summary>
        /// Retrieves the GameToken located at the given NodeId
        /// </summary>
        /// <param name="nodeId">The NodeId the token is stored at</param>
        /// <returns>The GameToken, if any are located at the NodeId position</returns>
        public static GameToken GetMapToken(NodeId nodeId)
        {
            if (nodeId != null)
            {
                _mapTokens.TryGetValue(nodeId, out var token);
                return token;
            }
            return null;
        }

        /// <summary>
        /// Generates a highlighted track given a `Route`
        /// </summary>
        /// <param name="route">The `Route` to build the track on</param>
        /// <returns>An index representing the ID of the track</returns>
        public static void GeneratePotentialTrack(Route route, Color highlightColor)
        {
            if (route.Nodes.Count == 0) return;

            var trackTokens = new List<GameToken>(route.Nodes.Count);
            for (int i = 0; i < route.Distance; ++i)
            {
                // Determine the rotation between adjacent Route nodes
                var rotation = Utilities.GetCardinalRotation(
                    Utilities.CardinalBetween(route.Nodes[i], route.Nodes[i + 1])
                );

                // Setup the track GameToken
                var trackToken = _trackPool.Retrieve();
                trackToken.transform.position = Utilities.GetPosition(route.Nodes[i]);
                trackToken.transform.rotation = rotation;

                // Highlight the token and add it to the token list
                trackToken.SetColor(highlightColor);
                trackTokens.Add(trackToken);
            }

            _potentialTracks[route] = trackTokens;
        }

        /// <summary>
        /// Destroys a potential-built track given the supplied `Route`
        /// </summary>
        /// <param name="route">The `Route` of the track being destroyed</param>
        public static void DestroyPotentialTrack(Route route)
        {
            if (route == null) return;

            // If the route exists in the current potential tracks,
            // return all tracks to the ObjectPool
            if (_potentialTracks.TryGetValue(route, out var tokens))
            {
                foreach (var token in tokens)
                    _trackPool.Return(token);

                _potentialTracks.Remove(route);
            }
        }

        /// <summary>
        /// Commits the given `Route` to the map, overlaying the track with a given color.
        /// </summary>
        /// <param name="route">The potential `Route` held in the map.</param>
        /// <param name="color">The color to set the new track to.</param>
        public static void CommitPotentialTrack(Route route, Color color)
        {
            if (route == null) return;

            // If the route exists in the current potential tracks,
            // Add its GameTokens to the track token map.
            if (_potentialTracks.TryGetValue(route, out var tokens))
            {
                for (int i = 0; i < route.Distance; ++i)
                {
                    _trackTokens[route.Nodes[i], route.Nodes[i + 1]] = tokens[i];
                    tokens[i].SetPrimaryColor(color);
                }
                _potentialTracks.Remove(route);
            }
        }

        /// <summary>
        /// Highlights or dehighlights a given list of NodeIds
        /// Skips any route index that is not on the track map
        /// </summary>
        /// <param name="route">The list of nodes to alter</param>
        /// <param name="highlightColor">Highlights to the specified Color, or resets Color if null</param>
        public static void HighlightRoute(List<NodeId> route, Color ? highlightColor)
        {
            if (route == null) return;

            // Check each Route node to see if it exists in the track token map.
            // If it does, (de)activate its highlight.
            for (int i = 0; i < route.Count - 1; ++i)
            {
                if (_trackTokens.TryGetEdgeValue(route[i], route[i + 1], out var token))
                {
                    if (highlightColor.HasValue)
                        token.SetColor(highlightColor.Value);
                    else
                        token.ResetColor();
                }
            }
        }

        /// <summary>
        /// Highlights or dehighlights a given track Route
        /// Skips any route index that is not on the track map
        /// </summary>
        /// <param name="route">The `Route` to alter</param>
        /// <param name="highlightColor">Highlights to the specified Color, or resets Color if null</param>
        public static void HighlightRoute(Route route, Color? highlightColor)
            => HighlightRoute(route?.Nodes, highlightColor);


        /// <summary>
        /// Sets a given player's train's `GameToken` based on the `TrainType` provided.
        /// </summary>
        public static void UpdatePlayerTrain(MapData mapData, int player, int index)
        {
            if (player < 0 || player >= _playerTrains.Length)
                throw new ArgumentException("Attempted to upgrade train for player that doesn't exist.");

            var oldToken = _playerTrains[player];

            var newToken = Instantiate(mapData.DefaultPlayerTemplate.GetTrainToken(index));
            newToken.transform.position = oldToken.transform.position;
            newToken.transform.rotation = oldToken.transform.rotation;
            _playerTrains[player] = newToken;

            Destroy(oldToken.gameObject);
        }

        /// <summary>
        /// Positions a player instantly to the specified NodeId position.
        /// </summary>
        /// <param name="player">The index of the player who wishes to be positioned</param>
        /// <param name="node">The position to place the player</param>
        public static void PositionTrain(int player, NodeId node)
        {
            _playerTrains[player].gameObject.SetActive(true);
            _playerTrains[player].transform.position = Utilities.GetPosition(node);
        }

        /// <summary>
        /// Moves a player train along the given `Route`.
        /// </summary>
        /// <param name="player">The player index to move.</param>
        /// <param name="path">The nodes the player train will traverse on.</param>
        public static void MoveTrain(int player, List<NodeId> path) => _singleton.StartCoroutine(CMoveTrain(player, path, 5.0f));
        
        #endregion

        #region Private Methods
        /// Instantiates the MapData board, and sets it to the correct size
        private static void GenerateBoard(MapData mapData)
        {
            var board = Instantiate(mapData.Board);

            // Scale the board to match the current spacing size
            board.transform.localScale = Vector3.one * Manager.Singleton.WSSize;
        }

        /// Instantiates all MapData nodes
        private static void GenerateNodes(MapData mapData)
        {
            // Cycle through all NodeIds
            for (int x = 0; x < Manager.Size; x++)
            {
                for (int y = 0; y < Manager.Size; y++)
                {
                    var nodeId = new NodeId(x, y);
                    var node = mapData.Nodes[nodeId.GetSingleId()];
                    var pos = Utilities.GetPosition(node.Id);

                    // Retrieve the GameToken from the Map's default
                    var modelToken = mapData.DefaultTokenTemplate.GetToken(node.Type);

                    if (modelToken != null)
                    {
                        // If the node is a MajorCity, determine if its the center
                        // node (ie. surrounded by nodes with the same CityId).
                        // If so, spawn the MajorCity token there.
                        if (node.Type == NodeType.MajorCity)
                        {
                            var neighborNodes = mapData.GetNeighborNodes(nodeId);
                            if (neighborNodes.All(nn => nn.Item2.CityId == node.CityId && nn.Item2.Type == NodeType.MajorCity))
                            {
                                var token = Instantiate(modelToken, _singleton.transform);
                                token.transform.position = pos + new Vector3(0, 0.1f, 0);

                                foreach (var nId in neighborNodes.Select(nn => nn.Item1))
                                    _mapTokens[nId] = token;

                                _mapTokens[nodeId] = token;
                            }
                        }
                        else
                        {
                            var token = Instantiate(modelToken, _singleton.transform);
                            token.transform.position = pos + new Vector3(0, 0.1f, 0);

                            _mapTokens[nodeId] = token;
                        }
                    }
                }
            }
        }

        // Generates all player trains, and assigns them their colors
        private static void GenerateTrains(MapData mapData, int playerCount, Color[] playerColors)
        {
            _playerTrains = new GameToken[playerCount];
            for (int p = 0; p < playerCount; p++)
            {
                _playerTrains[p] = Instantiate(mapData.DefaultPlayerTemplate.GetTrainToken(0), _singleton.transform);
                _playerTrains[p].gameObject.SetActive(false);

                if (p < playerColors.Length)
                    _playerTrains[p].SetColor(playerColors[p]);
            }
        }

        // Moves a player train through the given `Route`
        private static IEnumerator CMoveTrain(int player, List<NodeId> path, float speed)
        {
            if (player < 0 || player >= _playerTrains.Length)
                yield break;
            if (path == null || path.Count == 0)
                yield break;

            if (_currentlyRunningTrains.Contains(player))
            {
                _currentlyRunningTrains.Remove(player);
                yield return null;
            }
            _currentlyRunningTrains.Add(player);

            var tr = _playerTrains[player].transform;
            tr.gameObject.SetActive(true);

            Vector3 nextPoint;
            var nextRotation = Utilities.GetCardinalRotation(Utilities.CardinalBetween(path[0], path[1]));

            tr.position = Utilities.GetPosition(path[0]);
            tr.rotation = nextRotation;

            for (int i = 0; i < path.Count - 1; ++i)
            {
                nextPoint = Utilities.GetPosition(path[i + 1]);
                nextRotation = Utilities.GetCardinalRotation(Utilities.CardinalBetween(path[i], path[i + 1]));

                while (tr.position != nextPoint)
                {
                    if (!_currentlyRunningTrains.Contains(player))
                    {
                        tr.position = Utilities.GetPosition(path.Last());
                        tr.rotation = Utilities.GetCardinalRotation(
                            Utilities.CardinalBetween(path[path.Count - 2], path.Last())
                        );
                        yield break;
                    }

                    tr.position = Vector3.MoveTowards(tr.position, nextPoint, speed * Time.deltaTime);
                    tr.rotation = Quaternion.Slerp(tr.rotation, nextRotation, speed * 2.5f * Time.deltaTime);
                    yield return null;
                }
            }
            OnTrainMovementFinished?.Invoke(_singleton, null);
        }
        #endregion
    }
}
