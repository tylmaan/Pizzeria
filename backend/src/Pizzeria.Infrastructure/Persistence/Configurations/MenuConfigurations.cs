using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pizzeria.Domain.Menu;
using Pizzeria.Domain.Tenancy;

namespace Pizzeria.Infrastructure.Persistence.Configurations;

// Menu aggregates own their child collections (sizes, prices, modifiers). Children have no tenant column:
// they are only reachable through their tenant-filtered aggregate root.
// Size ids are referenced from prices without a database FK (EF cannot point at an owned entity);
// the application keeps them consistent (see MenuService: a size in use cannot be removed).

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).HasMaxLength(Category.NameMaxLength).IsRequired();
        builder.HasOne<Company>().WithMany().HasForeignKey(c => c.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => c.CompanyId);

        builder.OwnsMany(c => c.Sizes, size =>
        {
            size.ToTable("CategorySizes");
            size.WithOwner().HasForeignKey("CategoryId");
            size.HasKey(s => s.Id);
            size.Property(s => s.Id).ValueGeneratedNever();
            size.Property(s => s.Name).HasMaxLength(Category.SizeNameMaxLength).IsRequired();
        });
    }
}

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).HasMaxLength(Product.NameMaxLength).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(Product.DescriptionMaxLength);
        builder.HasOne<Company>().WithMany().HasForeignKey(p => p.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Category>().WithMany().HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => p.CompanyId);
        builder.HasIndex(p => p.CategoryId);

        // Child rows have their own client-generated keys: with a composite key (owner + size) EF treats
        // a newly added row as an existing one and issues an UPDATE instead of an INSERT.
        builder.OwnsMany(p => p.Prices, price =>
        {
            price.ToTable("ProductPrices");
            price.WithOwner().HasForeignKey("ProductId");
            price.HasKey(x => x.Id);
            price.Property(x => x.Id).ValueGeneratedNever();
            price.HasIndex("ProductId", nameof(ProductPrice.SizeId)).IsUnique();
        });

        builder.OwnsMany(p => p.ModifierGroups, link =>
        {
            link.ToTable("ProductModifierGroups");
            link.WithOwner().HasForeignKey("ProductId");
            link.HasKey(x => x.Id);
            link.Property(x => x.Id).ValueGeneratedNever();
            link.HasIndex("ProductId", nameof(ProductModifierGroup.ModifierGroupId)).IsUnique();
            link.HasOne<ModifierGroup>().WithMany().HasForeignKey(l => l.ModifierGroupId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

internal sealed class ModifierGroupConfiguration : IEntityTypeConfiguration<ModifierGroup>
{
    public void Configure(EntityTypeBuilder<ModifierGroup> builder)
    {
        builder.ToTable("ModifierGroups");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Name).HasMaxLength(ModifierGroup.NameMaxLength).IsRequired();
        builder.HasOne<Company>().WithMany().HasForeignKey(g => g.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Category>().WithMany().HasForeignKey(g => g.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(g => g.CompanyId);
        builder.HasIndex(g => g.CategoryId);

        builder.OwnsMany(g => g.Modifiers, modifier =>
        {
            modifier.ToTable("Modifiers");
            modifier.WithOwner().HasForeignKey("ModifierGroupId");
            modifier.HasKey(m => m.Id);
            modifier.Property(m => m.Id).ValueGeneratedNever();
            modifier.Property(m => m.Name).HasMaxLength(ModifierGroup.NameMaxLength).IsRequired();

            modifier.OwnsMany(m => m.Prices, price =>
            {
                price.ToTable("ModifierPrices");
                price.WithOwner().HasForeignKey("ModifierId");
                price.HasKey(x => x.Id);
                price.Property(x => x.Id).ValueGeneratedNever();
                price.HasIndex("ModifierId", nameof(ModifierPrice.SizeId)).IsUnique();
            });
        });
    }
}
