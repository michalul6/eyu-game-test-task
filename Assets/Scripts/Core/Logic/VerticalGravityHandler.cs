public class VerticalGravityHandler : IGravityHandler
{
    // Y=0 is bottom, Y=height-1 is top. Gravity moves tiles downward toward Y=0.
    public void ApplyGravity(Tile[,] grid)
    {
        int width = grid.GetLength(0);
        int height = grid.GetLength(1);

        for (int x = 0; x < width; x++)
        {
            int writeY = 0; // next position to drop into
            for (int y = 0; y < height; y++)
            {
                var tile = grid[x, y];
                if (tile != null && tile.Type != TileType.None)
                {
                    if (y != writeY)
                    {
                        grid[x, writeY] = tile;
                        grid[x, y] = null;
                        tile.Position = new GridPosition(x, writeY);
                    }
                    writeY++;
                }
            }
            // Above writeY are empty/null; leave as is (refill will populate)
            for (int y = writeY; y < height; y++)
            {
                // ensure slots are null (empties)
                grid[x, y] = grid[x, y] != null && grid[x, y].Type == TileType.None ? null : grid[x, y];
            }
        }
    }
}
