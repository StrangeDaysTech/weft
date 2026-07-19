using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Weft.Loro;
using Weft.Yrs;

namespace Weft.Versioning.Tests;

/// <summary>
/// Parity between the C header (source of truth for the contract) and the
/// <c>[LibraryImport]</c> declarations of the binding, for <b>both</b> shims (FU-017, CHARTER-12).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it exists.</b> Until CHARTER-12, `NativeMethods.cs` and `weft_ffi.h` both claimed that
/// «a CI test validates that the declarations match this header». <b>It did not exist</b>: neither
/// for yrs nor for Loro. The claim had been in the tree for months and even misled whoever wrote
/// FU-017, which asked to «replicate for Loro the test that yrs does have». This file is that test,
/// created for the first time, and it is what makes those comments true.
/// </para>
/// <para>
/// <b>What it covers.</b> That the set of functions declared in the header and the one declared in
/// the binding is the <b>same</b> (none extra, none missing), and that for each one the arity, the
/// order and the types of the parameters and the return type match, according to the C↔.NET map of
/// <see cref="TypeMap"/> — which is the marshalling contract the repo already uses de facto.
/// </para>
/// <para>
/// <b>What it does NOT cover, and is worth knowing.</b> It is <i>syntactic</i> parity. That `size_t`
/// matches `nuint` does not prove the marshalling is correct, nor does it validate the semantics, the
/// calling convention, or the ownership contract. Those guarantees still come from ASan/LSan and the
/// round-trip tests. A test whose scope is overstated is exactly the class of failure that CHARTER-12
/// closes, so this paragraph is part of the test.
/// </para>
/// <para>
/// The functions under <c>#ifdef WEFT_TEST_HOOKS</c> are excluded on purpose: they exist only with
/// the Cargo feature <c>test-hooks</c> and the binding does not declare them (<c>PanicSafetyTests</c>
/// resolves them via <c>NativeLibrary.GetExport</c>). The gate that verifies they don't ship in
/// release lives in <c>release.yml</c>.
/// </para>
/// </remarks>
public sealed class HeaderBindingParityTests
{
    /// <summary>Map of the subset of C that these headers use → the .NET shape of the binding.</summary>
    private static readonly Dictionary<string, string> TypeMap = new(StringComparer.Ordinal)
    {
        // Scalars
        ["void"] = "Void",
        ["int32_t"] = "Int32",
        ["uint32_t"] = "UInt32",
        ["uint64_t"] = "UInt64",
        ["size_t"] = "UIntPtr",
        // Output pointers: `T**` / `size_t*` → `out nint` / `out nuint` (byref).
        ["uint8_t**"] = "IntPtr&",
        ["size_t*"] = "UIntPtr&",
        ["WeftDoc**"] = "IntPtr&",
        ["WeftLoroDoc**"] = "IntPtr&",
        // Borrowed opaque handles (HandleLease, research R2).
        ["WeftDoc*"] = "IntPtr",
        ["WeftLoroDoc*"] = "IntPtr",
        // INPUT bytes (const) → span with automatic pinning; raw bytes (non-const) → pointer.
        ["const uint8_t*"] = "ReadOnlySpan<Byte>",
        ["uint8_t*"] = "IntPtr",
    };

    public static TheoryData<string, string, string> Shims() => new()
    {
        { "yrs", "native/weft-yrs-ffi/include/weft_ffi.h", "Weft.Yrs.NativeMethods" },
        { "loro", "native/weft-loro-ffi/include/weft_loro_ffi.h", "Weft.Loro.Interop.NativeMethods" },
    };

    [Theory]
    [MemberData(nameof(Shims))]
    public void Header_y_binding_declaran_las_mismas_funciones(string shim, string headerPath, string bindingType)
    {
        IReadOnlyDictionary<string, CFunction> header = ParseHeader(File.ReadAllText(RepoPath(headerPath)));
        IReadOnlyDictionary<string, CFunction> binding = ReflectBinding(bindingType);

        string[] soloEnHeader = [.. header.Keys.Except(binding.Keys).Order()];
        string[] soloEnBinding = [.. binding.Keys.Except(header.Keys).Order()];

        Assert.True(
            soloEnHeader.Length == 0,
            $"[{shim}] the header declares functions the binding does not have: {string.Join(", ", soloEnHeader)}. " +
            $"If they were added to the shim, declare them in NativeMethods; if they were removed, delete them from the header.");
        Assert.True(
            soloEnBinding.Length == 0,
            $"[{shim}] the binding declares functions the header does not have: {string.Join(", ", soloEnBinding)}. " +
            $"The header is the source of truth for the contract: update it.");
    }

