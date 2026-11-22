namespace SimPlanet;

/// <summary>
/// Propagates one seismic phase (P or S) across the planet as a 2D wave field, with the wave
/// speed varying from cell to cell. The grid wraps east-west and absorbs at the poles.
/// Runs on SIMD or OpenCL through <see cref="ComputeBackend"/>.
/// </summary>
public sealed class SeismicWaveSolver : IDisposable
{
    public int Width { get; }
    public int Height { get; }
    public ComputeKind Kind { get; private set; }

    /// <summary>The wave field after the last <see cref="Advance"/>.</summary>
    public float[] Field => _cur;
    public int StepCount { get; private set; }

    private float[] _prev, _cur, _next;
    private readonly float[] _c2;
    private readonly float[] _damp;

    // OpenCL state (only when Kind == OpenCL)
    private OpenClDevice? _cl;
    private IntPtr _kernel, _program, _bufPrev, _bufCur, _bufNext, _bufC2, _bufDamp;
    private bool _deviceDirty;

    private const string KernelSource = @"
__kernel void wave_step(__global const float* prev, __global const float* cur, __global float* next,
                        __global const float* c2, __global const float* damp, const int width, const int height)
{
    int x = get_global_id(0);
    int y = get_global_id(1);
    int i = y * width + x;
    if (y == 0 || y == height - 1) { next[i] = 0.0f; return; }
    int l = y * width + (x == 0 ? width - 1 : x - 1);
    int r = y * width + (x == width - 1 ? 0 : x + 1);
    float c = cur[i];
    float lap = cur[l] + cur[r] + cur[i - width] + cur[i + width] - 4.0f * c;
    next[i] = c + damp[i] * (c - prev[i]) + c2[i] * lap;
}";

    /// <param name="courant">Per cell: wave speed as a fraction of a cell per step (must stay below ~0.7).</param>
    public SeismicWaveSolver(int width, int height, float[] courant, ComputeKind kind)
    {
        Width = width;
        Height = height;
        int n = width * height;
        _prev = new float[n];
        _cur = new float[n];
        _next = new float[n];
        _c2 = new float[n];
        _damp = new float[n];

        const int sponge = 6;
        for (int y = 0; y < height; y++)
        {
            int edge = Math.Min(y, height - 1 - y);
            float absorb = edge < sponge ? 0.80f + 0.199f * edge / sponge : 0.999f;
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                float c = Math.Clamp(courant[i], 0.05f, 0.7f);
                _c2[i] = c * c;
                _damp[i] = absorb;
            }
        }

        Kind = kind;
        if (kind == ComputeKind.OpenCL)
        {
            try
            {
                _cl = OpenClDevice.Shared ?? OpenClDevice.TryCreateShared();
                if (_cl == null) throw new InvalidOperationException("no OpenCL device");
                _kernel = _cl.BuildKernel(KernelSource, "wave_step", out _program);
                _bufPrev = _cl.CreateBuffer(_prev);
                _bufCur = _cl.CreateBuffer(_cur);
                _bufNext = _cl.CreateBuffer(_next);
                _bufC2 = _cl.CreateBuffer(_c2);
                _bufDamp = _cl.CreateBuffer(_damp);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Compute] OpenCL wave solver unavailable, using SIMD: {ex.Message}");
                Kind = ComputeKind.Simd;
                _cl = null;
            }
        }
    }

    /// <summary>Adds energy at a cell (the earthquake source time function, sample by sample).</summary>
    public void Inject(int x, int y, float amount)
    {
        if (y < 1 || y >= Height - 1) return;
        x = ((x % Width) + Width) % Width;
        _cur[y * Width + x] += amount;
        _deviceDirty = true;
    }

    /// <summary>Runs a number of time steps and leaves the result in <see cref="Field"/>.</summary>
    public void Advance(int steps)
    {
        if (Kind == ComputeKind.OpenCL && _cl != null)
        {
            if (_deviceDirty)
            {
                _cl.Write(_bufCur, _cur);
                _deviceDirty = false;
            }
            for (int s = 0; s < steps; s++)
            {
                _cl.SetArgBuffer(_kernel, 0, _bufPrev);
                _cl.SetArgBuffer(_kernel, 1, _bufCur);
                _cl.SetArgBuffer(_kernel, 2, _bufNext);
                _cl.SetArgBuffer(_kernel, 3, _bufC2);
                _cl.SetArgBuffer(_kernel, 4, _bufDamp);
                _cl.SetArgInt(_kernel, 5, Width);
                _cl.SetArgInt(_kernel, 6, Height);
                _cl.Run2D(_kernel, Width, Height);
                (_bufPrev, _bufCur, _bufNext) = (_bufCur, _bufNext, _bufPrev);
            }
            _cl.Read(_bufCur, _cur);
        }
        else
        {
            for (int s = 0; s < steps; s++)
            {
                ComputeBackend.WaveStep(Kind, _prev, _cur, _next, _c2, _damp, Width, Height);
                (_prev, _cur, _next) = (_cur, _next, _prev);
            }
        }
        StepCount += steps;
    }

    public void Dispose()
    {
        if (_cl != null)
        {
            _cl.ReleaseBuffer(_bufPrev);
            _cl.ReleaseBuffer(_bufCur);
            _cl.ReleaseBuffer(_bufNext);
            _cl.ReleaseBuffer(_bufC2);
            _cl.ReleaseBuffer(_bufDamp);
            _cl.ReleaseKernel(_kernel, _program);
            _cl = null;
        }
    }
}
