using ImGuiNET;
using System;

namespace jps
{
    /// <summary>
    /// 优先队列测试
    /// </summary>
    public class HeapTest
    {
        protected Random m_Rand = null;

        protected class TNode : IPriorityQueueNode
        {
            public float Priority { get; set; }
            public long InsertionIndex { get; set; }
            public int QueueIndex { get; set; }
        }

        protected HeapPriorityQueue<TNode> m_Heap = null;
        protected int m_InputInt = 0;

        public HeapTest()
        {
            m_Rand = new Random(DateTime.Now.Millisecond);

            m_Heap = new HeapPriorityQueue<TNode>(8);
            for (var i = 0; i < 10; ++i) {
                m_Heap.Enqueue(new TNode(), m_Rand.Next(0, 1000));
            }
        }

        public void Init()
        {
        }

        public bool DrawGui()
        {
            ImGui.Begin("Heap");
            ImGui.Text($"Count = {m_Heap.Count}");
            ImGui.Text($"Capacity = {m_Heap.MaxSize}");
            ImGui.Separator();
            foreach (var item in m_Heap) {
                ImGui.Text($"{item.Priority}");
            }
            ImGui.Separator();
            if (ImGui.Button("Dequeue")) {
                m_Heap.Dequeue();
            }
            ImGui.InputInt("", ref m_InputInt);
            ImGui.SameLine();
            if (ImGui.Button("Enqueue")) {
                m_Heap.Enqueue(new TNode(), m_InputInt);
            }
            ImGui.End();
            return false;
        }
    }
}