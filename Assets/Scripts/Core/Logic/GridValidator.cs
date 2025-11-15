public class GridValidator : IGridValidator
{
    public bool HasMatches(Tile[,] grid, IMatchDetector detector)
    {
        var matches = detector.FindMatches(grid);
        return matches != null && matches.Count > 0;
    }

    public bool HasPossibleMoves(Tile[,] grid)
    {
        int width = grid.GetLength(0);
        int height = grid.GetLength(1);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Right neighbor
                if (x + 1 < width && TrySwapCreatesMatch(grid, x, y, x + 1, y))
                    return true;
                // Up neighbor
                if (y + 1 < height && TrySwapCreatesMatch(grid, x, y, x, y + 1))
                    return true;
            }
        }
        return false;
    }

    static bool TrySwapCreatesMatch(Tile[,] grid, int x1, int y1, int x2, int y2)
    {
        var a = grid[x1, y1];
        var b = grid[x2, y2];
        if (a == null || b == null) return false;
        if (a.Type == b.Type) return false; // swapping identical types won't change
        if (a.Type == TileType.None && b.Type == TileType.None) return false;

        // Swap types
        var tempType = a.Type;
        a.Type = b.Type;
        b.Type = tempType;

        bool result = HasLocalMatch(grid, x1, y1) || HasLocalMatch(grid, x2, y2);

        // Swap back
        tempType = a.Type;
        a.Type = b.Type;
        b.Type = tempType;

        return result;
    }

    static bool HasLocalMatch(Tile[,] grid, int x, int y)
    {
        var tile = grid[x, y];
        if (tile == null) return false;
        var t = tile.Type;
        if (t == TileType.None || t == TileType.RowBooster) return false;

        int width = grid.GetLength(0);
        int height = grid.GetLength(1);

        // Horizontal count
        int count = 1;
        // left
        for (int cx = x - 1; cx >= 0; cx--)
        {
            var tt = grid[cx, y]?.Type ?? TileType.None;
            if (tt == t) count++; else break;
        }
        // right
        for (int cx = x + 1; cx < width; cx++)
        {
            var tt = grid[cx, y]?.Type ?? TileType.None;
            if (tt == t) count++; else break;
        }
        if (count >= 3) return true;

        // Vertical count
        count = 1;
        // down
        for (int cy = y - 1; cy >= 0; cy--)
        {
            var tt = grid[x, cy]?.Type ?? TileType.None;
            if (tt == t) count++; else break;
        }
        // up
        for (int cy = y + 1; cy < height; cy++)
        {
            var tt = grid[x, cy]?.Type ?? TileType.None;
            if (tt == t) count++; else break;
        }
        return count >= 3;
    }
}
