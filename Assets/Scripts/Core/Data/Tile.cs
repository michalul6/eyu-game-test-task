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

    public bool CanMatch(Tile other)
    {
        if (other == null) return false;
        if (Type == TileType.None) return false;
        // RowBooster is a special tile; matching rules handled by match logic.
        // Keep CanMatch simple: same non-None type means can match.
        return Type == other.Type;
    }
}
