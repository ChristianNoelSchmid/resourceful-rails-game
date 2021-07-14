using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rails
{
    [Serializable]
    public struct NodeId
    {
        public int X;
        public int Y;

        public NodeId(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int GetSingleId()
        {
            return (X * Manager.Size) + Y;
        }

        public override bool Equals(object obj)
        {
            if(obj is NodeId node)
                return node.X == X && node.Y == Y;
            return false;
        }

        public override int GetHashCode()
        {
            unchecked {
                int hash = 17;
                hash = hash * 23 + X.GetHashCode();
                hash = hash * 23 + Y.GetHashCode();
                return hash;
            }
        }

        public override string ToString() => $"{{{X}, {Y}}}";

        public static bool operator ==(NodeId n1, NodeId n2) => n1.Equals(n2);
        public static bool operator !=(NodeId n1, NodeId n2) => !n1.Equals(n2); 

        public static NodeId operator +(NodeId n1, NodeId n2) => new NodeId(n1.X + n2.X, n1.Y + n2.Y);
        public static NodeId operator -(NodeId n1, NodeId n2) => new NodeId(n1.X - n2.X, n1.Y - n2.Y);
        public static NodeId operator *(NodeId n1, int x) => new NodeId(n1.X * x, n1.Y * x);

        public static float Distance(NodeId n1, NodeId n2) =>
            Mathf.Sqrt((float)Mathf.Pow(n1.X - n2.X, 2) + Mathf.Pow(n1.Y - n2.Y, 2));
    }
}
