using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace SimPlanet;

public enum ComputeKind
{
    Scalar,
    Simd,
    OpenCL
}

/// <summary>
/// Number-crunching kernels shared by the physical simulations (seismic wave fields,
/// tomography, synthetic seismograms, mesh deformation).
///
/// Three implementations of the same maths:
///  - Scalar: plain loops, the reference.
///  - Simd:   System.Numerics vectors, which the JIT maps to AVX2/AVX-512 on x64 and NEON on ARM.
///  - OpenCL: runs the stencil on a GPU when an OpenCL runtime and device are present.
/// The choice is automatic (GPU if there is one, otherwise SIMD) and can be forced with the
/// SIMPLANET_COMPUTE environment variable (scalar | simd | opencl).
/// </summary>
public static class ComputeBackend
{
    public static ComputeKind Preferred { get; private set; } = ComputeKind.Simd;
    public static string Description { get; private set; } = "";
    public static bool OpenClAvailable => OpenClDevice.Shared != null;

    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        string simd = Vector.IsHardwareAccelerated
            ? $"SIMD x{Vector<float>.Count} ({SimdFlavour()})"
            : "scalar (no SIMD)";
        Preferred = Vector.IsHardwareAccelerated ? ComputeKind.Simd : ComputeKind.Scalar;
        Description = simd;

        string? forced = Environment.GetEnvironmentVariable("SIMPLANET_COMPUTE")?.ToLowerInvariant();
        if (forced == "scalar")
        {
            Preferred = ComputeKind.Scalar;
            Description = "scalar (forced)";
            return;
        }
        if (forced == "simd") return;

