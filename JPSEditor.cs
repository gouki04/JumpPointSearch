using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace jps
{
    public class JPSEditor
    {
        public enum EGrid
        {
            None = 0,

            /// <summary>
            /// 起点
            /// </summary>
            Start = 1,

            /// <summary>
            /// 终点
            /// </summary>
            Goal = 2,

            /// <summary>
            /// 障碍物
            /// </summary>
            Obstacle = -1,
        }

        protected int m_BoardWidth = 30;
        protected int m_BoardHeight = 30;

        protected EGrid[,] m_Board = null;

        protected Random m_Rand = null;
        protected IFinder m_Alg = null;

        /// <summary>
        /// 寻路的结果，可以为null
        /// </summary>
        protected List<Vector2> m_Path = null;

        protected Vector2 m_StartPos;
        protected Vector2 m_GoalPos;

        protected Dictionary<EGrid, uint> m_GridColor = new Dictionary<EGrid, uint>()
        {
            { EGrid.None, 0xff000000 },
            { EGrid.Start, 0xff0000ff },
            { EGrid.Goal, 0xff00ffff },
            { EGrid.Obstacle, 0xffffffff },
        };

        protected Dictionary<EGrid, string> m_GridText = new Dictionary<EGrid, string>()
        {
            { EGrid.None, string.Empty },
            { EGrid.Start, "S" },
            { EGrid.Goal, "G" },
            { EGrid.Obstacle, string.Empty },
        };

        protected EGrid this[int x, int y]
        {
            get {
                return m_Board[x, y];
            }
            set {
                m_Board[x, y] = value;
            }
        }

        protected EGrid this[Vector2 pos]
        {
            get {
                return this[(int)pos.X, (int)pos.Y];
            }
            set {
                this[(int)pos.X, (int)pos.Y] = value;
            }
        }

        protected bool IsWalkableAt(int x, int y)
        {
            if (x < 0 || x >= m_BoardWidth || y < 0 || y >= m_BoardHeight) {
                return false;
            }

            return m_Board[x, y] != EGrid.Obstacle;
        }

        public JPSEditor()
        {
            m_Rand = new Random(DateTime.Now.Millisecond);
            m_Board = new EGrid[m_BoardWidth, m_BoardHeight];

            m_Alg = JumpPointSearch.CreateFinder(m_BoardWidth, m_BoardHeight, IsWalkableAt, EDiagonalMovement.Never);

            m_StartPos = new Vector2()
            {
                X = m_Rand.Next(0, m_BoardWidth),
                Y = m_Rand.Next(0, m_BoardHeight),
            };

            this[m_StartPos] = EGrid.Start;

            m_GoalPos = new Vector2()
            {
                X = m_Rand.Next(0, m_BoardWidth),
                Y = m_Rand.Next(0, m_BoardHeight),
            };

            while (m_GoalPos == m_StartPos) {
                m_GoalPos = new Vector2()
                {
                    X = m_Rand.Next(0, m_BoardWidth),
                    Y = m_Rand.Next(0, m_BoardHeight),
                };
            }
            this[m_GoalPos] = EGrid.Goal;
        }

        public void Init()
        {
        }

        protected int GetPathIndex(int x, int y)
        {
            if (m_Path == null) {
                return -1;
            }

            return m_Path.IndexOf(new Vector2(x, y));
        }

        public bool DrawGui()
        {
            ImGui.Begin("Board");

            if (ImGui.Button("Search Path")) {
                m_Path = m_Alg.FindPath(m_StartPos, m_GoalPos);
            }
            if (m_Path != null) {
                ImGui.SameLine();
                if (ImGui.Button("Clear Path")) {
                    m_Path = null;
                }
            }

            var grid_size = new Vector2(20f, 20f);
            ImGui.PushStyleVar(ImGuiStyleVar.SelectableTextAlign, new Vector2(0.5f, 0.5f));
            ImGui.PushStyleColor(ImGuiCol.Text, 0xff000000);

            for (var y = 0; y < m_BoardHeight; ++y) {
                for (var x = 0; x < m_BoardWidth; ++x) {
                    ImGui.PushID(x + y * m_BoardWidth);

                    var path_idx = GetPathIndex(x, y);
                    if (path_idx == -1) {
                        var v = this[x, y];

                        ImGui.PushStyleColor(ImGuiCol.Header, m_GridColor[v]);
                        var clicked = ImGui.Selectable(m_GridText[v], true, ImGuiSelectableFlags.None, grid_size);
                        if (clicked) {
                            if (v == EGrid.Obstacle) {
                                this[x, y] = EGrid.None;
                            }
                            else if (v == EGrid.None) {
                                this[x, y] = EGrid.Obstacle;
                            }
                        }

                        ImGui.PopStyleColor();
                    }
                    else {
                        ImGui.PushStyleColor(ImGuiCol.Header, 0xffff0000);
                        ImGui.Selectable(path_idx.ToString(), true, ImGuiSelectableFlags.None, grid_size);
                        ImGui.PopStyleColor();
                    }

                    if (x < m_BoardWidth - 1) {
                        ImGui.SameLine();
                    }
                    ImGui.PopID();
                }
            }
            ImGui.PopStyleColor();
            ImGui.PopStyleVar();
            ImGui.End();

            return false;
        }
    }
}