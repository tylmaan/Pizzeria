using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pizzeria.Domain.Orders;
using Pizzeria.Domain.Payments;
using Pizzeria.Domain.Tenancy;

namespace Pizzeria.Infrastructure.Persistence.Configurations;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Provider).HasMaxLength(Payment.ProviderMaxLength).IsRequired();
        builder.Property(p => p.ProviderPaymentId).HasMaxLength(Payment.ProviderPaymentIdMaxLength);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne<Company>().WithMany().HasForeignKey(p => p.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Order>().WithMany().HasForeignKey(p => p.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => p.CompanyId);
        builder.HasIndex(p => p.OrderId);

        // Webhooks look payments up by the provider id, which is unique per provider.
        builder.HasIndex(p => new { p.Provider, p.ProviderPaymentId }).IsUnique().HasFilter("provider_payment_id IS NOT NULL");
    }
}
