using System;
using System.Collections.Generic;
using UnityEngine;
using Rails.ScriptableObjects;
using System.Collections.ObjectModel;
using Rails.Rendering;
using Rails.Controls;
using Rails.Data;
using Rails.Systems;
using TMPro;
using Rails.UI;
using System.Linq;
using Assets.Scripts.Data;
using Rails.Collections;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Rails {
    public class Manager : MonoBehaviour {

				#region Events
        // Turn End
				public delegate void OnTurnEndEventHandler(Manager manager);
        public event OnTurnEndEventHandler OnTurnEnd;

        // Player Info Update
        public delegate void OnPlayerInfoUpdateEventHandler(Manager manager);
        public event OnPlayerInfoUpdateEventHandler OnPlayerInfoUpdate;

        // Phase Change
        public delegate void OnPhaseChangeHandler(Manager manager);
        public event OnTurnEndEventHandler OnPhaseChange;

        // Build Track
        public delegate void OnBuildTrackHandler(Manager manager);
        public event OnTurnEndEventHandler OnBuildTrack;

        public EventHandler<TrainCityInteraction> OnTrainMeetsCityHandler;
        public EventHandler OnTrainMeetsCityCompletion;

        #endregion

        #region Singleton

        private static Manager _singleton = null;

        /// <summary>
        /// Manager singleton
        /// </summary>
        public static Manager Singleton {
            get {
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

        #region Public Fields
        /// <summary>
        /// Map size.
        /// </summary>
        public const int Size = 64;
        /// <summary>
        /// Max number of cities.
        /// </summary>
        public const int MaxCities = 32;
        /// <summary>
        /// Max number of goods.
        /// </summary>
        public const int MaxGoods = 64;
        /// <summary>
        /// Controls the spacing between nodes in terms of Unity units.
        /// </summary>
        public float WSSize = 1f;
        /// <summary>
        /// The value representing a MajorCity NodeType.
        /// Used when setting down initial MajorCity tracks,
        /// Tracks values and ghost tracks.
        /// </summary>
        public const int MajorCityIndex = -2;

        /// <summary>
        /// A collection of game rules.
        /// </summary>
        public GameRules _rules;
        /// <summary>
        /// Stores the layout of the map, including nodes, cities, goods, etc.
        /// </summary>
        [SerializeField]
        public MapData MapData;
        /// <summary>
        /// The trains that players can use.
        /// </summary>
        public TrainData[] trainData;
        /// <summary>
        /// UI windows that show the controls for each phase.
        /// </summary>
        public GameObject[] PhasePanels;
        /// <summary>
        /// UI window that shows stats of the current player.
        /// </summary>
        public GameHUDManager GameHUDObject;
        /// <summary>
        /// Rules set at the start of the game.
        /// </summary>
        public GameStartRules _startRules;
        /// <summary>
        /// Stores info for all players.
        /// </summary>
        public PlayerInfo[] Players;
        /// <summary>
        /// Gets the index of the current player.
        /// </summary>
        public int CurrentPlayer { 
            get { return currentPlayer; } 
        }
        /// <summary>
        /// 
        /// </summary>
        public int CurrentPath { 
            get { return currentPath; } 
            set { SetPath(value); }
        }
        #endregion // Properties

        #region Private Fields
        /// <summary>
        /// Stores the tracks on the map.
        /// </summary>
        private static TrackGraph<int> Tracks = new TrackGraph<int>(-1);
        /// <summary>
        /// A reference to the current players info.
        /// </summary>
        private PlayerInfo player;
        /// <summary>
        /// Total number of phases in a turn.
        /// </summary>
        private int maxPhases;
        /// <summary>
        /// Player number whose turn it currently is.
        /// </summary>
        private int currentPlayer;
        /// <summary>
        /// Phase of the turn of current player.
        /// </summary>
        private int currentPhase;
        /// <summary>
        /// 
        /// </summary>
        private int currentPath;
        private int currentNodeinPath;
        private List<List<NodeId>> buildPaths;
        private Route moveRoute = null;
        private List<Route> buildRoutes;
        private GameToken _highlightToken;

        private bool _movingTrain = false;
        private TrainMovement _trainMovement;
        #endregion

        #region Unity Events

        private void Awake() {
            // set singleton reference on awake
            _singleton = this;
            _startRules = FindObjectOfType<GameStartRules>();
            _rules = MapData.DefaultRules;
            _trainMovement = GetComponent<TrainMovement>();

            // generate start rules if empty
            if (_startRules == null) {
                GameObject go = new GameObject("start rules");
                _startRules = go.AddComponent<GameStartRules>();
                _startRules.Players = new StartPlayerInfo[2]
                {
                    new StartPlayerInfo() { Name = "Player 1", Color = Color.red },
                    new StartPlayerInfo() { Name = "Player 2", Color = Color.blue }
                };
            }

            _trainMovement.OnMovementFinished += (_, __) => _movingTrain = false;
            _trainMovement.OnMeetsCity += (_, interaction) => Debug.Log($"Train {interaction.PlayerIndex} meets with City {interaction.City.Name}");
        }

        private void Start()
        {
            GameGraphics.Initialize(MapData, _startRules.Players.Length, _startRules.Players.Select(p => p.Color).ToArray());
            Pathfinding.Initialize(_rules, Tracks, MapData);
            Deck.Initialize();
            SetupMajorCityTracks();
            GameLoopSetup();
        }
 
        private void Update() {
            _highlightToken?.ResetColor();
            var highlightToken = GameGraphics.GetMapToken(GameInput.MouseNodeId);

            if (highlightToken != null) {
                highlightToken.SetColor(Color.yellow);
                _highlightToken = highlightToken;
            }
            if (GameInput.SelectJustPressed && GameInput.MouseNodeId.InBounds) {
                Debug.Log("player.trainPosition = " + player.trainPosition.ToString());
                if (currentPhase == 0 && !player.trainPlaced) {
                    Debug.Log("Place Train");
                    PlaceTrain(GameInput.MouseNodeId);
				}
				else {
                    Debug.Log("Add Node");
                    AddNode(currentPath, GameInput.MouseNodeId);
				}
            }
            if (GameInput.DeleteJustPressed) {
                ClearPath(currentPath);
            }
            if (GameInput.EnterJustPressed) {
                BuildTrack();
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos() {
            List<Action> postDraws = new List<Action>();
            if (MapData == null || MapData.Nodes == null || MapData.Nodes.Length == 0)
                return;

            var labelStyle = new GUIStyle(GUI.skin.GetStyle("Label"));
            labelStyle.alignment = TextAnchor.UpperCenter;
            labelStyle.fontSize = 16;
            labelStyle.fontStyle = FontStyle.Bold;

            for (int x = 0; x < Size; x++) {
                for (int y = 0; y < Size; y++) {
                    // draw node
                    var node = MapData.Nodes[(y * Size) + x];
                    var pos = Utilities.GetPosition(node.Id);
                    Gizmos.color = Utilities.GetNodeColor(node.Type);
                    Gizmos.DrawCube(pos, Vector3.one * WSSize * 0.3f);

                    //
                    if (node.CityId >= 0 && node.CityId < MapData.Cities.Count) {
                        var city = MapData.Cities[node.CityId];
                        if (node.Type == NodeType.MajorCity || node.Type == NodeType.MediumCity || node.Type == NodeType.SmallCity) {

                            postDraws.Add(() =>
                            {
                                Handles.Label(pos + Vector3.up, city.Name, labelStyle);
                            });

                        }
                    }

                    // draw segments
                    // we iterate only bottom-right half of segments to prevent drawing them twice
                    var segments = MapData.GetNodeSegments(node.Id);
                    for (Cardinal c = Cardinal.NE; c <= Cardinal.S; ++c) {
                        // get segment
                        var segment = segments[(int)c];
                        if (segment != null) {
                            // get neighboring nodeid
                            var nextNodeId = Utilities.PointTowards(node.Id, c);
                            if (nextNodeId.InBounds) {
                                // draw line to
                                Gizmos.color = Utilities.GetSegmentColor(segment.Type);
                                Gizmos.DrawLine(pos, Utilities.GetPosition(nextNodeId));
                            }
                        }
                    }
                }
            }

            foreach (var postDraw in postDraws)
                postDraw?.Invoke();
        }
#endif
        #endregion

        #region Public
        // General gameplay methods.

        // Moves the train to final node in path.
        public void MoveTrain() => StartCoroutine(CMoveTrain());
        private IEnumerator CMoveTrain() {
            
            var movePoints = Mathf.Min(_rules.TrainSpecs[player.trainStyle].movePoints, moveRoute.Distance); 

            player.moveSegments.RemoveAt(0);
            for (int i = 0; i < movePoints; ++i)
            {
                if (moveRoute.Nodes[i] == player.moveSegments[0])
                    player.moveSegments.RemoveAt(0);
            }

            if(player.moveSegments[0] != player.trainPosition)
                player.moveSegments.Insert(0, player.trainPosition);
            
            player.trainPosition = moveRoute.Nodes.Last();

            _movingTrain = true;
            _trainMovement.MoveTrain(currentPlayer, moveRoute.Nodes.Take(movePoints + 1).ToList());

            while (_movingTrain)
                yield return null;

            // Moving only updates the phase.
            GameLogic.UpdatePhase(PhasePanels, ref currentPhase, maxPhases);
            OnPhaseChange?.Invoke(this);
        }

        // Discards the player's hand.
        public void DiscardHand() {
            // Remove and refill players' hand
            foreach (Demand[] card in player.demandCards) {
                Deck.Discard(card);
						}
            for (int c = 0; c < _rules.HandSize; c++) {
                player.demandCards.Add(Deck.DrawOne());
						}
            // Ends the turn.
            GameLogic.IncrementPlayer(ref currentPlayer, Players.Length);
            OnPlayerInfoUpdate?.Invoke(this);
            return;
        }

        // Builds the track between the nodes in path.
        public void BuildTrack() {
            // Build the tracks
            if (buildPaths.Count != 0) {
                int cost = 0;
                foreach (Route route in buildRoutes) {
                    cost += route.Cost;
                    if (cost > _rules.MaxBuild)
                        return;
								}

                player.money -= GameLogic.BuildTrack(Tracks, buildRoutes, currentPlayer, player.color, _rules.MaxBuild);
                OnBuildTrack?.Invoke(this);
            }

            // Clear the lists
            buildPaths.Clear();
            buildPaths.Add(new List<NodeId>());

            // End the turn
            EndTurn();
            return;
        }
        // Upgrades the player's train.
        public void UpgradeTrain(int choice) {
            GameLogic.UpgradeTrain(ref player.trainStyle, ref player.money, choice, _rules.TrainUpgrade);
            EndTurn();
            return;
        }

        // Places the current player's train at position.
        public bool PlaceTrain(NodeId position) {
            NodeType type = MapData.GetNodeAt(position).Type;
            bool city = false;
            switch (type) {
                case NodeType.SmallCity:
                case NodeType.MediumCity:
                case NodeType.MajorCity:
                    city = true;
                    break;
            }
            if (city) {
                player.trainPosition = position;
                player.trainPlaced = true;
                player.moveSegments.Insert(0, player.trainPosition);
                Debug.Log("Train position = " + player.trainPosition.ToString());
		    }

            GameGraphics.PositionTrain(currentPlayer, position);
            return city;
        }
				#endregion

				#region Public Path Methods
				// Path Methods

				// Adds a new path to the end of the list.
				public int CreateNewPath() {
            buildPaths.Add(new List<NodeId>());
            return buildPaths.Count - 1;
				}
        // Sets the index of the current build path.
        public void SetPath(int path) {
            while (path < 0)
                path += buildPaths.Count;
            while (path >= buildPaths.Count)
                path -= buildPaths.Count;
            currentPath = path;
            return;
				}
        // Returns the list of nodes of the specified path.
        public List<NodeId> GetPath(int path) {
            if (path >= 0 && path < buildPaths.Count)
                return buildPaths[path];
            return null;
				}
        // Returns the number of paths in the build paths list.
        public int GetPathCount() {
            return buildPaths.Count;
				}
        // Adds a node to the list.
        // Clear current Queue
        public void ClearPath(int path) {
            // Move Phase
            if (currentPhase == 0)
            {
                GameGraphics.HighlightRoute(moveRoute, null);
                player.moveSegments.Clear();
                player.moveSegments.Add(player.trainPosition);
            }
            else
            {
                GameGraphics.DestroyPotentialTrack(buildRoutes[path]);
                buildPaths[path].Clear();
                if (buildPaths.Count == 0)
                    buildPaths.Add(new List<NodeId>());
                PlannedTracks();
            }
            return;
        }
        public void RemovePath(int path) {
            // Removes a path from the build paths.
            buildPaths[path].RemoveAt(path);
            return;
				}
        public void AddNode(int path, NodeId node) {
            // Add to player's movesegments if in move phase.
            if (currentPhase == 0)
            {
                player.moveSegments.Add(node);
                PlannedRoute();
            }
            // Add to build queue if in build phase.
            else
            {
                buildPaths[path].Add(node);
                PlannedTracks();
            }
            return;
        }
        // Removes a node from the list.
        public void RemoveNode(int path, int index) {
            if (currentPhase == 0) {
                player.moveSegments.RemoveAt(index);
		    }
            else {
                buildPaths[path].RemoveAt(index);
						}
            return;
				}
        // Sets the current node in the path.
        public void SetNode(int path, int index) {
            while (index < 0)
                index += buildPaths[path].Count;
            while (index >= buildPaths[path].Count)
                index -= buildPaths[path].Count;
            currentNodeinPath = index;
				}
        #endregion

        #region Private
        /// <summary>
        /// Sets up the current game.
        /// </summary>
        private void GameLoopSetup() {
            // Assign integers
            currentPlayer = 0;
            currentPhase = -2;
            currentPath = 0;
            maxPhases = PhasePanels.Length;

            // Create the path lists.
            buildPaths = new List<List<NodeId>>();
            buildPaths.Add(new List<NodeId>());
            buildRoutes = new List<Route>();
            buildRoutes.Add(null);

            // Initiate all player info.
            Players = new PlayerInfo[_startRules.Players.Length];
            for (int p = 0; p < Players.Length; p++)
                Players[p] = new PlayerInfo(_startRules.Players[p].Name,
                  _startRules.Players[p].Color, _rules.MoneyStart, 0);
            player = Players[currentPlayer];

            // Draw all players' cards.
            for (int c = 0; c < _rules.HandSize; c++) {
                for (int p = 0; p < Players.Length; p++) {
                    Players[p].demandCards.Add(Deck.DrawOne());
								}
						}

            // Deactivate all panels just in case.
            for (int u = 0; u < maxPhases; u++)
                PhasePanels[u].SetActive(false);

            // Activate first turn panel.
            PhasePanels[1].SetActive(true);
        }

        // Ends the turn and changes phase.
        private void EndTurn() {
            if (currentPhase < 0) {
                GameLogic.BuildTurn(ref currentPlayer, ref currentPhase, Players.Length);
            }
			      else {
                GameLogic.IncrementPlayer(ref currentPlayer, Players.Length);
			      }
            if (currentPhase >= 0) {
                GameLogic.UpdatePhase(PhasePanels, ref currentPhase, maxPhases);
                OnPhaseChange?.Invoke(this);
            }
            player = Players[currentPlayer];
            OnPlayerInfoUpdate?.Invoke(this);
            OnTurnEnd?.Invoke(this);
            return;
        }
        // Show the planned tracks on the map.
        private void PlannedTracks() {
            if (buildPaths.Count > 0)
            {
                if (buildRoutes != null)
                {
                    foreach (var route in buildRoutes)
                        GameGraphics.DestroyPotentialTrack(route);
                    buildRoutes.Clear();
                }
                buildRoutes = Pathfinding.CheapestBuilds(buildPaths);

                foreach (var route in buildRoutes)
                    GameGraphics.GeneratePotentialTrack(route, Color.yellow);
            }
            return;
        }
        // Show the planned route on the map.
        private void PlannedRoute() {
            if (player.moveSegments.Count > 0) {
                if (moveRoute != null) {
                    GameGraphics.HighlightRoute(moveRoute, null);
				}

                // Calculate the Route
                moveRoute = Pathfinding.CheapestMove(
                    currentPlayer,
                    _rules.TrainSpecs[player.trainStyle].movePoints,
                    player.moveSegments.ToArray());

                    var movePoints = Mathf.Min(_rules.TrainSpecs[player.trainStyle].movePoints, moveRoute.Distance);
                   
                    // Highlight the Route
                    GameGraphics.HighlightRoute(moveRoute.Nodes.Take(movePoints + 1).ToList(), player.color * 2.0f); 
                    GameGraphics.HighlightRoute(moveRoute.Nodes.Take(movePoints).ToList(), player.color);
		        }
            return;
				}
        
        /// <summary>
        /// Sets up the tracks that are automatically assigned to
        /// a Major City
        /// </summary>
        private void SetupMajorCityTracks()
        {
            for (int i = 0; i < Size * Size; ++i)
            {
                if (MapData.Nodes[i].Type == NodeType.MajorCity)
                {
                    var nodeId = NodeId.FromSingleId(i);
                    for (var c = Cardinal.N; c < Cardinal.MAX_CARDINAL; ++c)
                    {
                        var adjNodeId = Utilities.PointTowards(nodeId, c);
                        if (MapData.Nodes[adjNodeId.GetSingleId()].Type == NodeType.MajorCity &&
                           (!Tracks.TryGetEdgeValue(nodeId, c, out int e) || e == -1))
                        {
                            Tracks[nodeId, adjNodeId] = MajorCityIndex;
                            var route = new Route(0, new List<NodeId> { nodeId, adjNodeId });

                            GameGraphics.GeneratePotentialTrack(route, Color.clear);
                            GameGraphics.CommitPotentialTrack(route, Color.clear);
                        }
                    }
                }
            }
        }

        #endregion
    }
}