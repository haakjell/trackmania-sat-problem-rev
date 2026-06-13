using GBX.NET.Engines.Game;

/// <summary>One decision point on the track: a left/right checkpoint-stack pair.</summary>
/// <param name="VarId">1-based SAT variable id.</param>
/// <param name="LeftOrders">Clause-group ids reachable by taking the left jump.</param>
/// <param name="RightOrders">Clause-group ids reachable by taking the right jump.</param>
/// <param name="AvgX">Mean X of matched checkpoints (diagnostic).</param>
/// <param name="AvgZ">Mean Z of matched checkpoints (diagnostic).</param>
record DecisionPoint(
    int Col, int Row, int VarId,
    float CenterX, float CenterZ,
    IReadOnlyList<int> LeftOrders,
    IReadOnlyList<int> RightOrders,
    float AvgX, float AvgZ);

/// <summary>Walks the zig-zag track and yields every decision point in order.</summary>
static class Geometry
{
    const float SearchRadius = 52f;

    public static IEnumerable<DecisionPoint> Scan(CGameCtnChallenge map)
    {
        var startBlock = map.GetBlocks()
            .FirstOrDefault(b => b.WaypointSpecialProperty?.Tag?.ToLowerInvariant() == "spawn")
            ?? throw new InvalidOperationException("No start block found.");

        var cpItems = (map.AnchoredObjects ?? [])
            .Where(i => i.WaypointSpecialProperty?.Tag == "LinkedCheckpoint")
            .ToList();

        // Start world position (32 m per block unit in XZ).
        float swx = startBlock.Coord.X * 32f;
        float swz = startBlock.Coord.Z * 32f;

        // Column 0 starts 2 blocks east of start, with user-corrected +10 offset.
        float colStartX = swx + 10 - 64f;
        float colCenterZ = swz + 16;
        int stepDir = -1; // column 0 goes east (X decreasing)

        for (int col = 0; col < 21; col++)
        {
            int rowCount = col == 20 ? 25 : 57;

            for (int row = 0; row < rowCount; row++)
            {
                float centerX = colStartX + row * stepDir * 64f;
                int varId = col * 57 + row + 1;

                var northOrders = new List<int>();
                var southOrders = new List<int>();
                float sumX = 0, sumZ = 0;
                int matched = 0;

                foreach (var i in cpItems)
                {
                    var p = i.AbsolutePositionInMap;
                    if (Dist2D(p.X, p.Z, centerX, colCenterZ) > SearchRadius)
                        continue;
                    sumX += p.X;
                    sumZ += p.Z;
                    matched++;
                    (p.Z >= colCenterZ ? northOrders : southOrders).Add(i.WaypointSpecialProperty!.Order);
                }

                northOrders.Sort();
                southOrders.Sort();

                // Even cols travel east (stepDir=-1): left=north, right=south.
                // Odd cols travel west (stepDir=+1): left=south, right=north.
                var (left, right) = stepDir == -1
                    ? (northOrders, southOrders)
                    : (southOrders, northOrders);

                yield return new DecisionPoint(
                    col, row, varId, centerX, colCenterZ, left, right,
                    matched > 0 ? sumX / matched : centerX,
                    matched > 0 ? sumZ / matched : colCenterZ);
            }

            // Transition to next column: flip direction, 4 blocks south (-128 Z),
            // X shifts west (+48) after even cols, east (-48) after odd cols.
            float lastRowX = colStartX + (rowCount - 1) * stepDir * 64f;
            colStartX = lastRowX + (col % 2 == 0 ? +48f : -48f);
            colCenterZ -= 128f;
            stepDir = -stepDir;
        }
    }

    static float Dist2D(float ax, float az, float bx, float bz) =>
        MathF.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));
}
