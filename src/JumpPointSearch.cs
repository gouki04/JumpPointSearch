using System;

namespace jps
{
    /// <summary>
    /// 寻路的启发函数
    /// dx，dy表示和终点的水平和垂直距离
    /// </summary>
    public static class Heuristic
    {
        /// <summary>
        /// 曼哈顿距离
        /// </summary>
        /// <param name="dx"></param>
        /// <param name="dy"></param>
        /// <returns></returns>
        public static float Manhattan(float dx, float dy)
        {
            return dx + dy;
        }

        /// <summary>
        /// 欧拉距离
        /// </summary>
        /// <param name="dx"></param>
        /// <param name="dy"></param>
        /// <returns></returns>
        public static float Euclidean(float dx, float dy)
        {
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private static readonly float SQRT2 = (float)Math.Sqrt(2.0);

        /// <summary>
        /// 八方向格子距离
        /// @PS 水平和垂直移动1格距离为1，45度斜走距离为sqrt(2)
        /// </summary>
        /// <param name="dx"></param>
        /// <param name="dy"></param>
        /// <returns></returns>
        public static float Octile(float dx, float dy)
        {
            var F = SQRT2 - 1f;
            return (dx < dy) ? F * dx + dy : F * dy + dx;
        }

        /// <summary>
        /// 切比雪夫距离
        /// </summary>
        /// <param name="dx"></param>
        /// <param name="dy"></param>
        /// <returns></returns>
        public static float Chebyshev(float dx, float dy)
        {
            return Math.Max(dx, dy);
        }
    }

    public static class JumpPointSearch
    {
        public static IFinder CreateFinder(int width, int height, Func<int, int, bool> check_is_walkable_at, EDiagonalMovement diagonal = EDiagonalMovement.IfAtMostOneObstacle)
        {
            switch (diagonal) {
                case EDiagonalMovement.Always:
                    var finder1 = new JPS_AlwaysMoveDiagonally();
                    finder1.Init(width, height, check_is_walkable_at);
                    return finder1;
                case EDiagonalMovement.IfAtMostOneObstacle:
                    var finder2 = new JPS_MoveDiagonallyIfAtMostOneObstacle();
                    finder2.Init(width, height, check_is_walkable_at);
                    return finder2;
                case EDiagonalMovement.Never:
                    var finder3 = new JPS_NeverMoveDiagonally();
                    finder3.Init(width, height, check_is_walkable_at);
                    return finder3;
                case EDiagonalMovement.OnlyWhenNoObstacles:
                    var finder4 = new JPS_MoveDiagonallyIfNoObstacle();
                    finder4.Init(width, height, check_is_walkable_at);
                    return finder4;
            }

            throw new Exception($"No Finder for {diagonal}");
        }
    }
}
