using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace jps
{
    public enum EDiagonalMovement
    {
        Never = 0,
        OnlyWhenNoObstacles,
        IfAtMostOneObstacle,
        Always,
    }

    public abstract class JPS_Base : IFinder
    {
        protected class Node : IPriorityQueueNode
        {
            public int x = 0;
            public int y = 0;
            public float g = 0f;
            public float h = -1f;
            public float f = 0f;
            public bool is_opened = false;
            public bool is_closed = false;
            public Node parent = null;

            public float Priority { get { return f; } set { f = value; } }
            public long InsertionIndex { get; set; }
            public int QueueIndex { get; set; }
        }

        protected Node[,] m_Nodes = null;
        protected HeapPriorityQueue<Node> m_OpenList = null;
        protected Node m_StartNode = null;
        protected Node m_EndNode = null;
        protected Func<int, int, bool> m_IsWalkableAtFunc = null;
        protected Func<float, float, float> m_Heuristic = null;

        protected List<Node> m_NodeInUse = new List<Node>();
        protected Queue<Node> m_NodePool = new Queue<Node>();

        public void Init(int width, int height, Func<int, int, bool> func)
        {
            m_Nodes = new Node[width, height];
            m_IsWalkableAtFunc = func;
            m_Heuristic = Heuristic.Manhattan;
        }

        protected Node GetNodeAt(Vector2 pos, bool create_when_not_exist = true)
        {
            return GetNodeAt((int)pos.X, (int)pos.Y, create_when_not_exist);
        }

        protected Node GetNodeAt(int x, int y, bool create_when_not_exist = true)
        {
            var node = m_Nodes[x, y];
            if (node == null && create_when_not_exist) {
                if (m_NodePool.Count > 0) {
                    node = m_NodePool.Dequeue();
                }
                else {
                    node = new Node();
                }

                node.x = x;
                node.y = y;
                m_Nodes[x, y] = node;

                m_NodeInUse.Add(node);
            }

            return node;
        }

        protected bool IsWalkableAt(int x, int y)
        {
            return m_IsWalkableAtFunc(x, y);
        }

        /// <summary>
        /// Get the neighbors of the given node.
        ///     offsets      diagonalOffsets:
        ///  +---+---+---+    +---+---+---+
        ///  |   | 0 |   |    | 0 |   | 1 |
        ///  +---+---+---+    +---+---+---+
        ///  | 3 |   | 1 |    |   |   |   |
        ///  +---+---+---+    +---+---+---+
        ///  |   | 2 |   |    | 3 |   | 2 |
        ///  +---+---+---+    +---+---+---+
        /// </summary>
        /// <param name="node"></param>
        /// <param name="dia_movement"></param>
        /// <returns></returns>
        protected IEnumerable<Vector2> GetNeighbors(Node node, EDiagonalMovement dia_movement)
        {
            var x = node.x;
            var y = node.y;

            bool s0, s1, s2, s3;
            s0 = s1 = s2 = s3 = false;

            // ↑
            if (IsWalkableAt(x, y - 1)) {
                yield return new Vector2(x, y - 1);
                s0 = true;
            }
            // →
            if (IsWalkableAt(x + 1, y)) {
                yield return new Vector2(x + 1, y);
                s1 = true;
            }
            // ↓
            if (IsWalkableAt(x, y + 1)) {
                yield return new Vector2(x, y + 1);
                s2 = true;
            }
            // ←
            if (IsWalkableAt(x - 1, y)) {
                yield return new Vector2(x - 1, y);
                s3 = true;
            }

            bool d0, d1, d2, d3;
            d0 = d1 = d2 = d3 = false;

            if (dia_movement == EDiagonalMovement.Never) {
                yield break;
            }
            else if (dia_movement == EDiagonalMovement.OnlyWhenNoObstacles) {
                d0 = s3 && s0;
                d1 = s0 && s1;
                d2 = s1 && s2;
                d3 = s2 && s3;
            }
            else if (dia_movement == EDiagonalMovement.IfAtMostOneObstacle) {
                d0 = s3 || s0;
                d1 = s0 || s1;
                d2 = s1 || s2;
                d3 = s2 || s3;
            }
            else if (dia_movement == EDiagonalMovement.Always) {
                d0 = d1 = d2 = d3 = true;
            }

            // ↖
            if (d0 && IsWalkableAt(x - 1, y - 1)) {
                yield return new Vector2(x - 1, y - 1);
            }
            // ↗
            if (d1 && IsWalkableAt(x + 1, y - 1)) {
                yield return new Vector2(x + 1, y - 1);
            }
            // ↘
            if (d2 && IsWalkableAt(x + 1, y + 1)) {
                yield return new Vector2(x + 1, y + 1);
            }
            // ↙
            if (d3 && IsWalkableAt(x - 1, y + 1)) {
                yield return new Vector2(x - 1, y + 1);
            }
        }

        protected IEnumerable<Node> Backtrace(Node node)
        {
            do {
                yield return node;

                node = node.parent;
            } while (node != null);
        }

        public List<Vector2> FindPath(Vector2 start, Vector2 end)
        {
            try {
                m_OpenList = new HeapPriorityQueue<Node>(32);
                m_StartNode = GetNodeAt(start);
                m_EndNode = GetNodeAt(end);

                m_StartNode.g = 0f;
                m_StartNode.f = 0f;

                m_OpenList.Enqueue(m_StartNode);
                m_StartNode.is_opened = true;

                while (m_OpenList.Count > 0) {
                    var node = m_OpenList.Dequeue();
                    node.is_closed = true;

                    if (node == m_EndNode) {
                        return Backtrace(node).Reverse().Select(n => new Vector2(n.x, n.y)).ToList();
                    }

                    IdentifySuccessors(node);
                }

                return null;
            }
            finally {
                foreach (var node in m_NodeInUse) {
                    node.parent = null;
                    node.f = node.g = 0f;
                    node.is_closed = node.is_opened = false;
                    node.h = -1f;

                    m_NodePool.Enqueue(node);
                }
                m_NodeInUse.Clear();

                Array.Clear(m_Nodes, 0, m_Nodes.Length);
            }
        }

        protected void IdentifySuccessors(Node node)
        {
            var x = node.x;
            var y = node.y;

            foreach (var neighbor in FindNeighbors(node)) {
                var jump_point = Jump((int)neighbor.X, (int)neighbor.Y, x, y);
                if (jump_point.HasValue) {
                    var jx = (int)jump_point.Value.X;
                    var jy = (int)jump_point.Value.Y;
                    var jump_node = GetNodeAt(jx, jy);
                    if (jump_node.is_closed) {
                        continue;
                    }

                    var d = Heuristic.Octile(Math.Abs(jx - x), Math.Abs(jy - y));
                    var ng = node.g + d;

                    if (!jump_node.is_opened || ng < jump_node.g) {
                        jump_node.g = ng;
                        if (jump_node.h < 0f) {
                            jump_node.h = m_Heuristic(Math.Abs(jx - m_EndNode.x), Math.Abs(jy - m_EndNode.y));
                        }
                        jump_node.f = jump_node.g + jump_node.h;
                        jump_node.parent = node;

                        if (!jump_node.is_opened) {
                            m_OpenList.Enqueue(jump_node);
                            jump_node.is_opened = true;
                        }
                        else {
                            m_OpenList.UpdatePriority(jump_node, jump_node.Priority);
                        }

                        if (jump_node == m_EndNode) {
                            return;
                        }
                    }
                }
            }
        }

        protected abstract Vector2? Jump(int x, int y, int px, int py);

        protected abstract IEnumerable<Vector2> FindNeighbors(Node node);
    }
}
