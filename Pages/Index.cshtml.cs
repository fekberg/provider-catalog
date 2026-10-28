using Microsoft.AspNetCore.Mvc.RazorPages;
using ProviderCatalog.Catalog;

namespace ProviderCatalog.Pages;

public class IndexModel(SpecStore specs) : PageModel
{
    public IReadOnlyList<ProviderSummary> Providers { get; } = specs.Providers();
}
