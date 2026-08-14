namespace Rounds.Sim.Math;

public static class Trig
{
    public const double Pi = 3.141592653589793238462643383279502884;
    private const double TwoPi = 2.0 * Pi;
    private const double HalfPi = 0.5 * Pi;

    public static (double Sin, double Cos) SinCosDegrees(double degrees) =>
        SinCosRadians(degrees * (Pi / 180.0));

    public static (double Sin, double Cos) SinCosRadians(double radians)
    {
        var angle = radians - (TwoPi * System.Math.Floor((radians + Pi) / TwoPi));
        var cosineSign = 1.0;
        if (angle > HalfPi)
        {
            angle = Pi - angle;
            cosineSign = -1.0;
        }
        else if (angle < -HalfPi)
        {
            angle = -Pi - angle;
            cosineSign = -1.0;
        }

        var squared = angle * angle;
        var sine = angle * (1.0 + (squared * (
            (-1.0 / 6.0) + (squared * (
            (1.0 / 120.0) + (squared * (
            (-1.0 / 5040.0) + (squared * (
            (1.0 / 362880.0) + (squared * (
            (-1.0 / 39916800.0) + (squared / 6227020800.0))))))))))));
        var cosine = 1.0 + (squared * (
            (-1.0 / 2.0) + (squared * (
            (1.0 / 24.0) + (squared * (
            (-1.0 / 720.0) + (squared * (
            (1.0 / 40320.0) + (squared * (
            (-1.0 / 3628800.0) + (squared * (
            (1.0 / 479001600.0) - (squared / 87178291200.0)))))))))))));
        return (sine, cosine * cosineSign);
    }
}
