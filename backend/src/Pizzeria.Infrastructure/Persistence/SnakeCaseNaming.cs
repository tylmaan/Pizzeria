using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Pizzeria.Infrastructure.Persistence;

/// <summary>
/// Maps tables, columns, keys and indexes to snake_case, so reports can be written as plain SQL
/// without quoting every identifier.
/// </summary>
internal static class SnakeCaseNaming
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        var entityTypes = modelBuilder.Model.GetEntityTypes().ToList();

        // Tables first: default key/index names are derived from table names.
        foreach (var entity in entityTypes.Where(e => !e.IsOwned()))
        {
            if (entity.GetTableName() is { } table)
            {
                entity.SetTableName(ToSnakeCase(table));
            }
        }

        foreach (var entity in entityTypes)
        {
            // An owned type stored in its owner's table shares the owner's primary key column and key.
            var sharesOwnerTable = entity.IsOwned();

            foreach (var property in entity.GetProperties())
            {
                if (sharesOwnerTable && property.IsPrimaryKey())
                {
                    continue;
                }

                property.SetColumnName(ToSnakeCase(property.GetColumnName()));
            }

            if (sharesOwnerTable)
            {
                continue;
            }

            foreach (var key in entity.GetKeys())
            {
                if (key.GetName() is { } name)
                {
                    key.SetName(ToSnakeCase(name));
                }
            }

            foreach (var foreignKey in entity.GetForeignKeys())
            {
                if (foreignKey.GetConstraintName() is { } name)
                {
                    foreignKey.SetConstraintName(ToSnakeCase(name));
                }
            }

            foreach (var index in entity.GetIndexes())
            {
                if (index.GetDatabaseName() is { } name)
                {
                    index.SetDatabaseName(ToSnakeCase(name));
                }
            }
        }
    }

    public static string ToSnakeCase(string name)
    {
        var builder = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var current = name[i];
            if (char.IsUpper(current))
            {
                var previous = i > 0 ? name[i - 1] : '\0';
                var next = i + 1 < name.Length ? name[i + 1] : '\0';
                var startsWord = i > 0 && previous != '_' &&
                                 (char.IsLower(previous) || char.IsDigit(previous) || char.IsLower(next));
                if (startsWord)
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(current));
            }
            else
            {
                builder.Append(current);
            }
        }

        return builder.ToString();
    }
}
