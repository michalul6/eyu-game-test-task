public interface ITileGenerator
{
    void SetRandomSeed(int seed);
    Tile CreateRandomTile(GridPosition position);
    Tile CreateTile(TileType type, GridPosition position);
}
