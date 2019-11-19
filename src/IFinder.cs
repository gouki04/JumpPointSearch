using System.Collections.Generic;
using System.Numerics;

namespace jps
{
    public interface IFinder
    {
        List<Vector2> FindPath(Vector2 start, Vector2 end);
    }
}
