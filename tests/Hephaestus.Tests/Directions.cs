namespace Hephaestus.Tests;

/// <summary>Which way a lift goes: a two-state type, for the tests of the typed layer.</summary>
internal enum Direction { Down, Up }

/// <summary>Directions as the indicator of a truth, which is one when the lift goes up.</summary>
internal sealed record DirectionProjection : IProjection<Direction> {
    public double Encode(Direction value) => value == Direction.Up ? 1 : 0;

    public Direction Decode(double representation) => representation > 0.5 ? Direction.Up : Direction.Down;
}

/// <summary>A direction carried by one linear expression, normally an indicator: <c>new Switch(up.Indicator, new DirectionProjection())</c>.</summary>
internal sealed record Switch(
    ILinearExpression Expression,
    IProjection<Direction> Projection
) : ILinearlyEncodable<Direction>;
