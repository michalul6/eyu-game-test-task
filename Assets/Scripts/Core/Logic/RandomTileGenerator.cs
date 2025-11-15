using System;

public class RandomTileGenerator : ITileGenerator
{
    Random random;
    TileType[] availableTypes;

    public RandomTileGenerator(int seed, TileType[] types)
    {
        random = new Random(seed);
        availableTypes = types ?? Array.Empty<TileType>();
    }

    public void SetRandomSeed(int seed)
    {
        random = new Random(seed);
    }

    public Tile CreateRandomTile(GridPosition position)
    {
        if (availableTypes.Length == 0)
        {
            return new Tile(TileType.None, position);
        }
        int idx = random.Next(availableTypes.Length);
        var type = availableTypes[idx];
        // Safety: never spawn None or booster from factory unless explicitly included
        if (type == TileType.None || type == TileType.RowBooster)
        {
            // fallback to first valid normal type if present
            for (int i = 0; i < availableTypes.Length; i++)
            {
                if (availableTypes[i] != TileType.None && availableTypes[i] != TileType.RowBooster)
                {
                    type = availableTypes[i];
                    break;
                }
            }
        }
        return new Tile(type, position);
    }

    public Tile CreateTile(TileType type, GridPosition position)
    {
        return new Tile(type, position);
    }
}