        var device = OpenClDevice.TryCreateShared();
        if (device != null && (device.IsGpu || forced == "opencl"))
        {
            Preferred = ComputeKind.OpenCL;
            Description = $"OpenCL: {device.Name}";
        }
        else if (device != null)
        {
            Description = $"{simd}; OpenCL CPU device available";
        }
    }

    private static string SimdFlavour()
    {
        if (System.Runtime.Intrinsics.X86.Avx512F.IsSupported && Vector<float>.Count >= 16) return "AVX-512";
        if (System.Runtime.Intrinsics.X86.Avx2.IsSupported) return "AVX2";
        if (System.Runtime.Intrinsics.X86.Sse2.IsSupported) return "SSE2";
        if (System.Runtime.Intrinsics.Arm.AdvSimd.IsSupported) return "NEON";
        return "generic";
    }

    // ---------------------------------------------------------------------------------------
    // Wave equation stencil
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// One explicit time step of the 2D scalar wave equation on a grid that wraps in x:
    ///   next = cur + damp * (cur - prev) + c2 * laplacian(cur)
    /// c2 is (velocity * dt / dx)^2 per cell; damp carries attenuation and absorbing borders.
    /// </summary>
    public static void WaveStep(ComputeKind kind, float[] prev, float[] cur, float[] next, float[] c2, float[] damp, int width, int height)
    {
        if (kind == ComputeKind.Scalar)
        {
            for (int y = 1; y < height - 1; y++) WaveRowScalar(prev, cur, next, c2, damp, width, y, 0, width);
        }
        else if (width * height >= 60000)
        {
            Parallel.For(1, height - 1, y => WaveRowSimd(prev, cur, next, c2, damp, width, y));
        }
        else
        {
            for (int y = 1; y < height - 1; y++) WaveRowSimd(prev, cur, next, c2, damp, width, y);
        }
    }

    private static void WaveRowScalar(float[] prev, float[] cur, float[] next, float[] c2, float[] damp, int width, int y, int from, int to)
    {
        int row = y * width;
        for (int x = from; x < to; x++)
        {
            int i = row + x;
            int left = row + (x == 0 ? width - 1 : x - 1);
            int right = row + (x == width - 1 ? 0 : x + 1);
            float lap = cur[left] + cur[right] + cur[i - width] + cur[i + width] - 4f * cur[i];
            next[i] = cur[i] + damp[i] * (cur[i] - prev[i]) + c2[i] * lap;
        }
    }

    private static void WaveRowSimd(float[] prev, float[] cur, float[] next, float[] c2, float[] damp, int width, int y)
    {
        int n = Vector<float>.Count;
        int row = y * width;
        var four = new Vector<float>(4f);

        WaveRowScalar(prev, cur, next, c2, damp, width, y, 0, 1);
        int x = 1;
        int last = width - 1 - n;
        for (; x <= last; x += n)
        {
            int i = row + x;
            var c = new Vector<float>(cur, i);
            var lap = new Vector<float>(cur, i - 1) + new Vector<float>(cur, i + 1)
                    + new Vector<float>(cur, i - width) + new Vector<float>(cur, i + width) - four * c;
            var result = c + new Vector<float>(damp, i) * (c - new Vector<float>(prev, i)) + new Vector<float>(c2, i) * lap;
            result.CopyTo(next, i);
        }
        WaveRowScalar(prev, cur, next, c2, damp, width, y, x, width);
    }

    // ---------------------------------------------------------------------------------------
    // Small vector helpers
    // ---------------------------------------------------------------------------------------

    /// <summary>dst[i] += a[i] * scale</summary>
    public static void AddScaled(Span<float> dst, ReadOnlySpan<float> a, float scale)
    {
        int n = Vector<float>.Count, i = 0;
        if (Vector.IsHardwareAccelerated && Preferred != ComputeKind.Scalar)
        {
            var s = new Vector<float>(scale);
            for (; i <= dst.Length - n; i += n)
            {
                (new Vector<float>(dst.Slice(i)) + new Vector<float>(a.Slice(i)) * s).CopyTo(dst.Slice(i));
            }
        }
        for (; i < dst.Length; i++) dst[i] += a[i] * scale;
    }

    /// <summary>Sum of a[i] * b[i]</summary>
    public static float Dot(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        int n = Vector<float>.Count, i = 0;
        float sum = 0f;
        if (Vector.IsHardwareAccelerated && Preferred != ComputeKind.Scalar)
        {
            var acc = Vector<float>.Zero;
            for (; i <= a.Length - n; i += n)
            {
                acc += new Vector<float>(a.Slice(i)) * new Vector<float>(b.Slice(i));
            }
            sum = Vector.Sum(acc);
        }
        for (; i < a.Length; i++) sum += a[i] * b[i];
        return sum;
    }

    /// <summary>Largest absolute value in the span.</summary>
    public static float MaxAbs(ReadOnlySpan<float> a)
    {
        int n = Vector<float>.Count, i = 0;
        float max = 0f;
        if (Vector.IsHardwareAccelerated && Preferred != ComputeKind.Scalar)
        {
            var acc = Vector<float>.Zero;
            for (; i <= a.Length - n; i += n)
            {
                acc = Vector.Max(acc, Vector.Abs(new Vector<float>(a.Slice(i))));
            }
            for (int k = 0; k < n; k++) max = MathF.Max(max, acc[k]);
        }
        for (; i < a.Length; i++) max = MathF.Max(max, MathF.Abs(a[i]));
        return max;
    }

    /// <summary>
    /// Convolution of a signal with a short wavelet (same length out as in, centred).
    /// Used to turn reflectivity series into synthetic seismic traces.
    /// </summary>
    public static void Convolve(ReadOnlySpan<float> signal, ReadOnlySpan<float> wavelet, Span<float> output)
    {
        output.Clear();
        int half = wavelet.Length / 2;
        for (int k = 0; k < wavelet.Length; k++)
        {
            float wk = wavelet[k];
            if (wk == 0f) continue;
            int shift = k - half;
            int from = Math.Max(0, shift);
            int to = Math.Min(signal.Length, signal.Length + shift);
            if (to <= from) continue;
            AddScaled(output.Slice(from, to - from), signal.Slice(from - shift, to - from), wk);
        }
    }
}

/// <summary>
/// Minimal OpenCL 1.2 binding (loaded at run time, so the game still starts without it).
/// </summary>
internal sealed unsafe class OpenClDevice
{
    public static OpenClDevice? Shared { get; private set; }
    private static bool _tried;

    public string Name { get; private set; } = "";
    public bool IsGpu { get; private set; }
    public IntPtr Context { get; private set; }
    public IntPtr Queue { get; private set; }
    public IntPtr Device { get; private set; }

