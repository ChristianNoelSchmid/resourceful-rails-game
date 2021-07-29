using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rails.ScriptableObjects;
using UnityEngine.InputSystem;
using System.Collections.ObjectModel;

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

    #region Map
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
    /// The Cost for a player to use another player's track
    /// </summary>
    public const int AltTrackCost = 4;

    /// <summary>
    /// Controls the spacing between nodes in terms of Unity units.
    /// </summary>
    public float WSSize = 1f;

    /// <summary>
    /// Stores the layout of the map, including nodes, cities, goods, etc.
    /// </summary>
    [SerializeField]
    public MapData MapData;

    /// <summary>
    /// Stores the tracks on the map.
    /// </summary>
    [SerializeField]
    private Dictionary<NodeId, int[]> Tracks = new Dictionary<NodeId, int[]>();
		#endregion // Properties

    #region Utilities
    public Vector3 GetPosition(NodeId id) {
      var w = 2 * WSSize;
      var h = Mathf.Sqrt(3) * WSSize;
      var wspace = 0.75f * w;
      var pos = new Vector3(id.X * wspace, 0, id.Y * h);
      int parity = id.X & 1;
      if (parity == 1)
        pos.z += h / 2;

      return pos;
    }

    /// <summary>
    /// Returns a collection of NodeIds of nodes that lie within the given circle.
    /// </summary>
    /// <param name="position">Position of the circle</param>
    /// <param name="radius">Radius of circle</param>
    public List<NodeId> GetNodeIdsByPosition(Vector3 position, float radius) {
      List<NodeId> nodeIds = new List<NodeId>();
      var w = 2 * WSSize;
      var h = Mathf.Sqrt(3) * WSSize;
      var wspace = 0.75f * w;

      // Algorithm generates a bounding square
      // It then iterates all nodes within that box
      // Checking if the world space position of that node is within the circle

      // get grid-space node position
      Vector2 centerNodeId = new Vector2(position.x / wspace, position.z / h);
      if ((int)centerNodeId.x % 2 == 1)
        centerNodeId.y -= h / 2;

      // determine grid-space size of radius
      int extents = Mathf.CeilToInt(radius / wspace);

      // generate bounds from center and radius
      // clamp min to be no less than 0
      // clamp max to be no more than Size-1
      int minX = Mathf.Max(0, (int)centerNodeId.x - extents);
      int maxX = Mathf.Min(Size - 1, Mathf.CeilToInt(centerNodeId.x) + extents);
      int minY = Mathf.Max(0, (int)centerNodeId.y - extents);
      int maxY = Mathf.Min(Size - 1, Mathf.CeilToInt(centerNodeId.y) + extents);

      // iterate bounds
      for (int x = minX; x <= maxX; ++x) {
        for (int y = minY; y <= maxY; ++y) {
          // get position from NodeId
          var nodeId = new NodeId(x, y);
          var pos = GetPosition(nodeId);

          // check if position is within circle
          if (Vector3.Distance(pos, position) < radius)
            nodeIds.Add(nodeId);
        }
      }

      return nodeIds;
    }

    /// <summary>
    /// Inserts a new track onto the Map, based on position and direction.
    /// </summary>
    /// <param name="player">The player who owns the track</param>
    /// <param name="position">The position the track is placed</param>
    /// <param name="towards">The cardinal direction the track moves towards</param>
    private void InsertTrack(int player, NodeId position, Cardinal towards) {
      // If Cardinal data doesn't exist for the point yet,
      // insert and initialize the data
      if (!Tracks.ContainsKey(position)) {
        Tracks[position] = new int[(int)Cardinal.MAX_CARDINAL];
        for (int i = 0; i < (int)Cardinal.MAX_CARDINAL; ++i)
          Tracks[position][i] = -1;
      }

      Tracks[position][(int)towards] = player;

      // As Tracks is undirected, insert a track moving the opposite way from the
      // target node as well.
      InsertTrack(player, Utilities.PointTowards(position, towards), Utilities.ReflectCardinal(towards));
    }

    #endregion // Utilities
    #endregion // Map

    #region Graphics
    [SerializeField]
    public MapTokenTemplate MapGraphics;
    private GameObject[] PlayerTrains;
    private GameObject GetNodeType(MapTokenTemplate mapStyle, Node node) {
      GameObject model = null;
      switch (node.Type) {
        case NodeType.Clear:
          model = mapStyle.Clear;
          break;
        case NodeType.Mountain:
          model = mapStyle.Mountain;
          break;
        case NodeType.SmallCity:
          model = mapStyle.SmallCity;
          break;
        case NodeType.MediumCity:
          model = mapStyle.MediumCity;
          break;
        case NodeType.MajorCity:
          model = mapStyle.MajorCity;
          break;
        case NodeType.Water:
          model = null;
          break;
      }
      return model;
    }
    private void CreateNodes() {
      for (int x = 0; x < Size; x++) {
        for (int y = 0; y < Size; y++) {
          // draw node
          Node node = MapData.Nodes[(y * Size) + x];
          Vector3 pos = GetPosition(node.Id);
          GameObject model = GetNodeType(MapGraphics, node);
          if (model) {
            GameObject inst = Instantiate(model, transform);
            inst.transform.position = pos;
          }
        }
      }
    }
    private void CreateTrains() {
      for (int p = 0; p < MaxPlayers; p++) {
        PlayerTrains[p] = Instantiate(trainData[0].model, transform);
        PlayerTrains[p].SetActive(false);
        // TODO: Set the material to the correct color.
			}
		}
    private IEnumerator MoveTrain(int player, NodeId start, NodeId end, float speed) {
      float norm = 0f;
      float time = 0f;
      Vector3 startv = GetPosition(start);
      Vector3 endv = GetPosition(end);
      float distance = Vector3.Distance(startv, endv);
      Vector3 pos;

      while (norm <= 1f) {
        time += Time.deltaTime;
        norm = speed * time / distance;
        pos = Vector3.Slerp(startv, endv, norm);
        PlayerTrains[player].transform.position = pos;

        yield return null;
			}
		}
    #endregion

    #region Unity Events

    private void Awake() {
      // set singleton reference on awake
      _singleton = this;
      CreateNodes();
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
          var pos = GetPosition(node.Id);
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
                Gizmos.DrawLine(pos, GetPosition(nextNodeId));
              }
            }
          }
        }
      }

      foreach (var postDraw in postDraws)
        postDraw?.Invoke();
    }

