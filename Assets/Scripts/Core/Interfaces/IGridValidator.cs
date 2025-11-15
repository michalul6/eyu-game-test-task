public interface IGridValidator
{
    bool HasMatches(Tile[,] grid, IMatchDetector detector);
    bool HasPossibleMoves(Tile[,] grid);
}