    private const ulong DeviceTypeCpu = 1 << 1;
    private const ulong DeviceTypeGpu = 1 << 2;
    private const uint DeviceName = 0x102B;
    private const uint ProgramBuildLog = 0x1183;
    public const ulong MemReadWrite = 1 << 0;
    public const ulong MemCopyHostPtr = 1 << 5;

    private static IntPtr _lib;
    private static delegate* unmanaged[Cdecl]<uint, IntPtr*, uint*, int> _getPlatformIDs;
    private static delegate* unmanaged[Cdecl]<IntPtr, ulong, uint, IntPtr*, uint*, int> _getDeviceIDs;
    private static delegate* unmanaged[Cdecl]<IntPtr, uint, nuint, void*, nuint*, int> _getDeviceInfo;
    private static delegate* unmanaged[Cdecl]<IntPtr*, uint, IntPtr*, IntPtr, IntPtr, int*, IntPtr> _createContext;
    private static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, ulong, int*, IntPtr> _createCommandQueue;
    private static delegate* unmanaged[Cdecl]<IntPtr, uint, byte**, nuint*, int*, IntPtr> _createProgramWithSource;
    private static delegate* unmanaged[Cdecl]<IntPtr, uint, IntPtr*, byte*, IntPtr, IntPtr, int> _buildProgram;
    private static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, uint, nuint, void*, nuint*, int> _getProgramBuildInfo;
    private static delegate* unmanaged[Cdecl]<IntPtr, byte*, int*, IntPtr> _createKernel;
    private static delegate* unmanaged[Cdecl]<IntPtr, ulong, nuint, void*, int*, IntPtr> _createBuffer;
    private static delegate* unmanaged[Cdecl]<IntPtr, uint, nuint, void*, int> _setKernelArg;
    private static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, uint, nuint*, nuint*, nuint*, uint, IntPtr*, IntPtr*, int> _enqueueNDRangeKernel;
    private static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, uint, nuint, nuint, void*, uint, IntPtr*, IntPtr*, int> _enqueueReadBuffer;
    private static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, uint, nuint, nuint, void*, uint, IntPtr*, IntPtr*, int> _enqueueWriteBuffer;
    private static delegate* unmanaged[Cdecl]<IntPtr, int> _finish;
    private static delegate* unmanaged[Cdecl]<IntPtr, int> _releaseMemObject;
    private static delegate* unmanaged[Cdecl]<IntPtr, int> _releaseKernel;
    private static delegate* unmanaged[Cdecl]<IntPtr, int> _releaseProgram;

