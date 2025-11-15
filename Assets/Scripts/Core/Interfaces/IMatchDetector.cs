using System.Collections.Generic;

public interface IMatchDetector
{
    List<Match> FindMatches(Tile[,] grid);
}