    [Theory]
    [MemberData(nameof(Shims))]
    public void Header_y_binding_coinciden_en_firma(string shim, string headerPath, string bindingType)
    {
        IReadOnlyDictionary<string, CFunction> header = ParseHeader(File.ReadAllText(RepoPath(headerPath)));
        IReadOnlyDictionary<string, CFunction> binding = ReflectBinding(bindingType);

        List<string> divergencias = [];
        foreach ((string name, CFunction c) in header.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            if (!binding.TryGetValue(name, out CFunction? net))
            {
                continue; // Covered by Header_y_binding_declaran_las_mismas_funciones.
            }

            string esperado = MapReturn(shim, name, c.ReturnType);
            if (!string.Equals(esperado, net.ReturnType, StringComparison.Ordinal))
            {
                divergencias.Add($"{name}: return header={c.ReturnType}→{esperado} vs binding={net.ReturnType}");
            }

            if (c.Parameters.Count != net.Parameters.Count)
            {
                divergencias.Add(
                    $"{name}: arity header={c.Parameters.Count} vs binding={net.Parameters.Count}");
                continue;
            }

            for (int i = 0; i < c.Parameters.Count; i++)
            {
                string esperadoParam = MapParam(shim, name, i, c.Parameters[i]);
                if (!string.Equals(esperadoParam, net.Parameters[i], StringComparison.Ordinal))
                {
                    divergencias.Add(
                        $"{name}: param #{i} header={c.Parameters[i]}→{esperadoParam} vs binding={net.Parameters[i]}");
                }
            }
        }

        Assert.True(
            divergencias.Count == 0,
            $"[{shim}] {divergencias.Count} divergence(s) header↔binding:{Environment.NewLine}" +
            string.Join(Environment.NewLine, divergencias));
    }

    /// <summary>
    /// Negative case: proves that the test above can FAIL. A parity test that cannot detect a
    /// divergence would be yet another phantom verification — the failure that CHARTER-12 closes.
    /// Without this case, the two previous ones are worth nothing.
    /// </summary>
    [Fact]
    public void El_parser_detecta_divergencias_sintéticas()
    {
        IReadOnlyDictionary<string, CFunction> h = ParseHeader(
            """
            int32_t weft_doc_new(WeftDoc** out_doc);
            int32_t weft_fantasma(WeftDoc* doc);
            void    weft_buf_free(uint8_t* ptr, size_t len, uint32_t sobra);
            """);

        Assert.Equal(3, h.Count);
        // Function the real binding does not have → detected by the set test.
        Assert.Contains("weft_fantasma", h.Keys);
        // Divergent arity (3 vs 2 in the real binding) → detected by the signature test.
        Assert.Equal(3, h["weft_buf_free"].Parameters.Count);
        // And the map translates what it does understand.
        Assert.Equal("IntPtr&", TypeMap[h["weft_doc_new"].Parameters[0]]);
    }

