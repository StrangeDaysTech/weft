using Weft;
using Weft.Yrs;

namespace Weft.Versioning.Tests;

/// <summary>Runs the parameterized versioning suite over the yrs engine (T027).</summary>
public sealed class YrsVersioningTests : VersioningSuiteBase
{
    protected override ICrdtEngine Engine => YrsEngine.Instance;
}
