using System;
using System.Collections.Generic;

public class GridManager
{
    public int Width { get; private set; }
    public int Height { get; private set; }
    public Tile[,] Grid { get; private set; }

    /// <summary>
    /// Minimum number of matched tiles required to create a booster.
    /// Set to 4 by default. Set higher to make boosters rarer, or to int.MaxValue to disable booster creation.
    /// </summary>
    public int MinTilesForBooster { get; set; } = 4;

    readonly IMatchDetector matchDetector;
    readonly IGravityHandler gravityHandler;
    readonly ITileGenerator tileGenerator;
    readonly IGridValidator gridValidator;

    static readonly TileType[] DefaultTypes = new[]
   {
        TileType.Red, TileType.Blue, TileType.Green,
        TileType.Yellow, TileType.Purple, TileType.Orange
    };

    public GridManager(
        int width = 6,
        int height = 6,
        int seed = 12345,
        IMatchDetector matchDetector = null,
        IGravityHandler gravityHandler = null,
        ITileGenerator tileGenerator = null,
        IGridValidator gridValidator = null)
    {
        Width = width;
        Height = height;
        this.matchDetector = matchDetector ?? new StandardMatchDetector();
        this.gravityHandler = gravityHandler ?? new VerticalGravityHandler();
        this.tileGenerator = tileGenerator ?? new RandomTileGenerator(seed, DefaultTypes);
        this.gridValidator = gridValidator ?? new GridValidator();
        Initialize(seed);
    }

