using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pizzeria.Domain.Drivers;
using Pizzeria.Domain.Tenancy;
using Location = Pizzeria.Domain.Tenancy.Location;

namespace Pizzeria.Infrastructure.Persistence.Configurations;

internal sealed class DriverShiftConfiguration : IEntityTypeConfiguration<DriverShift>
{
    public void Configure(EntityTypeBuilder<DriverShift> builder)
    {
        builder.ToTable("DriverShifts");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.DiscrepancyComment).HasMaxLength(DriverShift.CommentMaxLength);
        builder.Ignore(s => s.ExpectedCash);
        builder.Ignore(s => s.Discrepancy);

        builder.HasOne<Company>().WithMany().HasForeignKey(s => s.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Location>().WithMany().HasForeignKey(s => s.LocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => s.CompanyId);
        builder.HasIndex(s => new { s.LocationId, s.Status });

        // One open shift per driver, guaranteed by the database (two quick taps on "start" cannot open two).
        builder.HasIndex(s => s.DriverId).IsUnique().HasFilter("status = 'Open'");
    }
}
