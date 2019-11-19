using System;
using System.Collections.Generic;
using System.Numerics;

namespace jps
{
    public class JPS_MoveDiagonallyIfNoObstacle : JPS_Base
    {
        protected override Vector2Int? Jump(int x, int y, int px, int py)
        {
            var dx = x - px;
            var dy = y - py;

            if (!IsWalkableAt(x, y)) {
                return null;
            }

            if (GetNodeAt(x, y, false) == m_EndNode) {
                return new Vector2Int(x, y);
            }

            // check for forced neighbors
            // along the diagonal
            if (dx != 0 && dy != 0) {
                // when moving diagonally, must check for vertical/horizontal jump points
                if (Jump(x + dx, y, x, y) != null || Jump(x, y + dy, x, y) != null) {
                    return new Vector2Int(x, y);
                }
            }
            // horizontally/vertically
            else {
                if (dx != 0) { // moving along x
                    if ((IsWalkableAt(x, y - 1) && !IsWalkableAt(x - dx, y - 1)) ||
                        (IsWalkableAt(x, y + 1) && !IsWalkableAt(x - dx, y + 1))) {
                        return new Vector2Int(x, y);
                    }
                }
                else if (dy != 0) {
                    if ((IsWalkableAt(x - 1, y) && !IsWalkableAt(x - 1, y - dy)) ||
                        (IsWalkableAt(x + 1, y) && !IsWalkableAt(x + 1, y - dy))) {
                        return new Vector2Int(x, y);
                    }
                }
            }

            // moving diagonally, must make sure one of the vertical/horizontal
            // neighbors is open to allow the path
            if (IsWalkableAt(x + dx, y) && IsWalkableAt(x, y + dy)) {
                return Jump(x + dx, y + dy, x, y);
            }
            else {
                return null;
            }
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
        protected IEnumerable<Vector2Int> GetNeighbors(Node node)
        {
            var x = node.x;
            var y = node.y;

            bool s0, s1, s2, s3;
            s0 = s1 = s2 = s3 = false;

            // ↑
            if (IsWalkableAt(x, y - 1)) {
                yield return new Vector2Int(x, y - 1);
                s0 = true;
            }
            // →
            if (IsWalkableAt(x + 1, y)) {
                yield return new Vector2Int(x + 1, y);
                s1 = true;
            }
            // ↓
            if (IsWalkableAt(x, y + 1)) {
                yield return new Vector2Int(x, y + 1);
                s2 = true;
            }
            // ←
            if (IsWalkableAt(x - 1, y)) {
                yield return new Vector2Int(x - 1, y);
                s3 = true;
            }

            var d0 = s3 && s0;
            var d1 = s0 && s1;
            var d2 = s1 && s2;
            var d3 = s2 && s3;

            // ↖
            if (d0 && IsWalkableAt(x - 1, y - 1)) {
                yield return new Vector2Int(x - 1, y - 1);
            }
            // ↗
            if (d1 && IsWalkableAt(x + 1, y - 1)) {
                yield return new Vector2Int(x + 1, y - 1);
            }
            // ↘
            if (d2 && IsWalkableAt(x + 1, y + 1)) {
                yield return new Vector2Int(x + 1, y + 1);
            }
            // ↙
            if (d3 && IsWalkableAt(x - 1, y + 1)) {
                yield return new Vector2Int(x - 1, y + 1);
            }
        }

        protected override IEnumerable<Vector2Int> FindNeighbors(Node node)
        {
            var parent = node.parent;
            var x = node.x;
            var y = node.y;

            // directed pruning: can ignore most neighbors, unless forced.
            if (parent != null) {
                var px = parent.x;
                var py = parent.y;

                // get the normalized direction of travel
                var dx = (x - px) / Math.Max(Math.Abs(x - px), 1);
                var dy = (y - py) / Math.Max(Math.Abs(y - py), 1);

                // search diagonally
                if (dx != 0 && dy != 0) {
                    if (IsWalkableAt(x, y + dy)) {
                        yield return new Vector2Int(x, y + dy);
                    }
                    if (IsWalkableAt(x + dx, y)) {
                        yield return new Vector2Int(x + dx, y);
                    }
                    if (IsWalkableAt(x, y + dy) && IsWalkableAt(x + dx, y)) {
                        yield return new Vector2Int(x + dx, y + dy);
                    }
                }
                // search horizontally/vertically
                else {
                    var is_next_walkable = false;
                    if (dx != 0) {
                        is_next_walkable = IsWalkableAt(x + dx, y);
                        var is_top_walkable = IsWalkableAt(x, y + 1);
                        var is_bottom_walkable = IsWalkableAt(x, y - 1);

                        if (is_next_walkable) {
                            yield return new Vector2Int(x + dx, y);
                            if (is_top_walkable) {
                                yield return new Vector2Int(x + dx, y + 1);
                            }
                            if (is_bottom_walkable) {
                                yield return new Vector2Int(x + dx, y - 1);
                            }
                        }
                        if (is_top_walkable) {
                            yield return new Vector2Int(x, y + 1);
                        }
                        if (is_bottom_walkable) {
                            yield return new Vector2Int(x, y - 1);
                        }
                    }
                    else if (dy != 0) {
                        is_next_walkable = IsWalkableAt(x, y + dy);
                        var is_right_walkable = IsWalkableAt(x + 1, y);
                        var is_left_walkable = IsWalkableAt(x - 1, y);

                        if (is_next_walkable) {
                            yield return new Vector2Int(x, y + dy);
                            if (is_right_walkable) {
                                yield return new Vector2Int(x + 1, y + dy);
                            }
                            if (is_left_walkable) {
                                yield return new Vector2Int(x - 1, y + dy);
                            }
                        }
                        if (is_right_walkable) {
                            yield return new Vector2Int(x + 1, y);
                        }
                        if (is_left_walkable) {
                            yield return new Vector2Int(x - 1, y);
                        }
                    }
                }
            }
            // return all neighbors
            else {
                foreach (var n in GetNeighbors(node)) {
                    yield return n;
                }
            }
        }
    }
}
