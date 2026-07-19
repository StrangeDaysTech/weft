using Weft;
using Weft.Loro;

namespace Weft.Versioning.Tests;

/// <summary>
/// Activates the dual-engine theory (T034, SC-008): the SAME versioning suite (VersioningSuiteBase)
/// runs over Loro. If it passes, the ICrdtEngine abstraction is alive over both engines (P-IV).
/// </summary>
public sealed class LoroVersioningTests : VersioningSuiteBase
{
    protected override ICrdtEngine Engine => LoroEngine.Instance;
}
