using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

namespace jps
{
    public interface IPriorityQueueNode
    {
        /// <summary>
        /// The Priority to insert this node at.  Must be set BEFORE adding a node to the queue
        /// </summary>
        float Priority
        {
            get;
            set;
        }

        /// <summary>
        /// <b>Used by the priority queue - do not edit this value.</b>
        /// Represents the order the node was inserted in
        /// </summary>
        long InsertionIndex { get; set; }

        /// <summary>
        /// <b>Used by the priority queue - do not edit this value.</b>
        /// Represents the current position in the queue
        /// </summary>
        int QueueIndex { get; set; }
    }

    /// <summary>
    /// The IPriorityQueue interface.  This is mainly here for purists, and in case I decide to add more implementations later.
    /// For speed purposes, it is actually recommended that you *don't* access the priority queue through this interface, since the JIT can
    /// (theoretically?) optimize method calls from concrete-types slightly better.
    /// </summary>
    public interface IPriorityQueue<T> : IEnumerable<T>
        where T : IPriorityQueueNode
    {
        void Remove(T node);
        void UpdatePriority(T node, float priority);
        void Enqueue(T node, float priority);
        T Dequeue();
        T First { get; }
        int Count { get; }
        int MaxSize { get; }
        void Clear();
        bool Contains(T node);
    }

