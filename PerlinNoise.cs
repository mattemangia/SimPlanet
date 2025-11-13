namespace SimPlanet;

/// <summary>
/// Perlin noise generator for procedural terrain generation
/// </summary>
public class PerlinNoise
{
    private readonly int[] _permutation;
    private const int PermutationSize = 256;

    public PerlinNoise(int seed)
    {
        var random = new Random(seed);
        _permutation = new int[PermutationSize * 2];

        var p = new int[PermutationSize];
        for (int i = 0; i < PermutationSize; i++)
            p[i] = i;

        // Shuffle using Fisher-Yates
        for (int i = PermutationSize - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (p[i], p[j]) = (p[j], p[i]);
        }

        // Duplicate for wrapping
        for (int i = 0; i < PermutationSize * 2; i++)
            _permutation[i] = p[i % PermutationSize];
    }

    public float Noise(float x, float y)
    {
        // Find unit square containing point
        int xi = (int)Math.Floor(x) & 255;
        int yi = (int)Math.Floor(y) & 255;

        // Find relative x, y in square
        float xf = x - (float)Math.Floor(x);
        float yf = y - (float)Math.Floor(y);

        // Compute fade curves
        float u = Fade(xf);
        float v = Fade(yf);

        // Hash coordinates of square corners
        int aa = _permutation[_permutation[xi] + yi];
        int ab = _permutation[_permutation[xi] + yi + 1];
        int ba = _permutation[_permutation[xi + 1] + yi];
        int bb = _permutation[_permutation[xi + 1] + yi + 1];

        // Blend results from corners
        float x1 = Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1, yf), u);
        float x2 = Lerp(Grad(ab, xf, yf - 1), Grad(bb, xf - 1, yf - 1), u);

        return Lerp(x1, x2, v);
    }

    public float OctaveNoise(float x, float y, int octaves, float persistence, float lacunarity)
    {
        float total = 0;
        float frequency = 1;
        float amplitude = 1;
        float maxValue = 0;

        for (int i = 0; i < octaves; i++)
        {
            total += Noise(x * frequency, y * frequency) * amplitude;
            maxValue += amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }

        return total / maxValue;
    }

    private static float Fade(float t)
    {
        return t * t * t * (t * (t * 6 - 15) + 10);
    }

    private static float Lerp(float a, float b, float t)
    {
        return a + t * (b - a);
    }

    private static float Grad(int hash, float x, float y)
    {
        int h = hash & 15;
        float u = h < 8 ? x : y;
        float v = h < 4 ? y : h == 12 || h == 14 ? x : 0;
        return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
    }
}
