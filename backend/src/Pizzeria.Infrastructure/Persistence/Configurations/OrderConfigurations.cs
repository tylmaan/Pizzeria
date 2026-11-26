using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pizzeria.Domain.Delivery;
using Pizzeria.Domain.Orders;
using Pizzeria.Domain.Tenancy;
using Pizzeria.Infrastructure.Identity;
using Pizzeria.Infrastructure.Orders;
using Location = Pizzeria.Domain.Tenancy.Location;

namespace Pizzeria.Infrastructure.Persistence.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.TrackingToken).HasMaxLength(64).IsRequired();
        builder.HasIndex(o => o.TrackingToken).IsUnique();

        // Enums as names: readable in SQL reports and safe against reordering.
        builder.Property(o => o.Source).HasConversion<string>().HasMaxLength(20);
        builder.Property(o => o.Fulfillment).HasConversion<string>().HasMaxLength(20);
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(o => o.PaymentMethod).HasConversion<string>().HasMaxLength(30);
        builder.Property(o => o.PaymentStatus).HasConversion<string>().HasMaxLength(30);

        builder.Property(o => o.CustomerName).HasMaxLength(Order.CustomerNameMaxLength).IsRequired();
        builder.Property(o => o.CustomerPhone).HasMaxLength(Order.PhoneMaxLength).IsRequired();
        builder.Property(o => o.Notes).HasMaxLength(Order.NotesMaxLength);
        builder.Property(o => o.DeliveryNotes).HasMaxLength(Order.NotesMaxLength);
        builder.Property(o => o.CancellationReason).HasMaxLength(Order.ReasonMaxLength);
        builder.Property(o => o.DeliveryPoint).HasColumnType($"geometry(Point, {Geo.Srid})");

        builder.OwnsOne(o => o.DeliveryAddress, address =>
        {
            address.Property(a => a.Street).HasColumnName("DeliveryStreet").HasMaxLength(Address.StreetMaxLength);
            address.Property(a => a.PostalCode).HasColumnName("DeliveryPostalCode").HasMaxLength(Address.PostalCodeMaxLength);
            address.Property(a => a.City).HasColumnName("DeliveryCity").HasMaxLength(Address.CityMaxLength);
        });

        builder.HasOne<Company>().WithMany().HasForeignKey(o => o.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Location>().WithMany().HasForeignKey(o => o.LocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(o => o.CompanyId);
        builder.HasIndex(o => new { o.LocationId, o.Status });
        builder.HasIndex(o => new { o.LocationId, o.PlacedAt });
        builder.HasIndex(o => o.DriverId);

        // Deleting a customer account keeps the order (accounting) and drops the link.
        builder.HasOne<AppUser>().WithMany().HasForeignKey(o => o.CustomerId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(o => new { o.CustomerId, o.PlacedAt });

        builder.OwnsMany(o => o.Items, item =>
        {
            item.ToTable("OrderItems");
            item.WithOwner().HasForeignKey("OrderId");
            item.HasKey(i => i.Id);
            item.Property(i => i.Id).ValueGeneratedNever();
            item.Property(i => i.ProductName).HasMaxLength(200).IsRequired();
            item.Property(i => i.SecondHalfProductName).HasMaxLength(200);
            item.Property(i => i.SizeName).HasMaxLength(50).IsRequired();
            item.Property(i => i.Notes).HasMaxLength(OrderItem.NotesMaxLength);

            item.OwnsMany(i => i.Modifiers, modifier =>
            {
                modifier.ToTable("OrderItemModifiers");
                modifier.WithOwner().HasForeignKey("OrderItemId");
                modifier.HasKey(m => m.Id);
                modifier.Property(m => m.Id).ValueGeneratedNever();
                modifier.Property(m => m.Name).HasMaxLength(200).IsRequired();
            });
        });

        builder.OwnsMany(o => o.StatusHistory, change =>
        {
            change.ToTable("OrderStatusChanges");
            change.WithOwner().HasForeignKey("OrderId");
            change.HasKey(c => c.Id);
            change.Property(c => c.Id).ValueGeneratedNever();
            change.Property(c => c.FromStatus).HasConversion<string>().HasMaxLength(30);
            change.Property(c => c.ToStatus).HasConversion<string>().HasMaxLength(30);
            change.Property(c => c.Reason).HasMaxLength(Order.ReasonMaxLength);
        });
    }
}

internal sealed class OrderCounterConfiguration : IEntityTypeConfiguration<OrderCounter>
{
    public void Configure(EntityTypeBuilder<OrderCounter> builder)
    {
        builder.ToTable("OrderCounters");
        builder.HasKey(c => new { c.LocationId, c.BusinessDay });
    }
}
