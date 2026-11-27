using System.Reflection;
using System.Text;
using AmrGrandPrix.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AmrGrandPrix.API.Tests.Infrastructure;

/// <summary>
/// Builds a Mermaid <c>erDiagram</c> of the Postgres schema from the EF Core model, for
/// <c>DOCS/ERD.md</c>. Uses the Npgsql provider (no connection is opened) so table names and
/// column types match what migrations create. ASP.NET Identity's plumbing tables are left out;
/// <c>Users</c> stays because app tables reference it.
/// </summary>
public static class ErdGenerator
{
    private static readonly HashSet<string> ExcludedTables = new(StringComparer.Ordinal)
    {
        "Roles", "UserRoles", "UserClaims", "UserLogins", "RoleClaims", "UserTokens",
    };

    public static string GenerateMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Database ERD");
        sb.AppendLine();
        sb.AppendLine("<!-- Generated from the EF Core model by AmrGrandPrix.API.Tests/Infrastructure/ErdGenerator.cs. Do not edit by hand. -->");
        sb.AppendLine();
        sb.AppendLine("PostgreSQL schema as built by EF Core migrations. ASP.NET Identity's role/claim/login/token tables are omitted.");
        sb.AppendLine("Regenerate after changing the model:");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine("UPDATE_ERD=1 dotnet test --filter \"FullyQualifiedName~ErdTests\"");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("Relationship labels name the foreign-key column(s) and delete behavior.");
        sb.AppendLine();
        sb.AppendLine("```mermaid");
        sb.Append(GenerateMermaid());
        sb.AppendLine("```");
        return sb.ToString();
    }

    public static string GenerateMermaid()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=erd")
            .Options;
        using var context = new ApplicationDbContext(options);
        var model = context.GetService<IDesignTimeModel>().Model;

        var entities = model.GetEntityTypes()
            .Where(e => e.GetTableName() is { } t && !ExcludedTables.Contains(t) && !e.IsOwned())
            .OrderBy(e => e.GetTableName(), StringComparer.Ordinal)
            .ToList();
        var included = entities.ToHashSet();

        var sb = new StringBuilder();
        sb.AppendLine("erDiagram");

        foreach (var entity in entities)
        {
            var table = entity.GetTableName()!;
            var storeObject = StoreObjectIdentifier.Table(table, entity.GetSchema());
            var pk = entity.FindPrimaryKey()?.Properties.ToHashSet() ?? [];
            var fk = entity.GetForeignKeys().SelectMany(f => f.Properties).ToHashSet();
            var uk = entity.GetIndexes()
                .Where(i => i.IsUnique && i.Properties.Count == 1)
                .Select(i => i.Properties[0])
                .ToHashSet();

            sb.AppendLine($"    {table} {{");
            foreach (var p in OrderProperties(entity, pk))
            {
                var keys = new List<string>();
                if (pk.Contains(p)) keys.Add("PK");
                if (fk.Contains(p)) keys.Add("FK");
                if (uk.Contains(p)) keys.Add("UK");

                var notes = new List<string>();
                if (p.IsNullable) notes.Add("nullable");
                var clr = Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType;
                if (clr.IsEnum) notes.Add($"enum {clr.Name}");
                if (p.GetMaxLength() is { } max && p.GetColumnType()?.Contains('(') != true) notes.Add($"max {max}");

                var line = new StringBuilder($"        {MermaidType(p.GetColumnType()!)} {p.GetColumnName(storeObject)}");
                if (keys.Count > 0) line.Append(' ').Append(string.Join(", ", keys));
                if (notes.Count > 0) line.Append(" \"").Append(string.Join("; ", notes)).Append('"');
                sb.AppendLine(line.ToString());
            }
            sb.AppendLine("    }");
        }

        var relationships = entities
            .SelectMany(e => e.GetForeignKeys())
            .Where(f => included.Contains(f.PrincipalEntityType) && included.Contains(f.DeclaringEntityType))
            .Select(f =>
            {
                var principal = f.PrincipalEntityType.GetTableName()!;
                var dependent = f.DeclaringEntityType.GetTableName()!;
                var principalCard = f.IsRequired ? "||" : "|o";
                var dependentCard = f.IsUnique ? "o|" : "o{";
                var columns = string.Join(", ", f.Properties.Select(p => p.Name));
                return $"    {principal} {principalCard}--{dependentCard} {dependent} : \"{columns} ({f.DeleteBehavior})\"";
            })
            .OrderBy(l => l, StringComparer.Ordinal)
            .ToList();

        if (relationships.Count > 0) sb.AppendLine();
        foreach (var r in relationships) sb.AppendLine(r);

        return sb.ToString();
    }

    /// <summary>Primary key first, then CLR declaration order (base class first), then shadow properties by name.</summary>
    private static IEnumerable<IProperty> OrderProperties(IEntityType entity, HashSet<IProperty> pk)
    {
        var clrType = entity.ClrType;
        var depth = new Dictionary<Type, int>();
        var d = 0;
        for (var t = clrType; t != null; t = t.BaseType) depth[t] = d++;

        return entity.GetProperties()
            .OrderBy(p => pk.Contains(p) ? 0 : 1)
            .ThenBy(p => p.PropertyInfo is null ? 1 : 0)
            .ThenByDescending(p => p.PropertyInfo is { DeclaringType: { } dt } && depth.TryGetValue(dt, out var v) ? v : -1)
            .ThenBy(p => p.PropertyInfo?.MetadataToken ?? 0)
            .ThenBy(p => p.Name, StringComparer.Ordinal);
    }

    /// <summary>Mermaid attribute types can't contain spaces; shorten the common Postgres names.</summary>
    private static string MermaidType(string storeType)
    {
        var t = storeType
            .Replace("character varying", "varchar")
            .Replace("timestamp with time zone", "timestamptz")
            .Replace("timestamp without time zone", "timestamp")
            .Replace("double precision", "float8");
        return t.Replace(' ', '_');
    }
}
