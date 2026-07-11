using Weft;
using Weft.Loro;

namespace Weft.Versioning.Tests;

/// <summary>
/// Activa la teoría dual-engine (T034, SC-008): la MISMA suite de versionado (VersioningSuiteBase)
/// corre sobre Loro. Si pasa, la abstracción ICrdtEngine está viva sobre ambos motores (P-IV).
/// </summary>
public sealed class LoroVersioningTests : VersioningSuiteBase
{
    protected override ICrdtEngine Engine => LoroEngine.Instance;
}
