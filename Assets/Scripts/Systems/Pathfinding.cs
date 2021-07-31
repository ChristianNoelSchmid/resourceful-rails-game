using System;
using System.Collections.Generic;
using System.Linq;
using Rails.Collections;
using Rails.Data;
using Rails.ScriptableObjects;
using UnityEngine;

namespace Rails.Systems
{
    // These pathfinding methods rely on Edsger W. Dijkstra's algorithm
    // found on Wikipedia (https://en.wikipedia.org/wiki/Dijkstra's_algorithm)
    // combined with Peter Hart, Nils Nilsson and Bertram Raphael's A*
    // heuristic algorithm found on Wikipedia 
    // (https://en.wikipedia.org/wiki/A*_search_algorithm)

 
    /// <summary>
    /// A comparable object representing the position,
    /// and weight to reach a node given a known start
    /// position. Used in pathfinding
    /// </summary>
    class WeightedNode : IComparable<WeightedNode>
    {
        public NodeId Position { get; set; }
        public int Weight { get; set; }
        
        // A running tally of what tracks have been paid for at
        // the current position
        public bool[] AltTracksPaid { get; set; }
        
        // How many spaces the player has left at this WeightedNode, from
        // start.
        public int SpacesLeft { get; set; }
        
        // Compared by Weight
        public int CompareTo(WeightedNode other) => Weight.CompareTo(other.Weight);
    } 
    
    public static class Pathfinding
    { 
        #region Public Methods

        /// <summary>
        /// Finds the least-distance build route given a series of
        /// intermediate segment nodes.
        /// </summary>
        /// <param name="tracks">The map's current player tracks</param>
        /// <param name="mapData">The map's node information</param>
        /// <param name="segments">The NodeIds the route must pass through</param>'
        /// <returns>
        /// The shortest-distance Route that connects all segments.
        /// Returns the furthest path between segments possible
        /// if a segment cannot be reached. 
        /// </returns>
        public static Route ShortestBuild(
            TrackGraph<int> tracks, MapData mapData,
            params NodeId[] segments
        ) {
            var path = new List<NodeId>();

            // If no segments were given, return an empty Route
            if (segments.Length == 0) 
                return new Route(0, path);


            // A duplicate of the current tracks - ensures that Pathfinder
            // doesn't reuse already commited paths in later traversals.
            var newTracks = tracks.Clone(entry => entry.Value.ToArray());
                
            // For each segment, find the least-distance path between it and the next segment.
            for(int i = 0; i < segments.Length - 1; ++i)
            {
                var route = LeastCostPath(
                    newTracks, mapData, segments[i], 
                    segments[i + 1], false
                );
                if(route == null) break;
                
                // Add the new path to newTracks
                for(int j = 0; j < route.Nodes.Count - 1; ++j)
                    newTracks[route.Nodes[j], route.Nodes[j + 1]] = 6;
         
                path.AddRange(route.Nodes.Take(route.Nodes.Count - 1));
                if (i == segments.Length - 2) path.Add(segments.Last());
            }
            
            return CreatePathRoute(mapData, path);
        }

        /// <summary>
        /// Finds the least-cost build route given a series of
        /// intermediate segment nodes.
        /// </summary>
        /// <param name="tracks">The map's current player tracks</param>
        /// <param name="mapData">The map's node information</param>
        /// <param name="segments">The NodeIds the route must pass through</param>
        /// <returns>
        /// The shortest-cost Route that connects all segments.
        /// Returns the furthest path between segments possible
        /// if a segment cannot be reached. 
        /// </returns>
        public static Route CheapestBuild(
            TrackGraph<int> tracks, MapData mapData,
            params NodeId[] segments
        ) {
            var path = new List<NodeId>();
            
            // If no segments were given, return an empty Route
            if (segments.Length == 0) 
                return new Route(0, path);


            // A duplicate of the current tracks - ensures that Pathfinder
            // doesn't reuse already commited paths in later traversals.
            var newTracks = tracks.Clone(entry => entry.Value.ToArray());

            // For each segment, find the least-cost path between it and the next segment.
            for(int i = 0; i < segments.Length - 1; ++i)
            {
                var route = LeastCostPath(
                    newTracks, mapData, segments[i], 
                    segments[i + 1], true
                );
                if(route == null) break;
                
                // Add the new path to newTracks
                for(int j = 0; j < route.Nodes.Count - 1; ++j)
                    newTracks[route.Nodes[j], route.Nodes[j+1]] = 6;

                path.AddRange(route.Nodes.Take(route.Nodes.Count - 1));
                if (i == segments.Length - 2) path.Add(segments.Last());
            }

            return CreatePathRoute(mapData, path);
        }

