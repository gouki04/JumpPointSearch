using System.Collections.Generic;
using System.Numerics;

namespace jps
{
    public interface IFinder
    {
        List<Vector2Int> FindPath(Vector2Int start, Vector2Int end);
    }
}
