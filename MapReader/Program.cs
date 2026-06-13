using GBX.NET;
using GBX.NET.Engines.Game;
using GBX.NET.LZO;

// Argument parsing: [subcommand] -i|--input <mapPath>  (input required)
string subcommand = "probe";
string? mapPath = null;
string solutionPath = "solution";

for (int a = 0; a < args.Length; a++)
{
    switch (args[a])
    {
        case "-i" or "--input":
            if (a + 1 >= args.Length)
            {
                Console.Error.WriteLine($"Missing path after {args[a]}");
                return 1;
            }
            mapPath = args[++a];
            break;
        case "-s" or "--solution":
            if (a + 1 >= args.Length)
            {
                Console.Error.WriteLine($"Missing path after {args[a]}");
                return 1;
            }
            solutionPath = args[++a];
            break;
        default:
            subcommand = args[a];
            break;
    }
}

if (mapPath is null)
{
    Console.Error.WriteLine("Missing map path. Use -i|--input <map>.");
    Console.Error.WriteLine("Usage: MapReader [metadata|probe|extract] -i|--input <map>");
    return 1;
}

try
{
    Gbx.LZO = new Lzo();
    var map = Gbx.ParseNode<CGameCtnChallenge>(mapPath);

    switch (subcommand)
    {
        case "metadata":
            RunMetadata(map);
            break;
        case "probe":
            Probe.Run(map);
            break;
        case "extract":
            Extract.Run(map);
            break;
        case "check":
            Check.Run(map, solutionPath);
            break;
        default:
            Console.Error.WriteLine($"Unknown subcommand: {subcommand}");
            Console.Error.WriteLine("Usage: MapReader [metadata|probe|extract|check] [-i|--input <map>] [-s|--solution <path>]");
            return 1;
    }
}
catch (FileNotFoundException ex)
{
    Console.Error.WriteLine($"File not found: {ex.FileName ?? ex.Message}");
    return 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed to parse map: {ex.GetType().Name}: {ex.Message}");
    return 1;
}

return 0;

static void RunMetadata(CGameCtnChallenge map)
{
    Console.WriteLine("=== Map Metadata ===");
    Console.WriteLine($"Name:        {map.MapName}");
    Console.WriteLine($"UID:         {map.MapUid}");
    Console.WriteLine($"Author:      {map.AuthorLogin}" +
                      (map.AuthorNickname is { } nick ? $" ({nick})" : ""));

    Console.WriteLine();
    Console.WriteLine("=== Medal Times ===");
    Console.WriteLine($"Author:  {FormatTime(map.AuthorTime)}");
    Console.WriteLine($"Gold:    {FormatTime(map.GoldTime)}");
    Console.WriteLine($"Silver:  {FormatTime(map.SilverTime)}");
    Console.WriteLine($"Bronze:  {FormatTime(map.BronzeTime)}");

    Console.WriteLine();
    Console.WriteLine("=== Blocks & Items ===");
    Console.WriteLine($"Blocks:          {map.GetBlocks().Count()}");
    Console.WriteLine($"Ghost blocks:    {map.GetGhostBlocks().Count()}");
    Console.WriteLine($"Anchored items:  {map.AnchoredObjects?.Count ?? 0}");
}

static string FormatTime(TmEssentials.TimeInt32? t) =>
    t is null ? "(null)" : $"{(int)t.Value.TotalMilliseconds} ms";