#endif

    private void Update() {
      InputUpdate();
    }

    #endregion

    #region Input

    void InputUpdate() {

    }

    #endregion

    #region Game Loop
    public struct PlayerInfo {
      public string name;
      public Color color;
      public int money;
      public int trainStyle;
      public int majorcities;
      public NodeId train_position;
      public Stack<NodeId> movepath;
      public List<Stack<NodeId>> buildpaths;
      public int currentPath;
      public int movePathStyle;
      public int buildPathStyle;

      public PlayerInfo(string name, Color color, int money, int train) {
        this.name = name;
        this.color = color;
        this.money = money;
        this.trainStyle = train;
        majorcities = 0;
        train_position = new NodeId(0, 0);
        movepath = new Stack<NodeId>();
        buildpaths = new List<Stack<NodeId>>();
        currentPath = 0;
        movePathStyle = 0;
        buildPathStyle = 0;
      }
    }

    #region Public Data
    /// <summary>
    /// The number of players playing this game.
    /// </summary>
    public int MaxPlayers = 6;
    /// <summary>
    /// The amount of money each player starts with.
    /// </summary>
    public int MoneyStart = 50;
    /// <summary>
    /// The max amount of money that can be spent building.
    /// </summary>
    public int MaxBuild = 20;
    /// <summary>
    /// The cost to for players to upgrade their train.
    /// </summary>
    public int TrainUpgrade = 20;
    /// <summary>
    /// The number of major cities that must be connected to win.
    /// </summary>
    public int Win_MajorCities = 6;
    /// <summary>
    /// The amount of money needed to win.
    /// </summary>
    public int Win_Money = 250;
    
    // The cost to build a track to a respective NodeType
    public static readonly ReadOnlyDictionary<NodeType, int> NodeCosts = new ReadOnlyDictionary<NodeType, int>(
        new Dictionary<NodeType, int>
        {
            { NodeType.Clear,      1 },
            { NodeType.Mountain,   2 },
            { NodeType.SmallCity,  3 },
            { NodeType.MediumCity, 3 },
            { NodeType.MajorCity,  5 },
        }
    );

    // The cost to build over a river
    public const int RiverCost = 2;

    /// <summary>
    /// The trains that players can use.
    /// </summary>
    public TrainData[] trainData;
    /// <summary>
    /// UI window that shows stats of the current player.
    /// </summary>
    public GameObject PlayerInfoPanel;
    /// <summary>
    /// UI windows that show the controls for each phase.
    /// </summary>
    public GameObject[] PhasePanels;
    #endregion

    #region Private Data
    PlayerInfo[] players;
    PlayerInfo player;
    int phases;
    int currentPlayer = 0;
    int currentPhase = -2;
    #endregion

    /// <summary>
    /// Sets up the current game.
    /// </summary>
    private void GameLoopSetup() {
      phases = PhasePanels.Length;

      // Initiate all player info.
      players = new PlayerInfo[MaxPlayers];
      for (int p = 0; p < MaxPlayers; p++)
        players[p] = new PlayerInfo("Player " + p, Color.white, MoneyStart, 0);

      // Deactivate all panels just in case.
      for (int u = 0; u < phases; u++)
        PhasePanels[u].SetActive(false);

      // Activate first turn panel.
      PhasePanels[1].SetActive(true);
      player = players[currentPlayer];
      UpdatePlayerInfo();
    }

    #region Player Actions
    // Moves the train to final node in path.
    public void MoveTrain() {
      // TODO: Move train to last pushed node.

      // Moving only updates the phase.
      UpdatePhase();
      return;
    }
    // Discards the player's hand.
    public void DiscardHand() {
      // TODO: removing and refilling player's hand
      // Ends the turn.
      IncrementPlayer();
      return;
    }

    // Builds the track between the nodes in path.
    public void BuildTrack() {
      BuildTrack_();
      // Ends the turn and changes phase.
      if (phases < 0)
        BuildTurn();
      else
        NormalTurn();
      return;
    }
    // Upgrades the player's train.
    public void UpgradeTrain(int choice) {
      UpgradeTrain_(choice);
      // Ends the turn and changes phase.
      if (phases < 0)
        BuildTurn();
      else
        NormalTurn();
      return;
    }
    // Places the current player's train at position.
    public void PlaceTrain(NodeId position) {
      player.train_position = position;
      return;
    }
    #endregion // Player Actions

    #region Path Methods
    // Adds nodes to a path stack
    // Used for building and movement
    public void PushNode(NodeId node) {
      // Phase 0 is the movement phase.
      if (currentPhase == 0 && !player.movepath.Contains(node)) {
        player.movepath.Push(node);
        return;
      }
      // Other phases involve building.
      // Check every path for existing node.
      foreach (Stack<NodeId> stack in player.buildpaths) {
        if (stack.Contains(node))
          return;
      }
      if (player.currentPath == -1) {
        player.buildpaths.Add(new Stack<NodeId>());
        player.currentPath++;
      }
      player.buildpaths[player.currentPath].Push(node);
      return;
    }
    public NodeId PopNode() {
      if (currentPhase == 0) {
        return player.movepath.Pop();
      }
      return player.buildpaths[player.currentPath].Pop();
    }
    public void ClearPath(int path) {
      // Clear the path specified.
      if (currentPhase == 0) {
        player.movepath.Clear();
      }
      player.buildpaths.RemoveAt(path);
      if (player.buildpaths.Count == 0)
        player.currentPath = -1;
      return;
    }
    public void SwitchPath(int path) {
      // Switch to a different building path.
      player.currentPath = path;
      return;
    }
    #endregion // Path

    #region Private Methods
    // Updates current player through normal turns.
    private void NormalTurn() {
      IncrementPlayer();
      UpdatePhase();
      return;
    }
    // Updates current player through the intial build turns.
    private void BuildTurn() {
      // Phase -2, build turns, normal player order.
      // Phase -1, build turns, reverse player order.
      // Phase 0, normal turns, place trains.
      switch (currentPhase) {
        case -2: IncrementPlayer(); break;
        case -1: DecrementPlayer(); break;
      }
      if (currentPlayer == 5 || currentPlayer == 0) {
        UpdatePhase();
      }
      if (currentPhase == 0) {
        // TODO: Change buttons to normal build/upgrade methods.

      }
      return;
    }
    // Private method for building.
    private void BuildTrack_() {
      // TODO: Build track between all nodes in stack.
      List<Route> routes = new List<Route>();

      foreach (Stack<NodeId> stack in player.buildpaths) {
        NodeId start;
        while (stack.Count != 0) {
          start = stack.Pop();

        }
      }

      return;
    }
    // Private method for upgrading.
    private void UpgradeTrain_(int choice) {
      // If player doesn't have enough money, don't upgrade
      if (player.money < TrainUpgrade) {
        // TODO: Activate failure UI message here.
        return;
      }

      // Deduct value from player's money stock and change train value.
      player.money -= TrainUpgrade;
      player.trainStyle = choice;
      Debug.Log(currentPlayer + " $" + player.money);
      return;
    }
    // Changes the current player
    private int IncrementPlayer() {
      currentPlayer += 1;
      if (currentPlayer >= MaxPlayers)
        currentPlayer = 0;
      UpdatePlayerInfo();
      return currentPlayer;
    }
    // Changes players for switchback start.
    private int DecrementPlayer() {
      currentPlayer -= 1;
      if (currentPlayer < 0)
        currentPlayer = 0;
      UpdatePlayerInfo();
      return currentPlayer;
    }
    // Updates name and money amount. Placeholder.
    private void UpdatePlayerInfo() {
      //Transform playertext = PlayerInfoPanel.transform.Find("Player");
      //playertext.GetComponent<TMP_Text>().text = "Player #" + (currentPlayer + 1);
      //playertext = PlayerInfoPanel.transform.Find("Money");
      //playertext.GetComponent<TMP_Text>().text = "$" + players[currentPlayer].money;
    }
    // Cycles through UI screens
    private int UpdatePhase() {
      PhasePanels[currentPhase].SetActive(false);
      currentPhase += 1;
      if (currentPhase >= phases)
        currentPhase = 0;
      PhasePanels[currentPhase].SetActive(true);
      return currentPhase;
    }
    // Check if the current player has won.
    private bool CheckWin() {
      if (player.majorcities >= Win_MajorCities &&
        player.money >= Win_Money) {
        return true;
      }
      return false;
    }
    #endregion // Private

    #endregion // Game Loop
  }
}