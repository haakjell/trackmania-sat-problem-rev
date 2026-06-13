using GBX.NET.Engines.Game;

/// <summary>
/// Simulates a run through the track using a SAT solution file.
/// CNF encoding (from Extract.cs): +varId literal in a clause = left fork covers it,
/// so variable TRUE (positive in solution) = left jump, FALSE (negative) = right jump.
/// </summary>
static class Check
{
    public static void Run(CGameCtnChallenge map, string solutionPath)
    {
        // Parse the DIMACS solution: lines starting with 'v' hold signed integers.
        // Positive = variable TRUE = left jump taken. Negative = FALSE = right jump taken.
        var assignment = new int[1200]; // assignment[varId] = +1 (left) or -1 (right) or 0 (missing)
        int parsed = 0;
        foreach (var line in File.ReadLines(solutionPath))
        {
            if (!line.StartsWith("v ")) continue;
            foreach (var token in line[2..].Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!int.TryParse(token, out int lit) || lit == 0) continue;
                int id = Math.Abs(lit);
                if (id < assignment.Length)
                {
                    assignment[id] = lit > 0 ? 1 : -1;
                    parsed++;
                }
            }
        }

        Console.Error.WriteLine($"Parsed {parsed} variable assignments from: {solutionPath}");

        var captured = new SortedSet<int>();

        // Collect directions grouped by column for the output file.
        // Geometry.Scan yields decision points in col-major order (col 0 all rows, then col 1, ...).
        var colDirections = new List<List<char>>();
        int currentCol = -1;

        foreach (var d in Geometry.Scan(map))
        {
            if (d.Col != currentCol)
            {
                colDirections.Add([]);
                currentCol = d.Col;
            }

            int sign = d.VarId < assignment.Length ? assignment[d.VarId] : 0;

            colDirections[^1].Add(sign > 0 ? 'L' : sign < 0 ? 'R' : '?');

            if (sign == 0) continue;

            // positive (sign=+1) = left jump; negative (sign=-1) = right jump
            var orders = sign > 0 ? d.LeftOrders : d.RightOrders;
            foreach (var order in orders)
                captured.Add(order);
        }

        Console.WriteLine($"Captured {captured.Count} checkpoint group(s):");
        Console.WriteLine(string.Join(", ", captured));

        if (captured.Count == 4854)
            Console.WriteLine($"\nCaptured all {captured.Count} checkpoint groups!");

        // Write directions file: one line per column, L/R per row.
        const string dirPath = "../directions.txt";
        using (var w = new StreamWriter(dirPath))
        {
            for (int c = 0; c < colDirections.Count; c++)
                w.WriteLine($"Col {c,2}: {string.Concat(colDirections[c])}");
        }
        Console.Error.WriteLine($"Directions written to: {dirPath}");
    }
}
