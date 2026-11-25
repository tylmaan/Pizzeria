using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pizzeria.Domain.Delivery;
using Pizzeria.Domain.Tenancy;
using Location = Pizzeria.Domain.Tenancy.Location;

namespace Pizzeria.Infrastructure.Persistence.Configurations;

internal sealed class DeliveryZoneConfiguration : IEntityTypeConfiguration<DeliveryZone>
{
    public void Configure(EntityTypeBuilder<DeliveryZone> builder)
    {
        builder.ToTable("DeliveryZones");
        builder.HasKey(z => z.Id);
        builder.Property(z => z.Name).HasMaxLength(DeliveryZone.NameMaxLength).IsRequired();
        builder.Property(z => z.Area).HasColumnType($"geometry(Polygon, {Geo.Srid})").IsRequired();
        builder.Ignore(z => z.Boundary);

        builder.HasOne<Company>().WithMany().HasForeignKey(z => z.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Location>().WithMany().HasForeignKey(z => z.LocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(z => z.CompanyId);
        builder.HasIndex(z => z.LocationId);

        // "Which zone contains this address" is a spatial lookup.
        builder.HasIndex(z => z.Area).HasMethod("gist");
    }
}