    public void Initialize(int seed)
    {
        tileGenerator.SetRandomSeed(seed);
        Grid = new Tile[Width, Height];
        const int maxAttempts = 2000;
        int attempts = 0;
        while (true)
        {
            attempts++;
            // Fill randomly - each CreateRandomTile() call advances the random state,
            // so re-rolls will produce different grids even with the same initial seed
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    Grid[x, y] = tileGenerator.CreateRandomTile(new GridPosition(x, y));
                }
            }
            // If any starting matches, re-roll
            if (gridValidator.HasMatches(Grid, matchDetector))
            {
                if (attempts >= maxAttempts) break; // fallback to avoid infinite loop
                continue;
            }
            // Ensure at least one possible move
            if (!HasAnyMoves())
            {
                if (attempts >= maxAttempts) break;
                continue;
            }
            break;
        }
    }

    // Required APIs
    public List<Match> FindMatches()
    {
        return matchDetector.FindMatches(Grid);
    }

    public void Clear(List<Match> matches)
    {
        if (matches == null || matches.Count == 0) return;

        var toClear = new HashSet<(int x, int y)>();
        var boosterPositions = new HashSet<(int x, int y)>();

        // Collect all tiles from matches
        foreach (var match in matches)
        {
            foreach (var tile in match.Tiles)
            {
                toClear.Add((tile.Position.X, tile.Position.Y));
            }

            // Booster creation rule: only for normal matches, not for special (e.g., booster activation)
            if (match.Type != MatchType.RowBooster && match.Tiles != null && match.Tiles.Count >= MinTilesForBooster)
            {
                // Deterministic choice: take the middle tile of the run
                int idx = match.Tiles.Count / 2; // for even lengths, picks the left/lower middle
                var candidate = match.Tiles[idx];
                if (candidate != null)
                {
                    var pos = (candidate.Position.X, candidate.Position.Y);
                    boosterPositions.Add(pos);
                }
            }
        }

        // Expand via boosters present in the clear set
        var boosterExpansions = new List<Tile>();
        foreach (var pos in toClear)
        {
            var tile = Grid[pos.x, pos.y];
            if (tile != null)
            {
                boosterExpansions.AddRange(GetBoosterExpansion(tile));
            }
        }
        foreach (var tile in boosterExpansions)
        {
            toClear.Add((tile.Position.X, tile.Position.Y));
        }

        // Do not clear the selected booster positions; instead, transform them after clearing others
        foreach (var bpos in boosterPositions)
        {
            toClear.Remove(bpos);
        }

        // Execute clear
        foreach (var pos in toClear)
        {
            var tile = Grid[pos.x, pos.y];
            if (tile != null)
            {
                tile.IsMatched = true;
                Grid[pos.x, pos.y] = null;
            }
        }

        // Transform selected survivor tiles into boosters
        foreach (var bpos in boosterPositions)
        {
            var tile = Grid[bpos.x, bpos.y];
            if (tile != null)
            {
                tile.Type = TileType.RowBooster;
                tile.IsMatched = false;
            }
            else
            {
                // Fallback: if tile was somehow cleared (overlap edge case), recreate a booster here
                Grid[bpos.x, bpos.y] = new Tile(TileType.RowBooster, new GridPosition(bpos.x, bpos.y));
            }
        }
    }

    public void ApplyGravity()
    {
        gravityHandler.ApplyGravity(Grid);
    }

    public void Refill()
    {
        for (int y = Height - 1; y >= 0; y--)
        {
            for (int x = 0; x < Width; x++)
            {
                if (Grid[x, y] == null)
                {
                    Grid[x, y] = tileGenerator.CreateRandomTile(new GridPosition(x, y));
                }
            }
        }
    }

    public bool HasAnyMoves()
    {
        return gridValidator.HasPossibleMoves(Grid);
    }

    /// <summary>
    /// Returns the tiles that would be affected if the booster at position is activated, without mutating the grid.
    /// Returns an empty list if position is invalid or does not contain a supported booster.
    /// </summary>
    public List<Tile> GetBoosterAffectedTiles(GridPosition position)
    {
        var result = new List<Tile>();
        if (!position.IsValid(Width, Height)) return result;
        var tile = Grid[position.X, position.Y];
        if (tile == null) return result;
        result.AddRange(GetBoosterExpansion(tile));
        return result;
    }

    /// <summary>
    /// Swaps two adjacent tiles and checks if a match is created.
    /// </summary>
    /// <param name="a">First tile to swap</param>
    /// <param name="b">Second tile to swap (must be adjacent to tile a)</param>
    /// <returns>True if swap creates a match, false otherwise</returns>
    public bool Swap(Tile a, Tile b)
    {
        if (a == null || b == null) return false;
        var pa = a.Position;
        var pb = b.Position;
        if (Math.Abs(pa.X - pb.X) + Math.Abs(pa.Y - pb.Y) != 1) return false;

        // Apply swap
        SwapTilesInGrid(pa, pb);

        // Check if swap creates matches
        var matches = matchDetector.FindMatches(Grid);
        bool createsMatch = matches != null && matches.Count > 0;

        return createsMatch;
    }

    // Helpers
    void ProcessCascade(List<Match> initial = null)
    {
        if (initial != null && initial.Count > 0)
        {
            Clear(initial);
            gravityHandler.ApplyGravity(Grid);
            Refill();
        }

        while (true)
        {
            var matches = matchDetector.FindMatches(Grid);
            if (matches == null || matches.Count == 0) break;
            Clear(matches);
            gravityHandler.ApplyGravity(Grid);
            Refill();
        }
    }

    void SwapTilesInGrid(GridPosition pa, GridPosition pb)
    {
        var a = Grid[pa.X, pa.Y];
        var b = Grid[pb.X, pb.Y];
        Grid[pa.X, pa.Y] = b;
        Grid[pb.X, pb.Y] = a;
        if (a != null) a.Position = pb;
        if (b != null) b.Position = pa;
    }

    List<Tile> GetBoosterExpansion(Tile boosterTile)
    {
        var result = new List<Tile>();
        if (boosterTile == null) return result;

        switch (boosterTile.Type)
        {
            case TileType.RowBooster:
                int y = boosterTile.Position.Y;
                for (int x = 0; x < Width; x++)
                {
                    var tile = Grid[x, y];
                    if (tile != null) result.Add(tile);
                }
                break;
            // Add future booster types here:
            // case TileType.ColumnBooster:
            //     ...
            //     break;
        }

        return result;
    }
}
