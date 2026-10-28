using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProviderCatalog.Catalog;

namespace ProviderCatalog.Pages;

public class SpecModel(SpecStore specs, FeedbackLog feedback) : PageModel
{
    public ProviderSpec Spec { get; private set; } = null!;

    public int? Current { get; private set; }

    public IReadOnlyList<Feedback> Feedback { get; private set; } = [];

    public IActionResult OnGet(string id, int version)
    {
        if (specs.Get(id, version) is not { } spec)
        {
            return NotFound();
        }

        Spec = spec;
        Current = specs.Providers()
            .First(p => p.Id == spec.ProviderId).Versions
            .Where(v => v.Status == "current")
            .Select(v => (int?)v.Version)
            .FirstOrDefault();
        Feedback = feedback.For(spec.ProviderId, spec.Version);

        return Page();
    }
}
