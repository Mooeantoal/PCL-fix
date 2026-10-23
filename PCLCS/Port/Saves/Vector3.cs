namespace PCLCS.Port.Saves;

/// <summary>三维坐标（出生点等）。</summary>
public struct Vector3
{
    public double X;
    public double Y;
    public double Z;

    public Vector3(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }
}
