using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Weft.Yrs;

/// <summary>
/// Resuelve el cdylib <c>weft_yrs_ffi</c> por RID (research R11) y verifica que su
/// <c>weft_abi_version</c> coincide con la esperada por este binding — desalineación
/// paquete/binario falla ruidosamente al cargar, no de forma silenciosa.
/// </summary>
internal static class NativeLibraryResolver
{
    // ABI v2 (CHARTER-09): añade weft_doc_new_with_client_id (siembra determinista, FU-012).
    private const uint ExpectedAbiVersion = 2;
    private static int _registered;

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Usage", "CA2255:The 'ModuleInitializer' attribute should not be used in libraries",
        Justification = "Registro único del DllImportResolver nativo antes de cualquier P/Invoke; " +
                        "patrón idiomático y deliberado para un binding nativo por RID.")]
    [ModuleInitializer]
    internal static void Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 0)
        {
            NativeLibrary.SetDllImportResolver(typeof(NativeLibraryResolver).Assembly, Resolve);
        }
    }

    private static nint Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != NativeMethods.Lib)
        {
            return nint.Zero; // otras libs las resuelve el runtime
        }

        string fileName = NativeFileName();
        foreach (string candidate in Candidates(fileName))
        {
            if (File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out nint handle))
            {
                VerifyAbi(handle, candidate);
                return handle;
            }
        }

        // Fallback: rutas por defecto del runtime (NATIVE_DLL_SEARCH_DIRECTORIES).
        if (NativeLibrary.TryLoad(NativeMethods.Lib, assembly, searchPath, out nint fallback))
        {
            VerifyAbi(fallback, NativeMethods.Lib);
            return fallback;
        }

        return nint.Zero;
    }

    private static IEnumerable<string> Candidates(string fileName)
    {
        string baseDir = AppContext.BaseDirectory;
        string rid = RuntimeInformation.RuntimeIdentifier; // p. ej. "linux-x64" o "fedora.44-x64"
        string portable = PortableRid();

        yield return Path.Combine(baseDir, "runtimes", rid, "native", fileName);
        if (portable != rid)
        {
            yield return Path.Combine(baseDir, "runtimes", portable, "native", fileName);
        }
        yield return Path.Combine(baseDir, fileName);
    }

    /// <summary>RID portable canónico (SO + arquitectura) para el layout de paquete NuGet.</summary>
    private static string PortableRid()
    {
        string os =
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win" :
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx" :
            "linux";
        string arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            Architecture.Arm => "arm",
            var other => other.ToString().ToLowerInvariant(),
        };
        return $"{os}-{arch}";
    }

    private static string NativeFileName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return $"{NativeMethods.Lib}.dll";
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return $"lib{NativeMethods.Lib}.dylib";
        }
        return $"lib{NativeMethods.Lib}.so";
    }

    private static unsafe void VerifyAbi(nint handle, string source)
    {
        if (!NativeLibrary.TryGetExport(handle, "weft_abi_version", out nint fn))
        {
            NativeLibrary.Free(handle);
            throw new WeftException(
                $"El binario nativo '{source}' no exporta weft_abi_version: no es un shim de Weft válido.");
        }

        uint actual = ((delegate* unmanaged<uint>)fn)();
        if (actual != ExpectedAbiVersion)
        {
            NativeLibrary.Free(handle);
            throw new WeftException(
                $"ABI del shim nativo '{source}' = {actual}, se esperaba {ExpectedAbiVersion}. " +
                "Reinstala el paquete de Weft con los binarios nativos correctos.");
        }
    }
}