    /// <summary>
    /// The parser must blow up on a declaration it does not understand, never silently ignore it:
    /// ignoring it is manufacturing the phantom this file is chasing (R1 of the Charter).
    /// </summary>
    [Fact]
    public void El_parser_falla_ruidosamente_ante_una_declaración_que_no_entiende()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => ParseHeader("int32_t (*weft_callback_raro)(void);"));

        Assert.Contains("weft_", ex.Message, StringComparison.Ordinal);
    }

    // ── C → .NET mapping ──

    private static string MapReturn(string shim, string fn, string cType) =>
        TypeMap.TryGetValue(cType, out string? net)
            ? net
            : throw new InvalidOperationException(
                $"[{shim}] unmapped C return type in {fn}: '{cType}'. Add it to TypeMap with its " +
                ".NET shape, do not ignore it.");

    private static string MapParam(string shim, string fn, int index, string cType) =>
        TypeMap.TryGetValue(cType, out string? net)
            ? net
            : throw new InvalidOperationException(
                $"[{shim}] unmapped C parameter type in {fn} #{index}: '{cType}'. Add it to TypeMap.");

    // ── Reflection over the binding ──

    private static IReadOnlyDictionary<string, CFunction> ReflectBinding(string typeName)
    {
        Assembly asm = typeName.StartsWith("Weft.Loro", StringComparison.Ordinal)
            ? typeof(LoroEngine).Assembly
            : typeof(YrsEngine).Assembly;

        Type type = asm.GetType(typeName, throwOnError: true)
            ?? throw new InvalidOperationException($"Type {typeName} was not found.");

        Dictionary<string, CFunction> result = new(StringComparer.Ordinal);
        foreach (MethodInfo m in type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
        {
            // Only the shim surface: the [LibraryImport] generator also emits internal helpers
            // that are not part of the contract.
            if (!m.Name.StartsWith("weft_", StringComparison.Ordinal))
            {
                continue;
            }

            result[m.Name] = new CFunction(
                NetTypeName(m.ReturnType),
                [.. m.GetParameters().Select(p => NetTypeName(p.ParameterType))]);
        }

        return result;
    }

    private static string NetTypeName(Type t)
    {
        if (t.IsByRef)
        {
            return NetTypeName(t.GetElementType()!) + "&";
        }

        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ReadOnlySpan<>))
        {
            return $"ReadOnlySpan<{NetTypeName(t.GetGenericArguments()[0])}>";
        }

        return t.Name;
    }

    // ── Header parser ──

    private sealed record CFunction(string ReturnType, IReadOnlyList<string> Parameters);

    /// <summary>
    /// Parser deliberately scoped to the subset of C that these two headers use: declarations of the
    /// form <c>TYPE name(args);</c> from a surface hand-written by us. It is not a C parser — there is
    /// no AST — and that is why its rule is to fail on what it does not understand (see the test above).
    /// </summary>
    private static IReadOnlyDictionary<string, CFunction> ParseHeader(string text)
    {
        // 1. Strip block comments (these headers don't use `//`).
        text = Regex.Replace(text, @"/\*.*?\*/", " ", RegexOptions.Singleline);

        // 2. Strip the preprocessor directives, and strip the entire test-hooks block.
        StringBuilder sb = new();
        bool enTestHooks = false;
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim();
            if (line.StartsWith('#'))
            {
                if (line.StartsWith("#ifdef WEFT_TEST_HOOKS", StringComparison.Ordinal))
                {
                    enTestHooks = true;
                }
                else if (enTestHooks && line.StartsWith("#endif", StringComparison.Ordinal))
                {
                    enTestHooks = false;
                }

                continue;
            }

            if (!enTestHooks)
            {
                sb.Append(line).Append(' ');
            }
        }

        // 3. One declaration per `;`.
        Dictionary<string, CFunction> functions = new(StringComparer.Ordinal);
        foreach (string rawStmt in sb.ToString().Split(';'))
        {
            string stmt = Regex.Replace(rawStmt, @"\s+", " ").Trim();

            // Only the shim declarations matter. Everything else (typedef, `extern "C" {`, stray
            // braces) does not mention `weft_`. The filter is deliberately wider than «a function
            // call»: any declaration that mentions the prefix has to be understood or blow up below —
            // if it required `weft_…(` glued together, a function pointer would slip through in
            // silence, which is exactly the gap this file exists to prevent.
            if (!Regex.IsMatch(stmt, @"\bweft_[a-z0-9_]+"))
            {
                continue;
            }

            Match m = Regex.Match(
                stmt,
                @"^(?<ret>(?:const\s+)?[A-Za-z_][A-Za-z0-9_]*\s*\**)\s+(?<name>weft_[a-z0-9_]+)\s*\((?<args>[^()]*)\)$");
            if (!m.Success)
            {
                throw new InvalidOperationException(
                    $"The parser does not understand this header declaration and will NOT ignore it: '{stmt}'. " +
                    "Extend the parser or simplify the header — silently ignoring it would create a " +
                    "verification gap (R1 of CHARTER-12).");
            }

            string name = m.Groups["name"].Value;
            functions[name] = new CFunction(
                Normalize(m.Groups["ret"].Value),
                ParseArgs(m.Groups["args"].Value));
        }

        return functions;
    }

    private static IReadOnlyList<string> ParseArgs(string args)
    {
        string trimmed = Normalize(args);
        if (trimmed.Length == 0 || string.Equals(trimmed, "void", StringComparison.Ordinal))
        {
            return [];
        }

        List<string> types = [];
        foreach (string rawArg in trimmed.Split(','))
        {
            // `const uint8_t* field` → type `const uint8_t*`, name `field`. The name is discarded:
            // the contract is positional, not nominal.
            string arg = Normalize(rawArg);
            int lastSpace = arg.LastIndexOf(' ');
            types.Add(lastSpace < 0 ? arg : Normalize(arg[..lastSpace]));
        }

        return types;
    }

    /// <summary>Collapses whitespace and glues the `*` to the type: `uint8_t ** out` → `uint8_t** out`.</summary>
    private static string Normalize(string s) =>
        Regex.Replace(Regex.Replace(s, @"\s+", " "), @"\s*(\*+)\s*", "$1 ").Trim();

    // Locates a repo file by walking up from the test binary (same pattern as
    // DeterminismTests.DeterminismCorpusDir).
    private static string RepoPath(string relative)
    {
        for (DirectoryInfo? d = new(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            string candidate = Path.Combine(d.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"'{relative}' was not found from the test binary.");
    }
}
