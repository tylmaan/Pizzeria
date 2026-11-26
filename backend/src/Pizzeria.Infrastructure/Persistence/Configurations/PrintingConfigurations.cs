using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pizzeria.Domain.Orders;
using Pizzeria.Domain.Printing;
using Pizzeria.Domain.Tenancy;

namespace Pizzeria.Infrastructure.Persistence.Configurations;

internal sealed class PrintAgentConfiguration : IEntityTypeConfiguration<PrintAgent>
{
    public void Configure(EntityTypeBuilder<PrintAgent> builder)
    {
        builder.ToTable("PrintAgents");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Name).HasMaxLength(PrintAgent.NameMaxLength).IsRequired();
        builder.Property(a => a.TokenHash).HasMaxLength(64).IsRequired();

        builder.HasOne<Company>().WithMany().HasForeignKey(a => a.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Location>().WithMany().HasForeignKey(a => a.LocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => a.CompanyId);
        builder.HasIndex(a => a.LocationId);

        // Agents authenticate by token: looked up by its hash.
        builder.HasIndex(a => a.TokenHash).IsUnique();
    }
}

internal sealed class PrintJobConfiguration : IEntityTypeConfiguration<PrintJob>
{
    public void Configure(EntityTypeBuilder<PrintJob> builder)
    {
        builder.ToTable("PrintJobs");
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(j => j.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(j => j.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(j => j.Error).HasMaxLength(PrintJob.ErrorMaxLength);

        // Several agents of a location may fetch at the same time: a job goes to one of them (PostgreSQL xmin).
        builder.Property<uint>("xmin").IsRowVersion();

        builder.HasOne<Company>().WithMany().HasForeignKey(j => j.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Location>().WithMany().HasForeignKey(j => j.LocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Order>().WithMany().HasForeignKey(j => j.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(j => j.CompanyId);
        builder.HasIndex(j => j.OrderId);

        // The agent fetches waiting jobs of its location; the panel lists the latest ones.
        builder.HasIndex(j => new { j.LocationId, j.Status, j.CreatedAt });
    }
}
