using System;

using UnityEngine;

namespace Rails
{
    public static class Utilities
    {
        /// <summary>
        /// Returns the point represented by a combined point and cardinal.
        /// The point returned is the one moving by Cardinal direction, one space over
        /// from start.
        /// </summary>
        /// <param name="start">The start point</param>
        /// <param name="towards">The cardinal direction moving towards</param>
        /// <returns>The new point being moved towards.</returns>
        public static NodeId PointTowards(NodeId start, Cardinal towards)
        {
            bool isOdd = start.X % 2 == 1;

            var dir = towards switch
            {
                Cardinal.N => Vector2Int.up,
                Cardinal.NE => isOdd ? new Vector2Int(1, 1) : Vector2Int.right,
                Cardinal.NW => isOdd ? new Vector2Int(-1, 1) : Vector2Int.left, 
                Cardinal.S => Vector2Int.down,
                Cardinal.SW => isOdd ? Vector2Int.left : new Vector2Int(-1, -1),
                _ => isOdd ? Vector2Int.right : new Vector2Int(1, -1),
            };

            return new NodeId(start.X + dir.x, start.Y + dir.y);
        }

        /// <summary>
        /// Reflects the given Cardinal, returning the
        /// exact opposite direction. (ie. N -> S, NE -> SW).
        /// </summary>
        /// <param name="towards">The Cardinal being reflected</param>
        /// <returns>The opposite cardinal direction to towards</returns> 
        public static Cardinal ReflectCardinal(Cardinal towards)
        {
            towards -= 3;
            if(towards < 0) 
                towards += (int)Cardinal.MAX_CARDINAL;

            return towards;
        }

        /// <summary>
        /// Finds the Cardinal between two adjacent NodeIds.
        /// </summary>
        /// <param name="node1">The starting NodeId</param>
        /// <param name="node2">The target NodeId</param>
        /// <returns>The Cardinal represented from n1 to n2.</returns>
        public static Cardinal CardinalBetween(NodeId node1, NodeId node2)
        {
            var exception = new ArgumentException($"Cannot find Cardinal between two non-adjacent NodeIds: {node1} and {node2}");

            // Find the direction the NodeIds are moving
            var dir = node2 - node1;
            bool isOdd = node1.X % 2 == 1;

            return (dir.X, dir.Y) switch
            {
                ( 0,  1) => Cardinal.N,
                ( 1,  1) => isOdd ? Cardinal.NE     : throw exception,
                ( 1,  0) => isOdd ? Cardinal.SE     : Cardinal.NE,
                ( 1, -1) => isOdd ? throw exception : Cardinal.SE,
                ( 0, -1) => Cardinal.S,
                (-1, -1) => isOdd ? throw exception : Cardinal.SW,
                (-1,  0) => isOdd ? Cardinal.SW     : Cardinal.NW,
                (-1,  1) => isOdd ? Cardinal.NW     : throw exception,
                _ => throw exception,
            };
        }
    }
}