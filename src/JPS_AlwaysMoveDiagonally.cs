using System;
using System.Collections.Generic;
using System.Numerics;

namespace jps
{
    public class JPS_AlwaysMoveDiagonally : JPS_Base
    {
        protected override Vector2? Jump(int x, int y, int px, int py)
        {
            var dx = x - px;
            var dy = y - py;

            if (!IsWalkableAt(x, y)) {
                return null;
            }

            if (GetNodeAt(x, y, false) == m_EndNode) {
                return new Vector2(x, y);
            }

            // check for forced neighbors
            // along the diagonal
            if (dx != 0 && dy != 0) {
                if ((IsWalkableAt(x - dx, y + dy) && !IsWalkableAt(x - dx, y)) ||
                    (IsWalkableAt(x + dx, y - dy) && !IsWalkableAt(x, y - dy))) {
                    return new Vector2(x, y);
                }
                // when moving diagonally, must check for vertical/horizontal jump points
                if (Jump(x + dx, y, x, y) != null || Jump(x, y + dy, x, y) != null) {
                    return new Vector2(x, y);
                }
            }
            // horizontally/vertically
            else {
                if (dx != 0) { // moving along x
                    if ((IsWalkableAt(x + dx, y + 1) && !IsWalkableAt(x, y + 1)) ||
                        (IsWalkableAt(x + dx, y - 1) && !IsWalkableAt(x, y - 1))) {
                        return new Vector2(x, y);
                    }
                }
                else {
                    if ((IsWalkableAt(x + 1, y + dy) && !IsWalkableAt(x + 1, y)) ||
                        (IsWalkableAt(x - 1, y + dy) && !IsWalkableAt(x - 1, y))) {
                        return new Vector2(x, y);
                    }
                }
            }

            return Jump(x + dx, y + dy, x, y);
        }

        protected override IEnumerable<Vector2> FindNeighbors(Node node)
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
                        yield return new Vector2(x, y + dy);
                    }
                    if (IsWalkableAt(x + dx, y)) {
                        yield return new Vector2(x + dx, y);
                    }
                    if (IsWalkableAt(x + dx, y + dy)) {
                        yield return new Vector2(x + dx, y + dy);
                    }
                    if (!IsWalkableAt(x - dx, y)) {
                        yield return new Vector2(x - dx, y + dy);
                    }
                    if (!IsWalkableAt(x, y - dy)) {
                        yield return new Vector2(x + dx, y - dy);
                    }
                }
                // search horizontally/vertically
                else {
                    if (dx == 0) {
                        if (IsWalkableAt(x, y + dy)) {
                            yield return new Vector2(x, y + dy);
                        }
                        if (!IsWalkableAt(x + 1, y)) {
                            yield return new Vector2(x + 1, y + dy);
                        }
                        if (!IsWalkableAt(x - 1, y)) {
                            yield return new Vector2(x - 1, y + dy);
                        }
                    }
                    else {
                        if (IsWalkableAt(x + dx, y)) {
                            yield return new Vector2(x + dx, y);
                        }
                        if (!IsWalkableAt(x, y + 1)) {
                            yield return new Vector2(x + dx, y + 1);
                        }
                        if (!IsWalkableAt(x, y - 1)) {
                            yield return new Vector2(x + dx, y - 1);
                        }
                    }
                }
            }
            // return all neighbors
            else {
                foreach (var n in GetNeighbors(node, EDiagonalMovement.Always)) {
                    yield return n;
                }
            }
        }
    }
}
