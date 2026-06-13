using GBX.NET.Engines.Game;

/// <summary>Prints every decision point as a table — used to verify the geometry scan.</summary>
static class Probe
{
    public static void Run(CGameCtnChallenge map)
    {
        Console.WriteLine(
            $"{"Col",-4} {"Row",-4}  {"L",-4}  {"R",-4}  {"dX",-4}  {"dZ",-4}   Left orders          |  Right orders");
        Console.WriteLine(new string('-', 120));

        foreach (var d in Geometry.Scan(map))
        {
            Console.WriteLine(
                $"{d.Col,-4} {d.Row,-4}  {d.LeftOrders.Count,-4}  {d.RightOrders.Count,-4}  " +
                $"{(int)d.AvgX - (int)d.CenterX,-4}  {(int)d.AvgZ - (int)d.CenterZ,-4}  " +
                $"{string.Join(",", d.LeftOrders),-50}  {string.Join(",", d.RightOrders)}");
        }
    }
}
