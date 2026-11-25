using Pizzeria.Domain.Common;
using Pizzeria.Domain.Tenancy;

namespace Pizzeria.Domain.Tests.Tenancy;

public sealed class CompanyTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("dominos", "dominos")]
    [InlineData("Pizza-Hut", "pizza-hut")]
    [InlineData("  a1  ", "a1")]
    public void Create_NormalizesSlug(string slug, string expected)
    {
        var company = Company.Create("Pizza", slug, Now);

        Assert.Equal(expected, company.Slug);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("-pizza")]
    [InlineData("pizza-")]
    [InlineData("pizza--hut")]
    [InlineData("pizza hut")]
    [InlineData("pizza.hut")]
    [InlineData("pizzeria-żak")]
    public void Create_InvalidSlug_Throws(string slug)
    {
        var ex = Assert.Throws<DomainException>(() => Company.Create("Pizza", slug, Now));

        Assert.Equal(TenancyErrors.InvalidSlug, ex.Code);
    }

    [Fact]
    public void Create_SlugLongerThanDnsLabel_Throws()
    {
        var ex = Assert.Throws<DomainException>(() => Company.Create("Pizza", new string('a', 64), Now));

        Assert.Equal(TenancyErrors.InvalidSlug, ex.Code);
    }

    [Fact]
    public void Create_TrimsNameAndSetsCreationTime()
    {
        var company = Company.Create("  Pizza Hut  ", "pizza-hut", Now);

        Assert.Equal("Pizza Hut", company.Name);
        Assert.Equal(Now, company.CreatedAt);
        Assert.NotEqual(Guid.Empty, company.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankName_Throws(string name)
    {
        var ex = Assert.Throws<DomainException>(() => Company.Create(name, "pizza", Now));

        Assert.Equal(TenancyErrors.InvalidName, ex.Code);
    }
}