    /// <summary>
    /// An implementation of a min-Priority Queue using a heap.  Has O(1) .Contains()!
    /// See https://bitbucket.org/BlueRaja/high-speed-priority-queue-for-c/wiki/Getting%20Started for more information
    /// </summary>
    /// <typeparam name="T">The values in the queue.  Must implement the PriorityQueueNode interface</typeparam>
    public sealed class HeapPriorityQueue<T> : IPriorityQueue<T>
        where T : class, IPriorityQueueNode
    {
        private int m_NumNodes;
        private readonly List<T> m_Nodes;
        private long m_NumNodesEverEnqueued;

        /// <summary>
        /// Instantiate a new Priority Queue
        /// </summary>
        /// <param name="maxNodes">The max nodes ever allowed to be enqueued (going over this will cause an exception)</param>
        public HeapPriorityQueue(int maxNodes)
        {
            m_NumNodes = 0;
            m_Nodes = new List<T>(maxNodes + 1);
            m_NumNodesEverEnqueued = 0;
        }

        /// <summary>
        /// Returns the number of nodes in the queue.  O(1)
        /// </summary>
        public int Count
        {
            get {
                return m_NumNodes;
            }
        }

        /// <summary>
        /// Returns the maximum number of items that can be enqueued at once in this queue.  Once you hit this number (ie. once Count == MaxSize),
        /// attempting to enqueue another item will throw an exception.  O(1)
        /// </summary>
        public int MaxSize
        {
            get {
                return m_Nodes.Capacity - 1;
            }
        }

        /// <summary>
        /// Removes every node from the queue.  O(n) (So, don't do this often!)
        /// </summary>
        public void Clear()
        {
            m_Nodes.Clear();
            m_NumNodes = 0;
        }

        private T this[int idx]
        {
            get {
                if (idx < 0 || idx >= m_Nodes.Count) {
                    return default(T);
                }

                return m_Nodes[idx];
            }
            set {
                if (idx < 0) {
                    throw new IndexOutOfRangeException($"index({idx}) out of range");
                }
                else if (idx >= m_Nodes.Count) {
                    m_Nodes.AddRange(Enumerable.Repeat(default(T), idx + 1 - m_Nodes.Count));
                }

                m_Nodes[idx] = value;
            }
        }

        /// <summary>
        /// Returns (in O(1)!) whether the given node is in the queue.  O(1)
        /// </summary>
        public bool Contains(T node)
        {
            return (this[node.QueueIndex] == node);
        }

        public void Enqueue(T node)
        {
            m_NumNodes++;
            this[m_NumNodes] = node;
            node.QueueIndex = m_NumNodes;
            node.InsertionIndex = m_NumNodesEverEnqueued++;
            CascadeUp(node);
        }

        /// <summary>
        /// Enqueue a node - .Priority must be set beforehand!  O(log n)
        /// </summary>
        public void Enqueue(T node, float priority)
        {
            node.Priority = priority;
            Enqueue(node);
        }

        private void Swap(T node1, T node2)
        {
            //Swap the nodes
            this[node1.QueueIndex] = node2;
            this[node2.QueueIndex] = node1;

            //Swap their indicies
            var temp = node1.QueueIndex;
            node1.QueueIndex = node2.QueueIndex;
            node2.QueueIndex = temp;
        }

        //Performance appears to be slightly better when this is NOT inlined o_O
        private void CascadeUp(T node)
        {
            //aka Heapify-up
            var parent = node.QueueIndex / 2;
            while (parent >= 1) {
                var parent_node = this[parent];
                if (HasHigherPriority(parent_node, node)) {
                    break;
                }

                //Node has lower priority value, so move it up the heap
                Swap(node, parent_node); //For some reason, this is faster with Swap() rather than (less..?) individual operations, like in CascadeDown()

                parent = node.QueueIndex / 2;
            }
        }

        private void CascadeDown(T node)
        {
            //aka Heapify-down
            T new_parent;
            var final_queue_index = node.QueueIndex;
            while (true) {
                new_parent = node;
                var child_left_index = 2 * final_queue_index;

                //Check if the left-child is higher-priority than the current node
                if (child_left_index > m_NumNodes) {
                    //This could be placed outside the loop, but then we'd have to check newParent != node twice
                    node.QueueIndex = final_queue_index;
                    this[final_queue_index] = node;
                    break;
                }

                var child_left = this[child_left_index];
                if (HasHigherPriority(child_left, new_parent)) {
                    new_parent = child_left;
                }

                //Check if the right-child is higher-priority than either the current node or the left child
                var child_right_index = child_left_index + 1;
                if (child_right_index <= m_NumNodes) {
                    var child_right = this[child_right_index];
                    if (HasHigherPriority(child_right, new_parent)) {
                        new_parent = child_right;
                    }
                }

                //If either of the children has higher (smaller) priority, swap and continue cascading
                if (new_parent != node) {
                    //Move new parent to its new index.  node will be moved once, at the end
                    //Doing it this way is one less assignment operation than calling Swap()
                    this[final_queue_index] = new_parent;

                    var temp = new_parent.QueueIndex;
                    new_parent.QueueIndex = final_queue_index;
                    final_queue_index = temp;
                }
                else {
                    //See note above
                    node.QueueIndex = final_queue_index;
                    this[final_queue_index] = node;
                    break;
                }
            }
        }

        /// <summary>
        /// Returns true if 'higher' has higher priority than 'lower', false otherwise.
        /// Note that calling HasHigherPriority(node, node) (ie. both arguments the same node) will return false
        /// </summary>
        private bool HasHigherPriority(T higher, T lower)
        {
            return (higher.Priority < lower.Priority ||
                (higher.Priority == lower.Priority && higher.InsertionIndex < lower.InsertionIndex));
        }

        /// <summary>
        /// Removes the head of the queue (node with highest priority; ties are broken by order of insertion), and returns it.  O(log n)
        /// </summary>
        public T Dequeue()
        {
            var return_me = this[1];
            Remove(return_me);
            return return_me;
        }

        /// <summary>
        /// Returns the head of the queue, without removing it (use Dequeue() for that).  O(1)
        /// </summary>
        public T First
        {
            get {
                return this[1];
            }
        }

        /// <summary>
        /// This method must be called on a node every time its priority changes while it is in the queue.  
        /// <b>Forgetting to call this method will result in a corrupted queue!</b>
        /// O(log n)
        /// </summary>
        public void UpdatePriority(T node, float priority)
        {
            node.Priority = priority;
            OnNodeUpdated(node);
        }

        private void OnNodeUpdated(T node)
        {
            //Bubble the updated node up or down as appropriate
            var parent_index = node.QueueIndex / 2;
            var parent_node = this[parent_index];

            if (parent_index > 0 && HasHigherPriority(node, parent_node)) {
                CascadeUp(node);
            }
            else {
                //Note that CascadeDown will be called if parentNode == node (that is, node is the root)
                CascadeDown(node);
            }
        }

        /// <summary>
        /// Removes a node from the queue.  Note that the node does not need to be the head of the queue.  O(log n)
        /// </summary>
        public void Remove(T node)
        {
            if (m_NumNodes <= 1) {
                this[1] = null;
                m_NumNodes = 0;
                return;
            }

            //Make sure the node is the last node in the queue
            var was_swapped = false;
            var former_last_node = this[m_NumNodes];
            if (node.QueueIndex != m_NumNodes) {
                //Swap the node with the last node
                Swap(node, former_last_node);
                was_swapped = true;
            }

            m_NumNodes--;
            this[node.QueueIndex] = null;

            if (was_swapped) {
                //Now bubble formerLastNode (which is no longer the last node) up or down as appropriate
                OnNodeUpdated(former_last_node);
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (var i = 1; i <= m_NumNodes; i++) {
                yield return m_Nodes[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// <b>Should not be called in production code.</b>
        /// Checks to make sure the queue is still in a valid state.  Used for testing/debugging the queue.
        /// </summary>
        public bool IsValidQueue()
        {
            for (var i = 1; i < m_Nodes.Count; i++) {
                if (this[i] != null) {
                    var child_left_index = 2 * i;
                    if (child_left_index < m_Nodes.Count && this[child_left_index] != null && HasHigherPriority(this[child_left_index], this[i])) {
                        return false;
                    }

                    var child_right_index = child_left_index + 1;
                    if (child_right_index < m_Nodes.Count && this[child_right_index] != null && HasHigherPriority(this[child_right_index], this[i])) {
                        return false;
                    }
                }
            }
            return true;
        }
    }
}
