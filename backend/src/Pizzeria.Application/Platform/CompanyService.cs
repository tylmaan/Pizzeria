using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pizzeria.Application.Abstractions;
using Pizzeria.Application.Errors;
using Pizzeria.Application.Tenancy;
using Pizzeria.Application.Users;
using Pizzeria.Domain.Identity;
using Pizzeria.Domain.Tenancy;

namespace Pizzeria.Application.Platform;

/// <summary>Platform-level company management. PlatformAdmin only.</summary>
public sealed class CompanyService(
    IAppDbContext db,
    ITenantContext tenant,
    IUserAccountService accounts,
    TimeProvider clock,
    IValidator<CreateCompanyRequest> validator)
{
    public async Task<IReadOnlyList<CompanyDto>> ListAsync(CancellationToken ct)
    {
        RequirePlatformAdmin();

        // PlatformAdmin works across tenants by definition, so the tenant filter must not apply here.
        var companies = await db.Companies
            .IgnoreQueryFilters()
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return companies.ConvertAll(CompanyDto.From);
    }

    public async Task<CompanyDto> CreateAsync(CreateCompanyRequest request, CancellationToken ct)
    {
        RequirePlatformAdmin();
        await validator.ValidateAndThrowAsync(request, ct).ConfigureAwait(false);

        var company = Company.Create(request.Name, request.Slug, clock.GetUtcNow());

        // Slug uniqueness is global across tenants, so the check has to bypass the tenant filter.
        var slugTaken = await db.Companies
            .IgnoreQueryFilters()
            .AnyAsync(c => c.Slug == company.Slug, ct)
            .ConfigureAwait(false);
        if (slugTaken)
        {
            throw new ConflictException(ErrorCodes.CompanySlugTaken);
        }

        // Company and its first admin are created atomically: no company without an administrator.
        var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            db.Companies.Add(company);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            await accounts.CreateAsync(
                new NewUserAccount(
                    company.Id,
                    request.AdminEmail.Trim(),
                    request.AdminPassword,
                    request.AdminDisplayName.Trim(),
                    Roles.CompanyAdmin,
                    []),
                ct).ConfigureAwait(false);

            await transaction.CommitAsync(ct).ConfigureAwait(false);
        }

        return CompanyDto.From(company);
    }

    private void RequirePlatformAdmin()
    {
        if (!tenant.IsPlatformAdmin)
        {
            throw new ForbiddenException();
        }
    }
}
