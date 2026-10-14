using System.ComponentModel.DataAnnotations;

namespace Claims.Infrastructure.Persistence;

public sealed class MongoOptions
{
    public const string SectionName = "MongoDb";

    public const string ConnectionStringName = "ClaimsDatabase";

    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    [Required]
    public string DatabaseName { get; set; } = "Claims";
}
