using Weft;
using Weft.Yrs;

namespace Weft.Versioning.Tests;

/// <summary>Ejecuta la suite parametrizada de versionado sobre el motor yrs (T027).</summary>
public sealed class YrsVersioningTests : VersioningSuiteBase
{
    protected override ICrdtEngine Engine => YrsEngine.Instance;
}
