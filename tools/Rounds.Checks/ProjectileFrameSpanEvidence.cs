namespace Rounds.Checks;

public readonly record struct EvidenceInterval(double Minimum, double Maximum)
{
    public bool Contains(double value) => value >= Minimum && value <= Maximum;
}

public readonly record struct ProjectileFrameSpanEvidence(
    double ProjectileStartCenterX,
    double ProjectileStartCenterY,
    double ProjectileEndCenterX,
    double ProjectileEndCenterY,
    double StableReferenceStartX,
    double StableReferenceStartY,
    double StableReferenceEndX,
    double StableReferenceEndY,
    double PlayerDiameterPixels,
    double ElapsedFrames,
    double TicksPerSourceFrame,
    double CoreCenterUncertaintyPixels,
    double StableReferenceUncertaintyPixels,
    double PlayerDiameterUncertaintyPixels,
    double FrameTimingUncertaintyFrames)
{
    public double CompensatedDeltaX =>
        (ProjectileEndCenterX - ProjectileStartCenterX) -
        (StableReferenceEndX - StableReferenceStartX);

    public double CompensatedDeltaY =>
        (ProjectileEndCenterY - ProjectileStartCenterY) -
        (StableReferenceEndY - StableReferenceStartY);

    public double EuclideanDisplacementPixels => Math.Sqrt(
        (CompensatedDeltaX * CompensatedDeltaX) +
        (CompensatedDeltaY * CompensatedDeltaY));

    public double NormalizedSpeed =>
        EuclideanDisplacementPixels / PlayerDiameterPixels / ElapsedFrames / TicksPerSourceFrame;

    public EvidenceInterval NormalizedSpeedInterval()
    {
        var axisUncertainty =
            (2 * CoreCenterUncertaintyPixels) +
            (2 * StableReferenceUncertaintyPixels);
        var minimumDeltaX = Math.Max(Math.Abs(CompensatedDeltaX) - axisUncertainty, 0);
        var minimumDeltaY = Math.Max(Math.Abs(CompensatedDeltaY) - axisUncertainty, 0);
        var maximumDeltaX = Math.Abs(CompensatedDeltaX) + axisUncertainty;
        var maximumDeltaY = Math.Abs(CompensatedDeltaY) + axisUncertainty;
        var minimumDistance = Math.Sqrt((minimumDeltaX * minimumDeltaX) + (minimumDeltaY * minimumDeltaY));
        var maximumDistance = Math.Sqrt((maximumDeltaX * maximumDeltaX) + (maximumDeltaY * maximumDeltaY));
        var maximumDiameter = PlayerDiameterPixels + PlayerDiameterUncertaintyPixels;
        var minimumDiameter = PlayerDiameterPixels - PlayerDiameterUncertaintyPixels;
        var maximumFrames = ElapsedFrames + FrameTimingUncertaintyFrames;
        var minimumFrames = ElapsedFrames - FrameTimingUncertaintyFrames;

        if (CoreCenterUncertaintyPixels < 0 ||
            StableReferenceUncertaintyPixels < 0 ||
            PlayerDiameterUncertaintyPixels < 0 ||
            FrameTimingUncertaintyFrames < 0 ||
            minimumDiameter <= 0 ||
            minimumFrames <= 0 ||
            TicksPerSourceFrame <= 0)
        {
            return new EvidenceInterval(double.NaN, double.NaN);
        }

        return new EvidenceInterval(
            minimumDistance / maximumDiameter / maximumFrames / TicksPerSourceFrame,
            maximumDistance / minimumDiameter / minimumFrames / TicksPerSourceFrame);
    }
}
