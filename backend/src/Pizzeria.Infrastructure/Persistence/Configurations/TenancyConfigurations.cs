using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pizzeria.Domain.Tenancy;

namespace Pizzeria.Infrastructure.Persistence.Configurations;

internal sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).HasMaxLength(Company.NameMaxLength).IsRequired();
        builder.Property(c => c.Slug).HasMaxLength(Company.SlugMaxLength).IsRequired();
        builder.HasIndex(c => c.Slug).IsUnique();
        builder.Property(c => c.HalfAndHalfPricing).HasConversion<string>().HasMaxLength(30);
    }
}

internal sealed class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.ToTable("Locations");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Name).HasMaxLength(Location.NameMaxLength).IsRequired();
        builder.Property(l => l.TimeZoneId).HasMaxLength(64).IsRequired();

        builder.OwnsOne(l => l.Address, address =>
        {
            address.Property(a => a.Street).HasMaxLength(Address.StreetMaxLength).IsRequired();
            address.Property(a => a.PostalCode).HasMaxLength(Address.PostalCodeMaxLength).IsRequired();
            address.Property(a => a.City).HasMaxLength(Address.CityMaxLength).IsRequired();
        });
        builder.Navigation(l => l.Address).IsRequired();

        builder.HasOne<Company>().WithMany().HasForeignKey(l => l.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(l => l.CompanyId);
    }
}
