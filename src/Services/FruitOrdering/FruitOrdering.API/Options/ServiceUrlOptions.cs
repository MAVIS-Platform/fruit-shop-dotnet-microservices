using System.ComponentModel.DataAnnotations;

namespace FruitOrdering.API.Options;

public sealed class ServiceUrlOptions
{
    public const string SectionName = "ServiceUrls";

    [Required]
    [Url]
    public string CatalogApi { get; init; } = string.Empty;

    [Required]
    [Url]
    public string BasketApi { get; init; } = string.Empty;
}
