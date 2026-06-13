using System.Text.Json;
using System.Text.Json.Nodes;
using GBX.NET.Engines.Game;

/// <summary>
/// Turns the track into a CNF-SAT instance.
///
/// Each decision point is one boolean variable: true = take the left jump,
/// false = take the right jump. Each clause-group <c>Order</c> becomes one
/// clause requiring that at least one decision collects that group:
/// a left checkpoint with order k contributes literal +varId to clause k,
/// a right checkpoint contributes -varId.
/// </summary>
static class Extract
{
    const string CnfPath = "../password-please.cnf";
    const string JsonPath = "../variable-map.json";

    public static void Run(CGameCtnChallenge map)
    {
        var clauses = new Dictionary<int, List<int>>(); // order group -> DIMACS literals
        var varEntries = new JsonArray();

        foreach (var d in Geometry.Scan(map))
        {
            foreach (var k in d.LeftOrders)
                (clauses.TryGetValue(k, out var l) ? l : clauses[k] = []).Add(+d.VarId);
            foreach (var k in d.RightOrders)
                (clauses.TryGetValue(k, out var l) ? l : clauses[k] = []).Add(-d.VarId);

            varEntries.Add(new JsonObject
            {
                ["id"]      = d.VarId,
                ["col"]     = d.Col,
                ["row"]     = d.Row,
                ["centerX"] = (int)d.CenterX,
                ["centerZ"] = (int)d.CenterZ,
                ["true"]    = "take left jump",
                ["false"]   = "take right jump",
            });
        }

        int varCount = varEntries.Count;

        using (var w = new StreamWriter(CnfPath))
        {
            w.WriteLine($"p cnf {varCount} {clauses.Count}");
            foreach (var (_, lits) in clauses.OrderBy(kv => kv.Key))
                w.WriteLine(string.Join(" ", lits) + " 0");
        }
        Console.WriteLine($"Wrote {CnfPath}  ({clauses.Count} clauses, {varCount} variables)");

        File.WriteAllText(JsonPath, varEntries.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Wrote {JsonPath}  ({varCount} variables)");
    }
}
