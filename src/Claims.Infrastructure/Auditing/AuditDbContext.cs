using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Claims.Infrastructure.Auditing;

public sealed class ClaimAudit
{
    public int Id { get; set; }

    public required string ClaimId { get; set; }

    public DateTime Created { get; set; }

    public required string HttpRequestType { get; set; }

    public Guid? EventId { get; set; }
}

public sealed class CoverAudit
{
    public int Id { get; set; }

    public required string CoverId { get; set; }

    public DateTime Created { get; set; }

    public required string HttpRequestType { get; set; }

    public Guid? EventId { get; set; }
}

public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContext(options)
{
    public const string ConnectionStringName = "AuditDatabase";

    public DbSet<ClaimAudit> ClaimAudits => Set<ClaimAudit>();

    public DbSet<CoverAudit> CoverAudits => Set<CoverAudit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        Configure(modelBuilder.Entity<ClaimAudit>(), "ClaimAudits");
        Configure(modelBuilder.Entity<CoverAudit>(), "CoverAudits");
    }

    private static void Configure<TAudit>(EntityTypeBuilder<TAudit> builder, string table)
        where TAudit : class
    {
        builder.ToTable(table);
        builder.HasKey("Id");
        builder.HasIndex("EventId")
            .IsUnique()
            .HasFilter("[EventId] IS NOT NULL")
            .HasDatabaseName($"IX_{table}_EventId");
    }
}

public sealed class AuditDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AuditDbContext>
{
    public AuditDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseSqlServer("Server=localhost;Database=ClaimsAudit;Integrated Security=true;TrustServerCertificate=true")
            .Options;

        return new AuditDbContext(options);
    }
}
