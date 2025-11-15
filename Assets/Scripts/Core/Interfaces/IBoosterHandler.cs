using System.Collections.Generic;

public interface IBoosterHandler
{
    bool CanHandle(TileType boosterType);
    List<Tile> GetAffectedTiles(Tile[,] grid, Tile boosterTile);
}
