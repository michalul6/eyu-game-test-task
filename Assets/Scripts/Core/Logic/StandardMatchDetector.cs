using System.Collections.Generic;

public class StandardMatchDetector : IMatchDetector
{
    const int MIN_MATCH_LENGTH = 3;

    public List<Match> FindMatches(Tile[,] grid)
    {
        var matches = new List<Match>(8);
        if (grid == null) return matches;

        int width = grid.GetLength(0);
        int height = grid.GetLength(1);

        // Horizontal
        for (int y = 0; y < height; y++)
        {
            TileType currentType = TileType.None;
            int runStartX = 0;
            int runLength = 0;

            for (int x = 0; x <= width; x++)
            {
                TileType t = x < width ? GetType(grid, x, y) : TileType.None; // sentinel at end
                if (x < width && IsMatchable(t) && t == currentType)
                {
                    runLength++;
                }
                else
                {
                    // close previous run
                    if (IsMatchable(currentType) && runLength >= MIN_MATCH_LENGTH)
                    {
                        var tiles = new List<Tile>(runLength);
                        for (int rx = runStartX; rx < runStartX + runLength; rx++)
                        {
                            var tile = grid[rx, y];
                            if (tile != null) tiles.Add(tile);
                        }
                        if (tiles.Count >= MIN_MATCH_LENGTH)
                            matches.Add(new Match(tiles, MatchType.Horizontal));
                    }

                    // start new run
                    if (x < width && IsMatchable(t))
                    {
                        currentType = t;
                        runStartX = x;
                        runLength = 1;
                    }
                    else
                    {
                        currentType = TileType.None;
                        runLength = 0;
                    }
                }
            }
        }

        // Vertical
        for (int x = 0; x < width; x++)
        {
            TileType currentType = TileType.None;
            int runStartY = 0;
            int runLength = 0;

            for (int y = 0; y <= height; y++)
            {
                TileType t = y < height ? GetType(grid, x, y) : TileType.None; // sentinel at end
                if (y < height && IsMatchable(t) && t == currentType)
                {
                    runLength++;
                }
                else
                {
                    if (IsMatchable(currentType) && runLength >= MIN_MATCH_LENGTH)
                    {
                        var tiles = new List<Tile>(runLength);
                        for (int ry = runStartY; ry < runStartY + runLength; ry++)
                        {
                            var tile = grid[x, ry];
                            if (tile != null) tiles.Add(tile);
                        }
                        if (tiles.Count >= MIN_MATCH_LENGTH)
                            matches.Add(new Match(tiles, MatchType.Vertical));
                    }

                    if (y < height && IsMatchable(t))
                    {
                        currentType = t;
                        runStartY = y;
                        runLength = 1;
                    }
                    else
                    {
                        currentType = TileType.None;
                        runLength = 0;
                    }
                }
            }
        }

        return matches;
    }

    static bool IsMatchable(TileType type)
    {
        // Only colored tiles are matchable; exclude None and RowBooster
        return type != TileType.None && type != TileType.RowBooster;
    }

    static TileType GetType(Tile[,] grid, int x, int y)
    {
        var tile = grid[x, y];
        return tile != null ? tile.Type : TileType.None;
    }
}
