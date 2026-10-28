using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProviderCatalog.Auth;

namespace ProviderCatalog.Pages;

public class SignInModel(AccessTokens tokens) : PageModel
{
    [BindProperty]
    public string? Token { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? Error { get; private set; }

    public async Task<IActionResult> OnPostAsync()
    {
        var owner = tokens.FindOwner(Token?.Trim());

        if (owner is null)
        {
            Error = "That token isn't valid.";
            return Page();
        }

        const string scheme = CookieAuthenticationDefaults.AuthenticationScheme;
        await HttpContext.SignInAsync(scheme, AccessTokens.CreateUser(owner, scheme));

        return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/");
    }
}