        /// <summary>
        /// Finds the least-distance traversal on a track,
        /// given a `player`, the player's train's `speed`, and
        /// a series of intermediate segments.
        /// </summary>
        /// <param name="tracks">The map's current player tracks</param>
        /// <param name="player">The index of the player performing the traversal</param>
        /// <param name="speed">The speed of the player's train</param>
        /// <param name="segments">The NodeIds the route must pass through</param>
        /// <returns>
        /// The shortest-distance Route that connects all segments.
        /// Returns the furthest path between segments possible
        /// if a segment cannot be reached. 
        /// </returns>
        public static Route ShortestMove(
            TrackGraph<int> tracks,
            int player, int speed, params NodeId [] segments
        ) {
            var path = new List<NodeId>();

            // If no segments were given, return an empty Route
            if (segments.Length == 0) 
                return new Route(0, path);

            path.Add(segments[0]);
    
            // For each segment, find the least-distance path between it and the next segment.
            for(int i = 0; i < segments.Length - 1; ++i)
            {
                var route = LeastCostTrack(
                    tracks, player, speed, 
                    segments[i], segments[i + 1], false
                );
                if(route == null) break;

                // Add the new path to newTracks
                path.AddRange(route.Nodes);
            }

            return CreateTrackRoute(tracks, player, speed, path); 
        }

        /// <summary>
        /// Finds the least-cost traversal on a track,
        /// given a `player`, the player's train's `speed`, and
        /// a series of intermediate segments.
        /// </summary>
        /// <param name="tracks">The map's current player tracks</param>
        /// <param name="player">The index of the player performing the traversal</param>
        /// <param name="speed">The speed of the player's train</param>
        /// <param name="segments">The NodeIds the route must pass through</param>
        /// <returns>
        /// The shortest-distance Route that connects all segments.
        /// Returns the furthest path between segments possible
        /// if a segment cannot be reached. 
        /// </returns>
        public static Route CheapestMove(
            TrackGraph<int> tracks,
            int player, int speed, params NodeId [] segments
        ) {
            var path = new List<NodeId>();
            
            // If no segments were given, return an empty Route
            if (segments.Length == 0) 
                return new Route(0, path);

            path.Add(segments[0]);

            // For each segment, find the least-cost path between it and the next segment.
            for(int i = 0; i < segments.Length - 1; ++i)
            {
                var route = LeastCostTrack(
                    tracks, player, speed, 
                    segments[i], segments[i + 1], true
                );
                if(route == null) break;
                
                // Add the new path to newTracks
                path.AddRange(route.Nodes);
            }

            return CreateTrackRoute(tracks, player, speed, path); 
        }

        #endregion

        #region Private Methods         

        /// <summary>
        /// Finds the lowest cost track available for the given player,
        /// from a start point to end point.
        /// </summary>
        private static Route LeastCostTrack(
            TrackGraph<int> tracks, 
            int player, int speed, 
            NodeId start, NodeId end,
            bool addAltTrackCost
        ) {
            if(start == end || !tracks.ContainsVertex(start) || !tracks.ContainsVertex(end))
                return null;

            // The list of nodes to return from method
            Route path = null;
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
                    path = CreateTrackRoute(tracks, previous, player, speed, start, end);
                    break;
                }
                
