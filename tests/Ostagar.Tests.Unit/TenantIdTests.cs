using Ostagar.Core;

namespace Ostagar.Tests.Unit;

public class TenantIdTests
{
    [Fact]
    public void From_rejects_the_empty_guid()
    {
        Assert.Throws<ArgumentException>(() => TenantId.From(Guid.Empty));
    }

    [Fact]
    public void None_is_recognisably_unassigned()
    {
        Assert.True(TenantId.None.IsEmpty);
    }

    [Fact]
    public void TryParse_refuses_an_empty_guid_instead_of_producing_a_bogus_tenant()
    {
        var parsed = TenantId.TryParse(Guid.Empty.ToString("D"), out var result);

        Assert.False(parsed);
        Assert.True(result.IsEmpty);
    }

    [Fact]
    public void Parse_and_try_parse_round_trip_a_real_identifier()
    {
        var tenant = TenantId.New();

        Assert.Equal(tenant, TenantId.Parse(tenant.ToString()));
        Assert.True(TenantId.TryParse(tenant.ToString(), out var roundTripped));
        Assert.Equal(tenant, roundTripped);
    }
}
