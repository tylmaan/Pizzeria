using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pizzeria.Domain.Customers;
using Pizzeria.Domain.Tenancy;
using Pizzeria.Infrastructure.Identity;

namespace Pizzeria.Infrastructure.Persistence.Configurations;

internal sealed class SavedAddressConfiguration : IEntityTypeConfiguration<SavedAddress>
{
    public void Configure(EntityTypeBuilder<SavedAddress> builder)
    {
        builder.ToTable("SavedAddresses");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Label).HasMaxLength(SavedAddress.LabelMaxLength);
        builder.Property(a => a.Notes).HasMaxLength(SavedAddress.NotesMaxLength);

        builder.OwnsOne(a => a.Address, address =>
        {
            address.Property(x => x.Street).HasMaxLength(Address.StreetMaxLength).IsRequired();
            address.Property(x => x.PostalCode).HasMaxLength(Address.PostalCodeMaxLength).IsRequired();
            address.Property(x => x.City).HasMaxLength(Address.CityMaxLength).IsRequired();
        });
        builder.Navigation(a => a.Address).IsRequired();

        builder.HasOne<Company>().WithMany().HasForeignKey(a => a.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(a => a.CustomerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(a => a.CompanyId);
        builder.HasIndex(a => a.CustomerId);
    }
}
