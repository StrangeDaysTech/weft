using System.Runtime.InteropServices;
using Weft;
using Weft.Yrs;

namespace Weft.Core.Tests;

/// <summary>
/// Panic-safety en la frontera (T020, SC-009): un panic del motor se captura como
/// <c>WEFT_ERR_PANIC</c> (nunca cruza la frontera C, que sería UB), el proceso sigue estable, y el
/// binding lo mapea a <see cref="WeftEngineException"/> con <see cref="WeftErrorCode.Panic"/>.
/// </summary>
public sealed class PanicSafetyTests
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int TestPanicFn();

    private static string NativeLibraryPath
    {
        get
        {
            (string rid, string name) =
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ("win-x64", "weft_yrs_ffi.dll") :
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? ("osx-arm64", "libweft_yrs_ffi.dylib") :
                ("linux-x64", "libweft_yrs_ffi.so");
            return Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native", name);
        }
    }

    [Fact]
    public void Test_panic_is_caught_and_process_stays_alive()
    {
        Assert.True(
            File.Exists(NativeLibraryPath),
            $"El cdylib con feature test-hooks no se encontró en {NativeLibraryPath}. " +
            "Compila: cargo build --release --features test-hooks en native/.");

        nint handle = NativeLibrary.Load(NativeLibraryPath);
        try
        {
            nint fnPtr = NativeLibrary.GetExport(handle, "weft_test_panic");
            var testPanic = Marshal.GetDelegateForFunctionPointer<TestPanicFn>(fnPtr);

            // Invocarlo muchas veces: el panic se captura cada vez y el proceso no crashea.
            for (int i = 0; i < 100; i++)
            {
                Assert.Equal(-127, testPanic()); // WEFT_ERR_PANIC
            }
        }
        finally
        {
            NativeLibrary.Free(handle);
        }
        // Llegar aquí sin crash es la evidencia de estabilidad del proceso (SC-009).
    }

    [Fact]
    public void Panic_status_maps_to_engine_exception_with_panic_code()
    {
        var ex = Assert.Throws<WeftEngineException>(() => FfiStatus.ThrowIfError(-127));
        Assert.Equal(WeftErrorCode.Panic, ex.ErrorCode);
    }
}