    public static OpenClDevice? TryCreateShared()
    {
        if (_tried) return Shared;
        _tried = true;
        try
        {
            Shared = Create();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Compute] OpenCL not usable: {ex.Message}");
            Shared = null;
        }
        return Shared;
    }

    private static bool LoadLibrary()
    {
        string[] names = OperatingSystem.IsWindows()
            ? new[] { "OpenCL.dll" }
            : OperatingSystem.IsMacOS()
                ? new[] { "/System/Library/Frameworks/OpenCL.framework/OpenCL" }
                : new[] { "libOpenCL.so.1", "libOpenCL.so" };

        foreach (var name in names)
        {
            if (NativeLibrary.TryLoad(name, out _lib)) break;
        }
        if (_lib == IntPtr.Zero) return false;

        _getPlatformIDs = (delegate* unmanaged[Cdecl]<uint, IntPtr*, uint*, int>)NativeLibrary.GetExport(_lib, "clGetPlatformIDs");
        _getDeviceIDs = (delegate* unmanaged[Cdecl]<IntPtr, ulong, uint, IntPtr*, uint*, int>)NativeLibrary.GetExport(_lib, "clGetDeviceIDs");
        _getDeviceInfo = (delegate* unmanaged[Cdecl]<IntPtr, uint, nuint, void*, nuint*, int>)NativeLibrary.GetExport(_lib, "clGetDeviceInfo");
        _createContext = (delegate* unmanaged[Cdecl]<IntPtr*, uint, IntPtr*, IntPtr, IntPtr, int*, IntPtr>)NativeLibrary.GetExport(_lib, "clCreateContext");
        _createCommandQueue = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, ulong, int*, IntPtr>)NativeLibrary.GetExport(_lib, "clCreateCommandQueue");
        _createProgramWithSource = (delegate* unmanaged[Cdecl]<IntPtr, uint, byte**, nuint*, int*, IntPtr>)NativeLibrary.GetExport(_lib, "clCreateProgramWithSource");
        _buildProgram = (delegate* unmanaged[Cdecl]<IntPtr, uint, IntPtr*, byte*, IntPtr, IntPtr, int>)NativeLibrary.GetExport(_lib, "clBuildProgram");
        _getProgramBuildInfo = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, uint, nuint, void*, nuint*, int>)NativeLibrary.GetExport(_lib, "clGetProgramBuildInfo");
        _createKernel = (delegate* unmanaged[Cdecl]<IntPtr, byte*, int*, IntPtr>)NativeLibrary.GetExport(_lib, "clCreateKernel");
        _createBuffer = (delegate* unmanaged[Cdecl]<IntPtr, ulong, nuint, void*, int*, IntPtr>)NativeLibrary.GetExport(_lib, "clCreateBuffer");
        _setKernelArg = (delegate* unmanaged[Cdecl]<IntPtr, uint, nuint, void*, int>)NativeLibrary.GetExport(_lib, "clSetKernelArg");
        _enqueueNDRangeKernel = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, uint, nuint*, nuint*, nuint*, uint, IntPtr*, IntPtr*, int>)NativeLibrary.GetExport(_lib, "clEnqueueNDRangeKernel");
        _enqueueReadBuffer = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, uint, nuint, nuint, void*, uint, IntPtr*, IntPtr*, int>)NativeLibrary.GetExport(_lib, "clEnqueueReadBuffer");
        _enqueueWriteBuffer = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, uint, nuint, nuint, void*, uint, IntPtr*, IntPtr*, int>)NativeLibrary.GetExport(_lib, "clEnqueueWriteBuffer");
        _finish = (delegate* unmanaged[Cdecl]<IntPtr, int>)NativeLibrary.GetExport(_lib, "clFinish");
        _releaseMemObject = (delegate* unmanaged[Cdecl]<IntPtr, int>)NativeLibrary.GetExport(_lib, "clReleaseMemObject");
        _releaseKernel = (delegate* unmanaged[Cdecl]<IntPtr, int>)NativeLibrary.GetExport(_lib, "clReleaseKernel");
        _releaseProgram = (delegate* unmanaged[Cdecl]<IntPtr, int>)NativeLibrary.GetExport(_lib, "clReleaseProgram");
        return true;
    }

    private static OpenClDevice? Create()
    {
        if (!LoadLibrary()) return null;

        uint platformCount = 0;
        if (_getPlatformIDs(0, null, &platformCount) != 0 || platformCount == 0) return null;
        var platforms = stackalloc IntPtr[(int)platformCount];
        _getPlatformIDs(platformCount, platforms, null);

        // Prefer a GPU on any platform; fall back to a CPU device
        IntPtr chosen = IntPtr.Zero;
        bool gpu = false;
        foreach (ulong type in new[] { DeviceTypeGpu, DeviceTypeCpu })
        {
            for (int p = 0; p < platformCount && chosen == IntPtr.Zero; p++)
            {
                uint deviceCount = 0;
                IntPtr device;
                if (_getDeviceIDs(platforms[p], type, 1, &device, &deviceCount) == 0 && deviceCount > 0)
                {
                    chosen = device;
                    gpu = type == DeviceTypeGpu;
                }
            }
            if (chosen != IntPtr.Zero) break;
        }
        if (chosen == IntPtr.Zero) return null;

        var nameBuffer = stackalloc byte[256];
        nuint nameLength = 0;
        _getDeviceInfo(chosen, DeviceName, 256, nameBuffer, &nameLength);
        string name = Encoding.ASCII.GetString(nameBuffer, (int)Math.Max(0, (long)nameLength - 1)).Trim();

        int err;
        IntPtr context = _createContext(null, 1, &chosen, IntPtr.Zero, IntPtr.Zero, &err);
        if (err != 0 || context == IntPtr.Zero) return null;
        IntPtr queue = _createCommandQueue(context, chosen, 0, &err);
        if (err != 0 || queue == IntPtr.Zero) return null;

        return new OpenClDevice { Name = name, IsGpu = gpu, Context = context, Queue = queue, Device = chosen };
    }

    public IntPtr BuildKernel(string source, string kernelName, out IntPtr program)
    {
        byte[] src = Encoding.ASCII.GetBytes(source);
        byte[] kname = Encoding.ASCII.GetBytes(kernelName + "\0");
        int err;
        IntPtr device = Device;
        fixed (byte* pSrc = src)
        fixed (byte* pName = kname)
        {
            byte* strings = pSrc;
            nuint length = (nuint)src.Length;
            program = _createProgramWithSource(Context, 1, &strings, &length, &err);
            if (err != 0) throw new InvalidOperationException($"clCreateProgramWithSource failed ({err})");

            err = _buildProgram(program, 1, &device, null, IntPtr.Zero, IntPtr.Zero);
            if (err != 0)
            {
                var log = new byte[4096];
                nuint logLength = 0;
                fixed (byte* pLog = log) _getProgramBuildInfo(program, device, ProgramBuildLog, (nuint)log.Length, pLog, &logLength);
                throw new InvalidOperationException($"OpenCL build failed ({err}): {Encoding.ASCII.GetString(log, 0, (int)logLength)}");
            }

            IntPtr kernel = _createKernel(program, pName, &err);
            if (err != 0) throw new InvalidOperationException($"clCreateKernel failed ({err})");
            return kernel;
        }
    }

    public IntPtr CreateBuffer(float[] data)
    {
        int err;
        fixed (float* p = data)
        {
            IntPtr buffer = _createBuffer(Context, MemReadWrite | MemCopyHostPtr, (nuint)(data.Length * sizeof(float)), p, &err);
            if (err != 0) throw new InvalidOperationException($"clCreateBuffer failed ({err})");
            return buffer;
        }
    }

    public void Write(IntPtr buffer, float[] data)
    {
        fixed (float* p = data)
        {
            int err = _enqueueWriteBuffer(Queue, buffer, 1, 0, (nuint)(data.Length * sizeof(float)), p, 0, null, null);
            if (err != 0) throw new InvalidOperationException($"clEnqueueWriteBuffer failed ({err})");
        }
    }

    public void Read(IntPtr buffer, float[] data)
    {
        fixed (float* p = data)
        {
            int err = _enqueueReadBuffer(Queue, buffer, 1, 0, (nuint)(data.Length * sizeof(float)), p, 0, null, null);
            if (err != 0) throw new InvalidOperationException($"clEnqueueReadBuffer failed ({err})");
        }
    }

    public void SetArgBuffer(IntPtr kernel, uint index, IntPtr buffer)
    {
        int err = _setKernelArg(kernel, index, (nuint)sizeof(IntPtr), &buffer);
        if (err != 0) throw new InvalidOperationException($"clSetKernelArg({index}) failed ({err})");
    }

    public void SetArgInt(IntPtr kernel, uint index, int value)
    {
        int err = _setKernelArg(kernel, index, sizeof(int), &value);
        if (err != 0) throw new InvalidOperationException($"clSetKernelArg({index}) failed ({err})");
    }

    public void Run2D(IntPtr kernel, int width, int height)
    {
        var size = stackalloc nuint[2];
        size[0] = (nuint)width;
        size[1] = (nuint)height;
        int err = _enqueueNDRangeKernel(Queue, kernel, 2, null, size, null, 0, null, null);
        if (err != 0) throw new InvalidOperationException($"clEnqueueNDRangeKernel failed ({err})");
    }

    public void Finish() => _finish(Queue);

    public void ReleaseBuffer(IntPtr buffer) { if (buffer != IntPtr.Zero) _releaseMemObject(buffer); }
    public void ReleaseKernel(IntPtr kernel, IntPtr program)
    {
        if (kernel != IntPtr.Zero) _releaseKernel(kernel);
        if (program != IntPtr.Zero) _releaseProgram(program);
    }
}
