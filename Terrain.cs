namespace MountainGen.WinForms;

/// <summary>
/// Fractal midpoint-displacement height field (diamond-square).
/// Inspired by the recursive midpoint subdivision in the original VB6 MountainGen.
/// The grid shares vertices, avoiding seams between adjacent triangles.
/// </summary>
public sealed class Terrain
{
    public int Size { get; }
    public float[,] Heights { get; }
    public float MinHeight { get; private set; }
    public float MaxHeight { get; private set; }

    private Terrain(int size)
    {
        Size = size;
        Heights = new float[size, size];
    }

    public static Terrain Generate(int detail, int seed, float roughness)
    {
        if (detail < 2 || detail > 9) throw new ArgumentOutOfRangeException(nameof(detail));
        if (roughness < 0 || roughness > 2) throw new ArgumentOutOfRangeException(nameof(roughness));
        int n = (1 << detail) + 1;
        var terrain = new Terrain(n);
        var heights = terrain.Heights;
        var random = new Random(seed);
        float RandomOffset(float amount) => (float)(random.NextDouble() * 2 - 1) * amount;
        heights[0, 0] = 0;
        heights[0, n - 1] = 0;
        heights[n - 1, 0] = 0;
        heights[n - 1, n - 1] = 0;

        float amplitude = 0.48f;
        for (int step = n - 1; step > 1; step /= 2)
        {
            int half = step / 2;
            for (int z = half; z < n; z += step)
            for (int x = half; x < n; x += step)
                heights[x, z] = (heights[x-half,z-half] + heights[x+half,z-half]
                    + heights[x-half,z+half] + heights[x+half,z+half]) * .25f + RandomOffset(amplitude);

            for (int z = 0; z < n; z += half)
            for (int x = ((z / half) % 2 == 0 ? half : 0); x < n; x += step)
            {
                float sum = 0; int count = 0;
                if (x >= half) { sum += heights[x-half,z]; count++; }
                if (x + half < n) { sum += heights[x+half,z]; count++; }
                if (z >= half) { sum += heights[x,z-half]; count++; }
                if (z + half < n) { sum += heights[x,z+half]; count++; }
                heights[x,z] = sum / count + RandomOffset(amplitude);
            }
            amplitude *= MathF.Pow(0.5f, roughness);
        }

        float min = float.PositiveInfinity, max = float.NegativeInfinity;
        foreach (float h in heights) { min = Math.Min(min,h); max = Math.Max(max,h); }
        float span = Math.Max(max-min, 0.000001f);
        for (int z = 0; z < n; z++)
        for (int x = 0; x < n; x++)
            heights[x,z] = (heights[x,z]-min)/span * 1.5f - 0.45f;
        terrain.MinHeight = -0.45f;
        terrain.MaxHeight = 1.05f;
        return terrain;
    }
}