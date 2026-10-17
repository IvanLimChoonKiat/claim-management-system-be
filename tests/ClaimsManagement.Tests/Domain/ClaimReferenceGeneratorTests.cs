using System.Text.RegularExpressions;
using ClaimsManagement.Domain.Services;

namespace ClaimsManagement.Tests.Domain;

public partial class ClaimReferenceGeneratorTests
{
    [GeneratedRegex(@"^CLM-\d{4}-[0-9A-F]{8}$")]
    private static partial Regex ReferenceFormat();

    [Fact]
    public void Generate_ProducesExpectedFormat()
    {
        var reference = new ClaimReferenceGenerator().Generate(new DateTime(2026, 10, 17, 0, 0, 0, DateTimeKind.Utc));

        Assert.Matches(ReferenceFormat(), reference);
        Assert.StartsWith("CLM-2026-", reference);
    }

    [Fact]
    public void Generate_ProducesDistinctValuesAcrossCalls()
    {
        var generator = new ClaimReferenceGenerator();
        var now = DateTime.UtcNow;

        var first = generator.Generate(now);
        var second = generator.Generate(now);

        Assert.NotEqual(first, second);
    }
}
