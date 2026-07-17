using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Weft.Loro;
using Weft.Yrs;

namespace Weft.Versioning.Tests;

/// <summary>
/// Paridad entre el header C (fuente de verdad del contrato) y las declaraciones
/// <c>[LibraryImport]</c> del binding, para <b>ambos</b> shims (FU-017, CHARTER-12).
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué existe.</b> Hasta CHARTER-12, `NativeMethods.cs` y `weft_ffi.h` afirmaban ambos que
/// «un test de CI valida que las declaraciones coinciden con este header». <b>No existía</b>: ni
/// para yrs ni para Loro. La afirmación llevaba meses en el árbol y llegó a engañar a quien redactó
/// FU-017, que pedía «replicar para Loro el test que yrs sí tiene». Este archivo es ese test,
/// creado por primera vez, y es lo que vuelve ciertos aquellos comentarios.
/// </para>
/// <para>
/// <b>Qué cubre.</b> Que el conjunto de funciones declaradas en el header y el declarado en el
/// binding sea el <b>mismo</b> (ninguna sobra, ninguna falta), y que para cada una coincidan la
/// aridad, el orden y los tipos de los parámetros y el tipo de retorno, según el mapa C↔.NET de
/// <see cref="TypeMap"/> — que es el contrato de marshalling que el repo ya usa de facto.
/// </para>
/// <para>
/// <b>Qué NO cubre, y conviene saberlo.</b> Es paridad <i>sintáctica</i>. Que `size_t` case con
/// `nuint` no prueba que el marshalling sea correcto, ni valida la semántica, la convención de
/// llamada, ni el contrato de ownership. Esas garantías siguen viniendo de ASan/LSan y de los tests
/// de round-trip. Un test cuyo alcance se exagera es exactamente la clase de fallo que CHARTER-12
/// cierra, así que este párrafo es parte del test.
/// </para>
/// <para>
/// Las funciones bajo <c>#ifdef WEFT_TEST_HOOKS</c> se excluyen a propósito: existen solo con la
/// feature de Cargo <c>test-hooks</c> y el binding no las declara (<c>PanicSafetyTests</c> las
/// resuelve con <c>NativeLibrary.GetExport</c>). El gate que verifica que no viajan en release vive
/// en <c>release.yml</c>.
/// </para>
/// </remarks>
public sealed class HeaderBindingParityTests
{
    /// <summary>Mapa del subconjunto de C que estos headers usan → la forma .NET del binding.</summary>
    private static readonly Dictionary<string, string> TypeMap = new(StringComparer.Ordinal)
    {
        // Escalares
        ["void"] = "Void",
        ["int32_t"] = "Int32",
        ["uint32_t"] = "UInt32",
        ["uint64_t"] = "UInt64",
        ["size_t"] = "UIntPtr",
        // Punteros de salida: `T**` / `size_t*` → `out nint` / `out nuint` (byref).
        ["uint8_t**"] = "IntPtr&",
        ["size_t*"] = "UIntPtr&",
        ["WeftDoc**"] = "IntPtr&",
        ["WeftLoroDoc**"] = "IntPtr&",
        // Handles opacos prestados (HandleLease, research R2).
        ["WeftDoc*"] = "IntPtr",
        ["WeftLoroDoc*"] = "IntPtr",
        // Bytes de ENTRADA (const) → span con pin automático; bytes crudos (no const) → puntero.
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
            $"[{shim}] el header declara funciones que el binding no tiene: {string.Join(", ", soloEnHeader)}. " +
            $"Si se añadió al shim, decláralas en NativeMethods; si se quitaron, bórralas del header.");
        Assert.True(
            soloEnBinding.Length == 0,
            $"[{shim}] el binding declara funciones que el header no tiene: {string.Join(", ", soloEnBinding)}. " +
            $"El header es la fuente de verdad del contrato: actualízalo.");
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
                continue; // Lo cubre Header_y_binding_declaran_las_mismas_funciones.
            }

            string esperado = MapReturn(shim, name, c.ReturnType);
            if (!string.Equals(esperado, net.ReturnType, StringComparison.Ordinal))
            {
                divergencias.Add($"{name}: retorno header={c.ReturnType}→{esperado} vs binding={net.ReturnType}");
            }

            if (c.Parameters.Count != net.Parameters.Count)
            {
                divergencias.Add(
                    $"{name}: aridad header={c.Parameters.Count} vs binding={net.Parameters.Count}");
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
            $"[{shim}] {divergencias.Count} divergencia(s) header↔binding:{Environment.NewLine}" +
            string.Join(Environment.NewLine, divergencias));
    }

    /// <summary>
    /// Caso negativo: prueba que el test de arriba puede FALLAR. Un test de paridad que no sabe
    /// detectar una divergencia sería la enésima verificación fantasma — el fallo que CHARTER-12
    /// cierra. Sin este caso, los dos anteriores no valen nada.
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
        // Función que el binding real no tiene → la detecta el test de conjuntos.
        Assert.Contains("weft_fantasma", h.Keys);
        // Aridad divergente (3 vs 2 del binding real) → la detecta el test de firma.
        Assert.Equal(3, h["weft_buf_free"].Parameters.Count);
        // Y el mapa traduce lo que sí entiende.
        Assert.Equal("IntPtr&", TypeMap[h["weft_doc_new"].Parameters[0]]);
    }

    /// <summary>
    /// El parser debe reventar ante una declaración que no entiende, nunca ignorarla en silencio:
    /// ignorar es fabricar el fantasma que este archivo persigue (R1 del Charter).
    /// </summary>
    [Fact]
    public void El_parser_falla_ruidosamente_ante_una_declaración_que_no_entiende()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => ParseHeader("int32_t (*weft_callback_raro)(void);"));

        Assert.Contains("weft_", ex.Message, StringComparison.Ordinal);
    }

    // ── Mapeo C → .NET ──

    private static string MapReturn(string shim, string fn, string cType) =>
        TypeMap.TryGetValue(cType, out string? net)
            ? net
            : throw new InvalidOperationException(
                $"[{shim}] tipo de retorno C sin mapear en {fn}: '{cType}'. Añádelo a TypeMap con su " +
                "forma .NET, no lo ignores.");

    private static string MapParam(string shim, string fn, int index, string cType) =>
        TypeMap.TryGetValue(cType, out string? net)
            ? net
            : throw new InvalidOperationException(
                $"[{shim}] tipo de parámetro C sin mapear en {fn} #{index}: '{cType}'. Añádelo a TypeMap.");

    // ── Reflexión sobre el binding ──

    private static IReadOnlyDictionary<string, CFunction> ReflectBinding(string typeName)
    {
        Assembly asm = typeName.StartsWith("Weft.Loro", StringComparison.Ordinal)
            ? typeof(LoroEngine).Assembly
            : typeof(YrsEngine).Assembly;

        Type type = asm.GetType(typeName, throwOnError: true)
            ?? throw new InvalidOperationException($"No se encontró el tipo {typeName}.");

        Dictionary<string, CFunction> result = new(StringComparer.Ordinal);
        foreach (MethodInfo m in type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
        {
            // Solo la superficie del shim: el generador de [LibraryImport] emite además helpers
            // internos que no forman parte del contrato.
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

    // ── Parser del header ──

    private sealed record CFunction(string ReturnType, IReadOnlyList<string> Parameters);

    /// <summary>
    /// Parser deliberadamente acotado al subconjunto de C que estos dos headers usan: declaraciones
    /// <c>TIPO nombre(args);</c> de una superficie escrita a mano por nosotros. No es un parser de C
    /// — no hay AST — y por eso su regla es fallar ante lo que no entiende (ver el test de arriba).
    /// </summary>
    private static IReadOnlyDictionary<string, CFunction> ParseHeader(string text)
    {
        // 1. Fuera comentarios de bloque (estos headers no usan `//`).
        text = Regex.Replace(text, @"/\*.*?\*/", " ", RegexOptions.Singleline);

        // 2. Fuera las directivas de preprocesador, y fuera el bloque de test-hooks entero.
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

        // 3. Una declaración por `;`.
        Dictionary<string, CFunction> functions = new(StringComparer.Ordinal);
        foreach (string rawStmt in sb.ToString().Split(';'))
        {
            string stmt = Regex.Replace(rawStmt, @"\s+", " ").Trim();

            // Solo interesan las declaraciones del shim. Lo demás (typedef, `extern "C" {`, llaves
            // sueltas) no menciona `weft_`. El filtro es a propósito más ancho que «una llamada a
            // función»: cualquier declaración que mencione el prefijo tiene que ser entendida o
            // reventar abajo — si exigiese `weft_…(` pegado, un puntero a función se colaría en
            // silencio, que es justo el hueco que este archivo existe para impedir.
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
                    $"El parser no entiende esta declaración del header y NO la va a ignorar: '{stmt}'. " +
                    "Amplía el parser o simplifica el header — ignorarla en silencio crearía un hueco " +
                    "de verificación (R1 de CHARTER-12).");
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
            // `const uint8_t* field` → tipo `const uint8_t*`, nombre `field`. El nombre se descarta:
            // el contrato es posicional, no nominal.
            string arg = Normalize(rawArg);
            int lastSpace = arg.LastIndexOf(' ');
            types.Add(lastSpace < 0 ? arg : Normalize(arg[..lastSpace]));
        }

        return types;
    }

    /// <summary>Colapsa espacios y pega los `*` al tipo: `uint8_t ** out` → `uint8_t** out`.</summary>
    private static string Normalize(string s) =>
        Regex.Replace(Regex.Replace(s, @"\s+", " "), @"\s*(\*+)\s*", "$1 ").Trim();

    // Localiza un archivo del repo subiendo desde el binario del test (mismo patrón que
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

        throw new FileNotFoundException($"No se encontró '{relative}' desde el binario del test.");
    }
}
