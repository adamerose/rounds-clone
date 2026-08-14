namespace Rounds.Sim.Maps;

public readonly record struct ArenaBounds(double XMin, double XMax, double YMin, double YMax)
{
    public double Width => XMax - XMin;

    public double Height => YMax - YMin;

    public bool IsValid =>
        double.IsFinite(XMin) && double.IsFinite(XMax) &&
        double.IsFinite(YMin) && double.IsFinite(YMax) &&
        XMin < XMax && YMin < YMax;
}
