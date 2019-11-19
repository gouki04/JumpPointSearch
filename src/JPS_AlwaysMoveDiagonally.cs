using System;
using System.Collections.Generic;
using System.Numerics;

namespace jps
{
    public class JPS_AlwaysMoveDiagonally : JPS_Base
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
                if ((IsWalkableAt(x - dx, y + dy) && !IsWalkableAt(x - dx, y)) ||
                    (IsWalkableAt(x + dx, y - dy) && !IsWalkableAt(x, y - dy))) {
                    return new Vector2Int(x, y);
                }
                // when moving diagonally, must check for vertical/horizontal jump points
                if (Jump(x + dx, y, x, y) != null || Jump(x, y + dy, x, y) != null) {
                    return new Vector2Int(x, y);
                }
            }
            // horizontally/vertically
            else {
                if (dx != 0) { // moving along x
                    if ((IsWalkableAt(x + dx, y + 1) && !IsWalkableAt(x, y + 1)) ||
                        (IsWalkableAt(x + dx, y - 1) && !IsWalkableAt(x, y - 1))) {
                        return new Vector2Int(x, y);
                    }
                }
                else {
                    if ((IsWalkableAt(x + 1, y + dy) && !IsWalkableAt(x + 1, y)) ||
                        (IsWalkableAt(x - 1, y + dy) && !IsWalkableAt(x - 1, y))) {
                        return new Vector2Int(x, y);
                    }
                }
            }

            return Jump(x + dx, y + dy, x, y);
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

            // ↑
            if (IsWalkableAt(x, y - 1)) {
                yield return new Vector2Int(x, y - 1);
            }
            // →
            if (IsWalkableAt(x + 1, y)) {
                yield return new Vector2Int(x + 1, y);
            }
            // ↓
            if (IsWalkableAt(x, y + 1)) {
                yield return new Vector2Int(x, y + 1);
            }
            // ←
            if (IsWalkableAt(x - 1, y)) {
                yield return new Vector2Int(x - 1, y);
            }

            // ↖
            if (IsWalkableAt(x - 1, y - 1)) {
                yield return new Vector2Int(x - 1, y - 1);
            }
            // ↗
            if (IsWalkableAt(x + 1, y - 1)) {
                yield return new Vector2Int(x + 1, y - 1);
            }
            // ↘
            if (IsWalkableAt(x + 1, y + 1)) {
                yield return new Vector2Int(x + 1, y + 1);
            }
            // ↙
            if (IsWalkableAt(x - 1, y + 1)) {
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
                    if (IsWalkableAt(x + dx, y + dy)) {
                        yield return new Vector2Int(x + dx, y + dy);
                    }
                    if (!IsWalkableAt(x - dx, y)) {
                        yield return new Vector2Int(x - dx, y + dy);
                    }
                    if (!IsWalkableAt(x, y - dy)) {
                        yield return new Vector2Int(x + dx, y - dy);
                    }
                }
                // search horizontally/vertically
                else {
                    if (dx == 0) {
                        if (IsWalkableAt(x, y + dy)) {
                            yield return new Vector2Int(x, y + dy);
                        }
                        if (!IsWalkableAt(x + 1, y)) {
                            yield return new Vector2Int(x + 1, y + dy);
                        }
                        if (!IsWalkableAt(x - 1, y)) {
                            yield return new Vector2Int(x - 1, y + dy);
                        }
                    }
                    else {
                        if (IsWalkableAt(x + dx, y)) {
                            yield return new Vector2Int(x + dx, y);
                        }
                        if (!IsWalkableAt(x, y + 1)) {
                            yield return new Vector2Int(x + dx, y + 1);
                        }
                        if (!IsWalkableAt(x, y - 1)) {
                            yield return new Vector2Int(x + dx, y - 1);
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
