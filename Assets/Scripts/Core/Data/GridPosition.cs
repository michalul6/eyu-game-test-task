public struct GridPosition
{
    public int X;
    public int Y;

    public GridPosition(int x, int y)
    {
        X = x;
        Y = y;
    }

    public bool IsValid(int gridWidth, int gridHeight)
    {
        return X >= 0 && X < gridWidth && Y >= 0 && Y < gridHeight;
    }

    public bool Compare(GridPosition other) => X == other.X && Y == other.Y;
}
