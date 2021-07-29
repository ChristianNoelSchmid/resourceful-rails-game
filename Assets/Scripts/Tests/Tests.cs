using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rails
{
    public class Tests : TesterBase
    {
        private NodeId nodeMid = new NodeId(1, 1);
        private NodeId nodeN = new NodeId(1, 2);
        private NodeId nodeNE = new NodeId(2, 2);
        private NodeId nodeSE = new NodeId(2, 1);
        private NodeId nodeS = new NodeId(1, 0);
        private NodeId nodeSW = new NodeId(0, 1);
        private NodeId nodeNW = new NodeId(0, 2);

        private void Awake()
        {
            TestMethod(TestPriorityQueueOrder);
            TestMethod(TestPriorityQueuePeek); 

            TestMethod(TestReflectCardinal);
            TestMethod(TestCardinalBetween);
            TestMethod(TestPointTowards);
            TestThrowsException(TestCardinalBetweenException, typeof(ArgumentException));
        }

        #region PriorityQueueTests 
        private void TestPriorityQueueOrder()
        {
            var queue = new PriorityQueue<int>();
            queue.Insert(10);
            queue.Insert(4);
            queue.Insert(6);
            queue.Insert(1);
            queue.Insert(6);

            Assert(queue.Pop() == 1);
            Assert(queue.Pop() == 4);
            Assert(queue.Pop() == 6);
            Assert(queue.Pop() == 6);
            Assert(queue.Pop() == 10);
        }
        private void TestPriorityQueuePeek()
        {
            var queue = new PriorityQueue<string>();
            queue.Insert("x");
            queue.Insert("d");
            queue.Insert("p");
            queue.Insert("z");

            Assert(queue.Peek() == "d");
            Assert(queue.Pop() == "d");
            Assert(queue.Pop() == "p");
            Assert(queue.Pop() == "x");
            Assert(queue.Pop() == "z");

            Assert(queue.Peek() == null);
            Assert(queue.Pop() == null);
        }
        #endregion

        #region UtilitiesTests
        private void TestReflectCardinal()
        {
            Assert(Utilities.ReflectCardinal(Cardinal.N) == Cardinal.S);
            Assert(Utilities.ReflectCardinal(Cardinal.NE) == Cardinal.SW);
            Assert(Utilities.ReflectCardinal(Cardinal.NW) == Cardinal.SE);
            Assert(Utilities.ReflectCardinal(Cardinal.S) == Cardinal.N);
            Assert(Utilities.ReflectCardinal(Cardinal.SW) == Cardinal.NE);
            Assert(Utilities.ReflectCardinal(Cardinal.SE) == Cardinal.NW);
        }

        private void TestCardinalBetween()
        {
            Assert(Utilities.CardinalBetween(nodeMid, nodeN) == Cardinal.N);
            Assert(Utilities.CardinalBetween(nodeMid, nodeNE) == Cardinal.NE);
            Assert(Utilities.CardinalBetween(nodeMid, nodeSE) == Cardinal.SE);
            Assert(Utilities.CardinalBetween(nodeMid, nodeS) == Cardinal.S);
            Assert(Utilities.CardinalBetween(nodeMid, nodeSW) == Cardinal.SW);
            Assert(Utilities.CardinalBetween(nodeMid, nodeNW) == Cardinal.NW);
        }

        private void TestCardinalBetweenException() => Utilities.CardinalBetween(nodeMid, new NodeId(20, 20));

        private void TestPointTowards()
        {
            Assert(Utilities.PointTowards(nodeMid, Cardinal.N) == nodeN);
            Assert(Utilities.PointTowards(nodeMid, Cardinal.NE) == nodeNE);
            Assert(Utilities.PointTowards(nodeMid, Cardinal.SE) == nodeSE);
            Assert(Utilities.PointTowards(nodeMid, Cardinal.S) == nodeS);
            Assert(Utilities.PointTowards(nodeMid, Cardinal.SW) == nodeSW);
            Assert(Utilities.PointTowards(nodeMid, Cardinal.NW) == nodeNW);
        }
        #endregion

    }
}
