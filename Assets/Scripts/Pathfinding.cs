using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rails
{
    public static class Pathfinding
    {
        private const int _maxPaths = 3;

        #region Data Structures

        /// <summary>
        /// Represents information related to a path found by the
        /// Pathfinder class.
        /// </summary>
        public class PathData
        {
            public int Distance { get; set; }
            public int Cost { get; set; }
            public List<NodeId> Nodes { get; set; }

            public PathData(int distance, int cost, List<NodeId> nodes)
            {
                Distance = distance;
                Cost = cost;
                Nodes = nodes;
            }

        }

        /// <summary>
        /// A comparable object representing the position,
        /// and weight to reach a node given a known start
        /// position. Used in pathfinding
        /// </summary>
        class WeightedNode : IComparable<WeightedNode>
        {
            public NodeId Position { get; set; }
            public int Weight { get; set; }
            public bool[] AltTracksPaid { get; set; }

            public int SpacesLeft { get; set; }
            
            // Compared by Weight
            public int CompareTo(WeightedNode other) => Weight.CompareTo(other.Weight);
        }

        public class PriorityQueue<T> where T: IComparable<T>
        {
            private List<T> items;

            public PriorityQueue() => items = new List<T>();

            public T Peek() => items.FirstOrDefault();
            public T Pop()
            {
                var item = items.FirstOrDefault();

                if(items.Count > 0)
                {
                    items[0] = items.Last();

                    int index = 0;
                    int childIndex = 1;

                    bool traversed = true;

                    while(traversed)
                    { 
                        traversed = false;

                        if(childIndex > items.Count - 1) 
                            break;

                        if(childIndex + 1 < items.Count && items[childIndex].CompareTo(items[childIndex + 1]) > 0)
                            childIndex += 1;

                        if(items[index].CompareTo(items[childIndex]) > 0)
                        {
                            T temp = items[index];
                            items[index] = items[childIndex];
                            items[childIndex] = temp;

                            traversed = true;
                        }

                        index = childIndex;
                        childIndex = 2 * childIndex + 1;
                    } 

                    items.RemoveAt(items.Count - 1);
                }

                return item;
            }

            public void Insert(T item)
            {
                int index = items.Count;
                int parent = Mathf.FloorToInt((index - 1) / 2);

                items.Add(item);

                while(items[index].CompareTo(items[parent]) < 0)
                {
                    var temp = items[parent];
                    items[parent] = items[index];
                    items[index] = temp;

                    index = parent;
                    parent = Mathf.FloorToInt((index - 1) / 2);
                }
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Calculates the best tracks available for the given
        /// player who wishes to move from start to end.
        /// </summary>
        /// <param name="tracks">The tracks on the current map</param>
        /// <param name="player">The player that wants to move</param>
        /// <param name="start">The start point</param>
        /// <param name="end">The target point</param>
        /// <returns>The list of PathDatas representing the shortest tracks</returns>
        public static List<PathData> BestTracks(
            Dictionary<NodeId, int[]> tracks,
            int player, int speed, 
            NodeId start, NodeId end
        ) {
            // First determine the least cost track (if it exists)
            var leastCost = DjikstraLeastCostTrack(tracks, player, speed, start, end, true);
            if(leastCost == null) return null;

            // A copy of the track map - nodes and edges are removed from
            // this map while keeping the original unchanged
            var spurTracks = new Dictionary<NodeId, int[]>(tracks);

            // Create the returned list, starting with the initial least cost path
            var paths = new List<PathData> { leastCost };

            // A list of spur paths to be added to returned list
            var pathSpurs = new List<PathData>();

            var removedNodes = new HashSet<NodeId>();
            var removedEdges = new Dictionary<Tuple<NodeId, Cardinal>, int>();

            for(int k = 1; k <= _maxPaths - 2; ++k)
            {
                // Iterate through all nodes in the latest shortest path
                // added to the returned list
                for(int i = 0; i < paths[k - 1].Nodes.Count - 2; ++i)
                {
                    // Select a node to branch off of
                    var spurNode = paths[k - 1].Nodes[i];

                    // Create a sub-track from the root of the start of the
                    // chosen path to spurNode 
                    var rootPath = paths[k-1].Nodes.GetRange(0, i + 1);

                    // Remove the current edge to force the pathfinding algorithm
                    // to find a different route.
                    foreach(var p in paths.Select(path => path.Nodes))
                    {
                        if(p.Count >= rootPath.Count && p.GetRange(0, i + 1).SequenceEqual(rootPath))
                        {
                            if(spurTracks.ContainsKey(p[i]))
                            {
                                Cardinal c = Utilities.CardinalBetween(p[i], p[i+1]);
                                if(spurTracks[p[i]][(int)c] != -1)
                                {
                                    removedEdges.Add(Tuple.Create(p[i], c), spurTracks[p[i]][(int)c]);
                                    spurTracks[p[i]][(int)c] = -1;

                                    c = Utilities.CardinalBetween(p[i+1], p[i]);
                                    removedEdges.Add(Tuple.Create(p[i+1], c), spurTracks[p[i+1]][(int)c]);
                                    spurTracks[p[i+1]][(int)c] = -1;
                                } 
                            }
                        }   
                    }

                    foreach(var node in rootPath.Where(p => p != spurNode))
                    {
                        for(int e = 0; e < (int)Cardinal.MAX_CARDINAL; ++e)
                        {
                            if(spurTracks[node][e] != -1)
                                removedEdges.Add(Tuple.Create(node, (Cardinal)e), spurTracks[node][e]);
                        }
                        removedNodes.Add(node);
                        spurTracks.Remove(node);
                    }

                    // Calculate the shortest path from the spur node to the end node
                    var spurPath = DjikstraLeastCostTrack(spurTracks, player, speed, spurNode, end, true);

                    PathData totalPath;
                    if(spurPath != null)
                    {
                        totalPath = CreatePathData(tracks, player, speed, rootPath, spurPath.Nodes);

                        // If that path doesn't exist in pathSpurs, add it to the pathSpurs list
                        // to be considered as the shortest path
                        if(!pathSpurs.Any(p => p.Nodes.SequenceEqual(totalPath.Nodes)))
                            pathSpurs.Add(totalPath); 
                    }
                    foreach(var node in removedNodes)
                    {
                        spurTracks[node] = new int[(int)Cardinal.MAX_CARDINAL];
                        for(int c = 0; c < (int)Cardinal.MAX_CARDINAL; ++c)
                            spurTracks[node][c] = -1;
                    }
                    foreach(var edge in removedEdges.Keys)
                        spurTracks[edge.Item1][(int)edge.Item2] = removedEdges[edge];

                    removedNodes.Clear();
                    removedEdges.Clear();
                }
                // If there aren't enough shortest paths to continue the
                // loop, break and return what is present
                if(pathSpurs.Count == 0)
                    break;

                // Finally, sort pathSpurs to retrieve the shortest path generated
                // from the iteration of spur nodes. Add that to the returned list
                pathSpurs.Sort((p1, p2) => p1.Cost.CompareTo(p2.Cost));
                paths.Add(pathSpurs.First());
                pathSpurs.RemoveAt(0);
            }

            // Finally, add the least distance path as an added consideration
            // (if it isn't already part of the list)
            var leastDistance = LeastDistanceTrack(tracks, player, speed, start, end);
            if(leastDistance != null && !paths.Any(p => p.Nodes.SequenceEqual(leastDistance.Nodes)))
                paths.Insert(0, leastDistance);

            // Remove tracks that offer no benefit compared to others
            // (ie. distance and cost is greater than another's)
            for(int i = 0; i < paths.Count; ++i)
            {
                for(int j = 0; j < paths.Count; ++j)
                {
                    if(i == j) continue;
                    if(paths[i].Cost >= paths[j].Cost && paths[i].Distance >= paths[j].Distance)
                    {
                        paths.RemoveAt(i);
                        if(i > 0) --i;
                        if(j > 0) --j;
                    }
                }
            }

            paths.Sort((p1, p2) => p1.Distance.CompareTo(p2.Distance));

            return paths;
        }

        public static List<NodeId> LeastWeightPath(
            Dictionary<NodeId, int[]> tracks, 
            MapData mapData, int player,
            NodeId start, NodeId end
        ) {
            return null;
        }

        #endregion

        #region Private Methods        

        // Finds the lowest distance track available for the given player,
        // from a start point to end point.
        private static PathData LeastDistanceTrack(
            Dictionary<NodeId, int[]> tracks, 
            int player, int speed, 
            NodeId start, NodeId end
        ) {
            return DjikstraLeastCostTrack(tracks, player, speed, start, end, false);
        }

        /// <summary>
        /// Finds the lowest cost track available for the given player,
        /// from a start point to end point.
        /// </summary>
        /// <param name="tracks">The track nodes being traversed on</param>
        /// <param name="player">The player enacting the traversal</param>
        /// <param name="start">The start point of the traversal</param>
        /// <param name="end">The target point of the traversal</param>
        /// <returns>The lowest cost track</returns>
        private static PathData DjikstraLeastCostTrack(
            Dictionary<NodeId, int[]> tracks, 
            int player, int speed, 
            NodeId start, NodeId end,
            bool addAltTrackCost
        ) {
            if(start == end || !tracks.ContainsKey(start) || !tracks.ContainsKey(end))
                return null;

            // The list of nodes to return from method
            PathData path = null;
            // The true distances from start to considered point
            // (without A* algorithm consideration)
            var distMap = new Dictionary<NodeId, int>();
            // The list of all nodes visited, connected to their
            // shortest-path neighbors.
            var previous = new Dictionary<NodeId, NodeId>();
            // The next series of nodes to check, sorted by minimum weight
            var queue = new PriorityQueue<WeightedNode>();

            // The current considered node
            WeightedNode node;

            distMap.Add(start, 0);

            var startNode = new WeightedNode 
            {
                Position = start,
                Weight = 0,
                SpacesLeft = speed + 1,
                AltTracksPaid = new bool[6],
            };
            startNode.AltTracksPaid[player] = true;
            // To start things off, add the start node to the queue
            queue.Insert(startNode);

            // While there are still nodes to visit,
            // Find the lowest-weight one and determine
            // it's connected nodes.
            while((node = queue.Pop()) != null)
            { 
                // If the node's position is the target, build the path and assign
                // it to the returned PathData
                if(node.Position == end)
                {
                    path = CreatePathData(tracks, previous, player, speed, start, end);
                    break;
                }

                if(addAltTrackCost && node.SpacesLeft == 0)
                {
                    node.SpacesLeft = speed;
                    for(int i = 0; i < 6; ++i)
                        node.AltTracksPaid[i] = false;
                    node.AltTracksPaid[player] = true;
                }

                for(Cardinal c = Cardinal.N; c < Cardinal.MAX_CARDINAL; ++c)
                {
                    var newPoint = Utilities.PointTowards(node.Position, c);
                    if(tracks[node.Position][(int)c] != -1 && tracks.ContainsKey(newPoint))
                    {
                        var newNode = new WeightedNode
                        {
                            Position = newPoint, 
                            SpacesLeft = node.SpacesLeft - 1,
                        };
                        newNode.AltTracksPaid = node.AltTracksPaid.ToArray();

                        var newCost = distMap[node.Position] + 1;
                        int trackOwner = tracks[node.Position][(int)Utilities.CardinalBetween(node.Position, newPoint)];

                        // If the current track is owned by a different player,
                        // one whose track the current player currently is not on
                        // add the Alternative Track Cost to the track's weight.
                        if(addAltTrackCost && !newNode.AltTracksPaid[trackOwner])
                        {
                            newCost += 1000;
                            newNode.AltTracksPaid[trackOwner] = true;
                        }

                        // If a shorter path has already been found, continue
                        if(distMap.TryGetValue(newPoint, out int currentCost) && currentCost <= newCost)
                            continue;

                        distMap[newPoint] = newCost;
                        previous[newPoint] = node.Position;

                        newNode.Weight = newCost + Mathf.RoundToInt(NodeId.Distance(newNode.Position, end));
                        queue.Insert(newNode);
                    }
                } 
            } 
            return path;
        }

        /// <summary>
        /// Creates a new PathData using the given
        /// tracks, nodes from start to end and player index
        /// </summary>
        private static PathData CreatePathData(
            Dictionary<NodeId, int[]> tracks,
            int player, int speed,
            params List<NodeId>[] paths
        ) {
            int spacesLeft = speed + 1;
            int distance = paths.Sum(n => n.Count);
            int cost = 0;
            var nodes = new List<NodeId>();

            bool [] tracksPaid = new bool[6] { false, false, false, false, false, false };
            tracksPaid[player] = true; 

            for(int p = 0; p < paths.Length; ++p)
            {
                for(int i = 0; i < paths[p].Count; ++i)
                {
                    nodes.Add(paths[p][i]); 

                    NodeId node1; NodeId ? node2 = null;
                    node1 = paths[p][i];

                    if(i < paths[p].Count - 1)
                        node2 = paths[p][i+1];
                    else if(p < paths.Length - 1)
                        node2 = paths[p+1][0];

                    if(node2.HasValue)
                    {
                        Cardinal c = Utilities.CardinalBetween(node1, node2.Value);
                        if(!tracksPaid[tracks[node1][(int)c]])
                        {
                            cost += Manager.AltTrackCost;
                            tracksPaid[tracks[node1][(int)c]] = true;
                        }
                    }

                    spacesLeft -= 1;

                    if(spacesLeft == 0)
                    {
                        spacesLeft = speed;
                        for(int t = 0; t < 6; ++t)
                            tracksPaid[t] = false;

                        tracksPaid[player] = true;
                    }
                }
            }

            return new PathData(distance, cost, nodes);
        }

        /// <summary>
        /// Creates a new PathData using the given tracks,
        /// a map of all tracks' nodes previous nodes for shortest
        /// path, player index and start / end points.
        /// </summary>
        private static PathData CreatePathData (
            Dictionary<NodeId, int[]> tracks,
            Dictionary<NodeId, NodeId> previous,
            int player, int speed, NodeId start, NodeId end
         ) {
            int spacesLeft = speed + 1;
            var cost = 0;
            var distance = 0;
            bool [] tracksPaid = new bool[6] { false, false, false, false, false, false };
            tracksPaid[player] = true;

            var nodes = new List<NodeId>();
            var current = end;

            do
            {
                nodes.Add(current);
                distance += 1;

                Cardinal c = Utilities.CardinalBetween(current, previous[current]);
                if(!tracksPaid[tracks[current][(int)c]])
                {
                    cost += Manager.AltTrackCost;
                    tracksPaid[tracks[current][(int)c]] = true;
                }

                current = previous[current];

                spacesLeft -= 1;

                if(spacesLeft == 0)
                {
                    spacesLeft = speed;
                    for(int t = 0; t < 6; ++t)
                        tracksPaid[t] = false;

                    tracksPaid[player] = true;
                }
            } 
            while(current != start);

            nodes.Reverse();
            return new PathData(distance, cost, nodes);
        }
 
        #endregion
    }    
}