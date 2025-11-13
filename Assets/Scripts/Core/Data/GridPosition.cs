using System;

public struct GridPosition : IEquatable<GridPosition>
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

    public bool Equals(GridPosition other) => X == other.X && Y == other.Y;

    public override bool Equals(object obj) => obj is GridPosition other && Equals(other);

    public override int GetHashCode() => (X * 397) ^ Y;

    public static bool operator ==(GridPosition a, GridPosition b) => a.Equals(b);
    public static bool operator !=(GridPosition a, GridPosition b) => !a.Equals(b);
}
