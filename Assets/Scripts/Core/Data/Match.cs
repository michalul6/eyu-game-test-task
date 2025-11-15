using System.Collections.Generic;

public enum MatchType
{
    Horizontal,
    Vertical,
    Special // For booster matches
}

public class Match
{
    public List<Tile> Tiles { get; }
    public MatchType Type { get; }

    public Match(List<Tile> tiles, MatchType type)
    {
        Tiles = tiles ?? new List<Tile>(0);
        Type = type;
    }

    public bool IsValid() => Tiles != null && Tiles.Count >= 3;
}