                // If other players' track costs are being considered,
                // and the player has run out of spaces on this turn, reset
                // all AltTracksPaid elements to false (except the traversing player). 
                if(addAltTrackCost && node.SpacesLeft == 0)
                {
                    node.SpacesLeft = speed;
                    for(int i = 0; i < 6; ++i)
                        node.AltTracksPaid[i] = false;
                    node.AltTracksPaid[player] = true;
                }
                
                // With the current NodeId, cycle through all Cardinal directions,
                // determining cost to traverse that direction.
                for(Cardinal c = Cardinal.N; c < Cardinal.MAX_CARDINAL; ++c)
                {
                    var newPoint = Utilities.PointTowards(node.Position, c);
                    if(tracks[node.Position, c] != -1 && tracks.ContainsVertex(newPoint))
                    {
                        var newNode = new WeightedNode
                        {
                            Position = newPoint, 
                            SpacesLeft = node.SpacesLeft - 1,
                        };
                        newNode.AltTracksPaid = node.AltTracksPaid.ToArray();

                        var newCost = distMap[node.Position] + 1;

                        // Retrieve the track owner of the edge being considered
                        int trackOwner = tracks[node.Position, newPoint];

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
                        
                        // Add the true new cost to distMap
                        distMap[newPoint] = newCost;
                        // Establish the previous node for the new node, to connect
                        // the path upon completion
                        previous[newPoint] = node.Position;
                        
                        // Add the new cost, with the A* heuristic, to the node's weight
                        newNode.Weight = newCost + Mathf.RoundToInt(NodeId.Distance(newNode.Position, end));
                        queue.Insert(newNode);
                    }
                } 
            } 
            return path;
        }
           
        /// <summary>
        /// Finds the lowest cost path available from a start point to end point.
        /// </summary>
        private static Route LeastCostPath(
            TrackGraph<int> tracks, 
            MapData map, NodeId start, NodeId end,            
            bool addWeight
        ) {
            if(start == end)
                return null;
            if(tracks.TryGetValue(start, out var startCardinals) && startCardinals.All(p => p != -1))
                return null;
            if(tracks.TryGetValue(end, out var endCardinals) && endCardinals.All(p => p != -1))
                return null;

            // The list of nodes to return from method
            Route path = null;
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

            distMap[start] = 0;
            var startNode = new WeightedNode { Position = start, Weight = 0 };

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
                    path = CreatePathRoute(map, previous, start, end);
                    break;
                }

                // With the current NodeId, cycle through all Cardinal directions,
                // determining cost to traverse that direction.
                for (Cardinal c = Cardinal.N; c < Cardinal.MAX_CARDINAL; ++c)
                {
                    var newPoint = Utilities.PointTowards(node.Position, c);

                    // If there is a track already at the considered edge, continue
                    if (tracks.TryGetValue(node.Position, out var cardinals) && cardinals[(int)c] != -1)
                        continue;

                    // If the point is outside the map bounds, continue
                    if (newPoint.X < 0 || newPoint.Y < 0 || newPoint.X >= Manager.Size || newPoint.Y >= Manager.Size)
                        continue;

                    var newCost = distMap[node.Position] + 1;

                    if (addWeight)
                    {
                        // Add the node cost to newCost, as well as river cost if the edge is over a river
                        newCost += Manager.NodeCosts[map.Nodes[newPoint.GetSingleId()].Type];

                        if (map.Segments[(newPoint.GetSingleId() * 6) + (int)c].Type == NodeSegmentType.River)
                            newCost += Manager.RiverCost;
                    }

                    // If a shorter path has already been found, continue
                    if (distMap.TryGetValue(newPoint, out int currentCost) && currentCost <= newCost)
                        continue;

                    // Add the true new cost to distMap
                    distMap[newPoint] = newCost;
                    // Establish the previous node for the new node, to connect
                    // the path upon completion
                    previous[newPoint] = node.Position;

                    // Add the new cost, with the A* heuristic, to the node's weight
                    var newNode = new WeightedNode { Position = newPoint, Weight = newCost };
                    queue.Insert(newNode);
                }
            } 
            return path;
        }
        
        /// <summary>
        /// Creates a new Route using the given tracks,
        /// a map of all tracks' nodes previous nodes for shortest
        /// path.
        /// </summary>
        private static Route CreateTrackRoute (
            TrackGraph<int> tracks,
            Dictionary<NodeId, NodeId> previous,
            int player, int speed, NodeId start, NodeId end
         ) {
            int spacesLeft = speed + 1;
            var cost = 0;
            bool [] tracksPaid = new bool[6] { false, false, false, false, false, false };
            tracksPaid[player] = true;

            var nodes = new List<NodeId>();
            var current = end;

            // While the start node has not been reached, traverse
            // down the previous map.
            do
            {
                nodes.Add(current);

                // If the track has yet to be paid for this turn
                // add the cost.
                if(!tracksPaid[tracks[current, previous[current]]])
                {
                    cost += Manager.AltTrackCost;
                    tracksPaid[tracks[current, previous[current]]] = true;
                }

                current = previous[current];

                spacesLeft -= 1;

                // If the player has run out of spaces this turn,
                // reset tracksPaid
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
            return new Route(cost, nodes);
        }

        /// <summary>
        /// Creates a new Route using the given
        /// tracks, nodes from start to end and player index
        /// </summary>
        private static Route CreateTrackRoute(
            TrackGraph<int> tracks,
            int player, int speed,
            List<NodeId> path
        ) {
            int spacesLeft = speed + 1;
            int cost = 0;

            bool [] tracksPaid = new bool[6] { false, false, false, false, false, false };
            tracksPaid[player] = true; 
            
            // Traverse the path, adding the cost of each edge while doing so
            for(int i = 0; i < path.Count - 1; ++i)
            {
                // If the track has yet to be paid for this turn
                // add the cost.
                if(!tracksPaid[tracks[path[i], path[i+1]]])
                {
                    cost += Manager.AltTrackCost;
                    tracksPaid[tracks[path[i], path[i+1]]] = true;
                }
            
                spacesLeft -= 1;
                
                // If the player has run out of spaces this turn,
                // reset tracksPaid
                if(spacesLeft == 0)
                {
                    spacesLeft = speed;

                    for(int t = 0; t < 6; ++t)
                        tracksPaid[t] = false;

                    tracksPaid[player] = true;
                }
            }

            return new Route(cost, path);
        }

        /// <summary>
        /// Creates a new Route using the MapData and a map of all tracks' 
        /// nodes previous nodes for shortest path.
        /// </summary>
        private static Route CreatePathRoute(
            MapData map,
            Dictionary<NodeId, NodeId> previous,
            NodeId start, NodeId end
        ) {
            var cost = 0;
            var nodes = new List<NodeId>();
            var current = end;

            // While the start node has not been reached, traverse
            // down the previous map.
            while(current != start)
            {
                nodes.Add(current);

                // Add the cost of building the track, based on NodeType
                // and NodeSegment 
                cost += Manager.NodeCosts[map.Nodes[current.GetSingleId()].Type];
                if (map.Segments[current.GetSingleId() * 6 + (int)Utilities.CardinalBetween(current, previous[current])].Type == NodeSegmentType.River)
                    cost += Manager.RiverCost;

                current = previous[current];
            }
            nodes.Add(start);

            nodes.Reverse();
            return new Route(cost, nodes);
        }
        
        /// <summary>
        /// Creates a new Route using the MapData and a list of NodeIds
        /// </summary>
        private static Route CreatePathRoute(
            MapData map,
            List<NodeId> path
        ) {
            int cost = 0;

            // Traverse the path, adding the cost of each edge while doing so
            for (int i = 0; i < path.Count - 1; ++i)
            {
                // Add the node cost to newCost, as well as river cost if the edge is over a river
                cost += Manager.NodeCosts[map.Nodes[path[i+1].GetSingleId()].Type];

                if (map.Segments[path[i].GetSingleId() * 6 + (int)Utilities.CardinalBetween(path[i], path[i + 1])].Type == NodeSegmentType.River)
                    cost += Manager.RiverCost;
            }
 
            return new Route(cost, path);
        }
        
        #endregion
    }    
}