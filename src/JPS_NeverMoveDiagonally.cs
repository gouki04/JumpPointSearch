using System;
using System.Collections.Generic;
using System.Numerics;

namespace jps
{
    public class JPS_NeverMoveDiagonally : JPS_Base
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

            if (dx != 0) { // moving along x
                if ((IsWalkableAt(x, y - 1) && !IsWalkableAt(x - dx, y - 1)) ||
                    (IsWalkableAt(x, y + 1) && !IsWalkableAt(x - dx, y + 1))) {
                    return new Vector2(x, y);
                }
            }
            else if (dy != 0) {
                if ((IsWalkableAt(x - 1, y) && !IsWalkableAt(x - 1, y - dy)) ||
                    (IsWalkableAt(x + 1, y) && !IsWalkableAt(x + 1, y - dy))) {
                    return new Vector2(x, y);
                }
                //When moving vertically, must check for horizontal jump points
                if (Jump(x + 1, y, x, y) != null || Jump(x - 1, y, x, y) != null) {
                    return new Vector2(x, y);
                }
            }
            else {
                throw new Exception("Only horizontal and vertical movements are allowed");
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

                if (dx != 0) {
                    if (IsWalkableAt(x, y - 1)) {
                        yield return new Vector2(x, y - 1);
                    }
                    if (IsWalkableAt(x, y + 1)) {
                        yield return new Vector2(x, y + 1);
                    }
                    if (IsWalkableAt(x + dx, y)) {
                        yield return new Vector2(x + dx, y);
                    }
                }
                else if (dy != 0) {
                    if (IsWalkableAt(x - 1, y)) {
                        yield return new Vector2(x - 1, y);
                    }
                    if (IsWalkableAt(x + 1, y)) {
                        yield return new Vector2(x + 1, y);
                    }
                    if (IsWalkableAt(x, y + dy)) {
                        yield return new Vector2(x, y + dy);
                    }
                }
            }
            // return all neighbors
            else {
                foreach (var n in GetNeighbors(node, EDiagonalMovement.Never)) {
                    yield return n;
                }
            }
        }
    }
}
