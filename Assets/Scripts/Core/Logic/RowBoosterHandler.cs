using System.Collections.Generic;

public class RowBoosterHandler : IBoosterHandler
{
    public bool CanHandle(TileType boosterType) => boosterType == TileType.RowBooster;

    public List<Tile> GetAffectedTiles(Tile[,] grid, Tile boosterTile)
    {
        var result = new List<Tile>();
        if (grid == null || boosterTile == null) return result;
        int width = grid.GetLength(0);
        int y = boosterTile.Position.Y;
        for (int x = 0; x < width; x++)
        {
            var tile = grid[x, y];
            if (tile != null)
                result.Add(tile);
        }
        return result;
    }
}
