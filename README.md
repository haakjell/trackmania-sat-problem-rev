# Trackmania SAT problem rev

Reverse engineering the Trackmania map **"Password Please"** (by `orlp`) back
into the CNF-SAT instance it secretly encodes. Though this project independently
produced a solution, [Teggot solved it with CryptoMiniSat less than 24 hours after it was released](https://www.reddit.com/r/TrackMania/comments/1u4lw8t/getting_beaten_by_the_unbeaten_at_project/)

To find the solution, the extracted `.cnf` is fed to CryptoMiniSat. This yields
an assignment that the `check` command will verify if it covers all 4854
checkpoint groups. The full L/R path is in `directions.txt`. Running
CryptoMiniSat on 8 threads on my computer yielded a solution after approx 17
minutes.

Map on TMX: [Password Please](https://trackmania.exchange/mapshow/326842#!)  
Inital reddit thread: [Beating the Unbeaten AT project ](https://www.reddit.com/r/TrackMania/comments/1u46lj7/beating_the_unbeaten_at_project/?sort=top)  
Follow up thread: [Getting beaten by the Unbeaten AT project](https://www.reddit.com/r/TrackMania/comments/1u4lw8t/getting_beaten_by_the_unbeaten_at_project/)

Huge thanks to BigBang1112 for providing
[GBX.NET](https://github.com/BigBang1112/gbx-net) and
[GBX.NET.LZO](https://github.com/BigBang1112/gbx-net). This project would not
have been fun without these.

The map is a giant logic puzzle: a boolean satisfiability problem with
1165 variables and 4854 clauses. To "beat" the map you must drive a path
that collects every checkpoint group. The only way to do that is to solve the
SAT problem. This project extracts that instance into a standard DIMACS `.cnf`
file plus a `variable-map.json` so it can be handed to any SAT solver and the
solution translated back into a sequence of left/right jumps.

## How to run

Every command needs the map passed with `-i`/`--input`:

```
dotnet run metadata -i "../Password Please-no-validation-replay.Map.Gbx"  # map stats
dotnet run probe    -i "../Password Please-no-validation-replay.Map.Gbx"  # tabulate decision points
dotnet run extract  -i "../Password Please-no-validation-replay.Map.Gbx"  # emit cnf + variable-map.json
dotnet run check    -i "../Password Please-no-validation-replay.Map.Gbx" -s ../solution  # verify solution
```

`dotnet run extract` produces:

| File                  | Contents                                                                                       |
| --------------------- | ---------------------------------------------------------------------------------------------- |
| `password-please.cnf` | The SAT instance in DIMACS CNF: `p cnf 1165 4854` + one clause per line                        |
| `variable-map.json`   | For each variable: its grid position and what `true`/`false` mean physically (left/right jump) |

`dotnet run check` reads a DIMACS solution file (lines starting with `v`),
walks every decision point in track order, takes left or right according to the
assignment, and collects the checkpoint groups reachable on that fork. It
outputs the full list of captured groups and writes `directions.txt` with one
line per column showing the L/R sequence. Running it against the CryptoMiniSat
solution captures all 4854 groups, confirming the extraction and the solution
are both correct.

## How the map encodes a SAT problem

### The zig-zag pattern

The drivable path is a zig-zag snake pattern, 21 columns each with of 57
decision points (though the last column is only 25), giving
`20×57 + 25 = `**`1165`** decision points. A decision point is just a fork in
the road where you need to drive one of the paths with no way of going back and
collecting the other path. You start in one corner, drive to the far end of a
column, hairpin into the next column, and snake back the other way.

```
             0         1   2     3   4  …  19  20
       56   ┌───────────┐ ┌───────┐ ┌── … ──┐
       55   │●         ●↓ ↑●     ●↓ ↑●     ●↓
       54   │●         ●│ │●     ●│ │●     ●│
       :    │●          : :       : :       :
       2    │●         ●│ │●     ●│ │●     ●│ ┌ → FINISH
       1    │●         ●│ │●     ●│ │●     ●│ │●
       0    │●         ●↓ ↑●     ●↓ ↑●     ●↓ ↑●
            └ ← START   └─┘       └─┘       └─┘

 each ● is one fork (a left/right stack pair)

```

Travel direction alternates each column. Even columns head East, odd columns
head West.

### Each decision point is one boolean variable

At every decision point the path forks: two side-by-side stacks of checkpoint
gates straddle the centerline, and you can only drive through one of them.

```
              decision point (col, row)  =  variable  V

                        path
                  ────────╫────────►  direction of travel
                          ║
              ┌───────────╨───────────┐
        LEFT  │  A  B  C  │  D  E  F  │  RIGHT
        stack │  orders   │  orders   │  stack
              │ 12,40,87  │  4,55,91  │
              └───────────┴───────────┘
                    │             │
            V = true (drive       V = false (drive
            through left)         through right)
```

Each gate in a stack is assigned a checkpoint group. In the .Gbx map file this
is represented in the `Order` parameter on the checkpoint item block. This order
value is its clause-group id. A stack is 1–20 checkpoints deep, and each
checkpoint belongs to a checkpoint group (clause). So the left stack's `Order`
list and the right stack's `Order` list tell you exactly which clauses you
satisfy by going left vs. right at that point.

### Each clause group is one clause

The same `Order` value appears at many forks all over the map, These are linked
checkpoints. As any trackmania player knows, touching _any one_ of them
satisfies that checkpoint group. That is essentially a logical OR.

To finish the map you must collect every checkpoint group, which is essentially
an AND across all checkpoint groups. So the problem reduces to this:

> Find a left/right choice at each of the 1165 forks such that every one of the
> 4854 checkpoint groups is collected at least once.

### The literal mapping

We pick the convention **`true` = drive left, `false` = drive right**:

| Where gate with order `k` sits  | Literal added to clause `k` | Meaning                                 |
| ------------------------------- | --------------------------- | --------------------------------------- |
| **Left** stack of variable `V`  | `+V`                        | choosing `V = true` collects order `k`  |
| **Right** stack of variable `V` | `−V`                        | choosing `V = false` collects order `k` |

A clause is then just the OR of every literal that can collect its group, e.g.

```
clause for order 14:   -14  151  -1114  0
                       └ at decision point 14, going RIGHT collects order 14
                            └ at point 151, going LEFT collects it
                                  └ at point 1114, going RIGHT collects it
```

The polarity choice is arbitrary, so flipping it yields an equivalent problem.
We just record it in `variable-map.json` to translate a solver's answer back
into real jumps.

### Left and right

The probe reads checkpoint world positions, which are **North/South** of the
path centerline, not left/right. "Left" depends on which way you're facing:

- **Even columns** (driving East) => left is North, right is South.
- **Odd columns** (driving West) => left is South, right is North.

So `Geometry.cs` swaps the North/South stacks into left/right based on column
parity. Getting this wrong would corrupt the formula so its very important to
not make mistakes here. I can not guarantee that mistakes has not been made ;)

## Trackmania coordinate system

I'm not an expert I trackmania map files or mapping, but through some testing
I figured out that the coordinate system seemed to be set up like this:

- East = −X, West = +X, North = +Z, South = −Z
- **32 m** per block unit in XZ (8 m in Y)
- Start block world position: `swx = Coord.X * 32`, `swz = Coord.Z * 32`
- Checkpoint tag is `"LinkedCheckpoint"`; model `GateCheckpointCenter32mv2`
- `WaypointSpecialProperty.Order` is the **clause-group id**

The zig-zag scan walks from the start block:

```
colStartX  = swx + 10 − 64  // column 0 begins here    *Note 1
colCenterZ = swz + 16
stepDir    = −1             // column 0 heads East (X decreasing)

centerX for a row = colStartX + row * stepDir * 64

// after finishing a column:
lastRowX = colStartX + (rowCount − 1) * stepDir * 64
colStartX = lastRowX + (col even ? 48 : −48)        // *Note 2
colCenterZ −= 128                                   // *Note 3
stepDir    = −stepDir                               // flip travel direction
```

Checkpoints within `SearchRadius = 52 m` of a row center are matched to that
decision point and split into North/South stacks by their Z relative to
`colCenterZ`.

- **Note 1**  
  I had to add some minor corrections due to the start block origin not lining
  up where I thought it was in the grid. This is why i have added 10 and 16
  meters to the start coordinates for example.
- **Note 2**  
  Due to the staggered layout, we need to adjust the Z position one and a half
  block in the Eastern direction when going from an odd column to an even one,
  and in the Western direction when going from even to odd.
- **Note 3**  
  Each column is four blocks wide (32m per block makes 128m)

## Code layout

.NET 10 console app

| File          | Responsibility                                                                                                                    |
| ------------- | --------------------------------------------------------------------------------------------------------------------------------- |
| `Program.cs`  | Argument parsing, subcommand dispatch, `metadata` command                                                                         |
| `Geometry.cs` | The zig-zag scan: `Geometry.Scan(map)` yields one `DecisionPoint` per variable (col, row, varId, center, left/right order lists). |
| `Probe.cs`    | `probe` — prints each `DecisionPoint` as a table row for visual verification                                                      |
| `Extract.cs`  | `extract` — builds the clause dictionary and writes the CNF + variable map                                                        |
| `Check.cs`    | `check` — reads a DIMACS solution, simulates the run, verifies all groups are captured, writes `directions.txt`                   |

`probe`, `extract`, and `check` all consume `Geometry.Scan`, so they can never
disagree about the track layout.

### Output formats

**`password-please.cnf`** (DIMACS CNF):

```
p cnf 1165 4854
-14 151 -1114 0
-14 -151 1114 0
14 -151 -1114 0
...
```

**`variable-map.json`** (one entry per variable):

```json
{
  "id": 1,
  "col": 0,
  "row": 0,
  "centerX": 3818,
  "centerZ": 3856,
  "true": "take left jump",
  "false": "take right jump"
}
```

## Solving it

The CNF is standard DIMACS, so any solver can read it. Running CryptoMiniSat:

```
cryptominisat5 --threads 8 password-please.cnf > output.txt
```

The resulting `solution` file will be very long, but at the end you will find
your solution. Notable parts of this file can be found in the root of this repo
as `output.txt`. The file can be checked with
`dotnet run check -i <map> -s solution`, which confirms all 4854 checkpoint
groups are captured. The full left/right path is written to `directions.txt`.
