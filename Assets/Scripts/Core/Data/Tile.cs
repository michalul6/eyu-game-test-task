public class Tile
{
    public TileType Type { get; set; }
    public GridPosition Position { get; set; }
    public bool IsMatched { get; set; }

    public Tile(TileType type, GridPosition position)
    {
        Type = type;
        Position = position;
        IsMatched = false;
    }
}
