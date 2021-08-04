using System;
using System.Collections.Generic;
using UnityEngine;
using Rails.ScriptableObjects;
using System.Collections.ObjectModel;
using Rails.Rendering;
using Rails.Controls;
using Rails.Data;
using Rails.Systems;
using Rails.Collections;
using System.Linq;
using Assets.Scripts.Data;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Rails {
  public class Manager : MonoBehaviour {
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

    #region Properties
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
    /// A collection of game rules.
    /// </summary>
    public GameRules _rules;
    /// <summary>
    /// Stores the layout of the map, including nodes, cities, goods, etc.
    /// </summary>
    [SerializeField]
    public MapData MapData;
    /// <summary>
    /// The cost to build a track to a respective NodeType
    /// </summary>
    public static readonly ReadOnlyDictionary<NodeType, int> NodeCosts = new ReadOnlyDictionary<NodeType, int>(
        new Dictionary<NodeType, int>
        {
            { NodeType.Clear,      1 },
            { NodeType.Mountain,   2 },
            { NodeType.SmallCity,  3 },
            { NodeType.MediumCity, 3 },
            { NodeType.MajorCity,  5 },
            { NodeType.Water, 1000   },
        }
    );
    /// <summary>
    /// The cost to build over a river
    /// </summary>
    public const int RiverCost = 2;
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
    //public GameObject PlayerInfoPanel;

		#endregion // Properties

		#region Private Fields
    /// <summary>
    /// Stores the tracks on the map.
    /// </summary>
    private static TrackGraph<int> Tracks = new TrackGraph<int>(-1);
    /// <summary>
    /// Stores info for all players.
    /// </summary>
    private PlayerInfo[] players;
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
    private List<Queue<NodeId>> buildPaths;
    private List<Route> routes;
    private GameToken _highlightToken;
    #endregion

    #region Unity Events

    private void Awake() {
      // set singleton reference on awake
      _singleton = this;
    }

    private void Start() {
      GameGraphics.Initialize(MapData);
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
        EnqueueNode(GameInput.MouseNodeId);
      }
      if (GameInput.DeleteJustPressed) {
        ClearQueue();
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
		// Moves the train to final node in path.
		public void MoveTrain() {
      // TODO: Move train to last pushed node.
      // Moving only updates the phase.
      GameLogic.UpdatePhase(PhasePanels, ref currentPhase, maxPhases);
      return;
    }
    // Discards the player's hand.
    public void DiscardHand() {
      // TODO: removing and refilling player's hand
      // Ends the turn.
      GameLogic.IncrementPlayer(ref currentPlayer, _rules.maxPlayers);
      return;
    }

    // Builds the track between the nodes in path.
    public void BuildTrack() {
      GameLogic.BuildTrack(Tracks, routes, player.color);
      EndTurn();
      return;
    }
    // Upgrades the player's train.
    public void UpgradeTrain(int choice) {
      GameLogic.UpgradeTrain(ref player.trainStyle, ref player.money, choice, _rules.trainUpgrade);
      EndTurn();
      return;
    }

		// Places the current player's train at position.
		public void PlaceTrain(NodeId position) {
      player.train_position = position;
      return;
    }

    // Add Node to Queue
    public void EnqueueNode(NodeId node) {
      // Add to move queue if in move phase.
      if (currentPhase == 0)
        player.movepath.Enqueue(node);
			// Add to build queue if in build phase.
			else {
        buildPaths[currentPath].Enqueue(node);
        PlannedTracks();
			}
		}
    // Clear current Queue
    public void ClearQueue() {
      // Move Phase
      if (currentPhase == 0)
        player.movepath.Clear();
			else {
        GameGraphics.DestroyPotentialTrack(routes[currentPath]);
        buildPaths[currentPath].Clear();
        PlannedTracks();
			}
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

      //
      buildPaths = new List<Queue<NodeId>>();
      buildPaths.Add(new Queue<NodeId>());
      routes = new List<Route>();
      routes.Add(null);

      // Initiate all player info.
      players = new PlayerInfo[_rules.maxPlayers];
      for (int p = 0; p < _rules.maxPlayers; p++)
        players[p] = new PlayerInfo("Player " + p, Color.white, _rules.moneyStart, 0);
      player = players[currentPlayer];

      // Deactivate all panels just in case.
      for (int u = 0; u < maxPhases; u++)
        PhasePanels[u].SetActive(false);

      // Activate first turn panel.
      PhasePanels[1].SetActive(true);

      return;
    }
    // Ends the turn and changes phase.
    private void EndTurn() {
      if (maxPhases >= 0) {
        GameLogic.IncrementPlayer(ref currentPlayer, _rules.maxPlayers);
        GameLogic.UpdatePhase(PhasePanels, ref currentPhase, maxPhases);
      }
      else {
        GameLogic.BuildTurn(ref currentPlayer, ref currentPhase, _rules.maxPlayers);
      }
      player = players[currentPlayer];
      return;
    }
    // Show the planned route on the map.
    private void PlannedTracks() {
      for (int p = 0; p < buildPaths.Count; p++) {
        if (buildPaths[p].Count > 1) {
          if (routes[p] != null)
            GameGraphics.DestroyPotentialTrack(routes[p]);
          routes[p] = Pathfinding.CheapestBuild(Tracks, MapData, buildPaths[p].ToArray());
          GameGraphics.GeneratePotentialTrack(routes[p]);
        }
      }
      return;
    }
    #endregion
  }
}