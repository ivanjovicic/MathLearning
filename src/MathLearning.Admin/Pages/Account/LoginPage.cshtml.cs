using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MathLearning.Admin.Pages.Account;

public class LoginPageModel : PageModel
{
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly ILogger<LoginPageModel> _logger;

    public LoginPageModel(SignInManager<IdentityUser> signInManager, ILogger<LoginPageModel> logger)
    {
        _signInManager = signInManager;
        _logger = logger;
    }

    [BindProperty]
    public string Username { get; set; } = "";

    [BindProperty]
    public string Password { get; set; } = "";

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
        _logger.LogInformation("LoginPage GET - ReturnUrl: {ReturnUrl}", ReturnUrl);
    }

    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OnPostAsync()
    {
        _logger.LogInformation("LoginPage POST - Username: {Username}, ReturnUrl: {ReturnUrl}", Username, ReturnUrl);

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("ModelState is invalid");
            return Page();
        }

        try
        {
            _logger.LogInformation("Attempting login...");
            var result = await _signInManager.PasswordSignInAsync(
                Username, Password, isPersistent: true, lockoutOnFailure: false);

            _logger.LogInformation("Login result: Succeeded={Succeeded}", result.Succeeded);

            if (result.Succeeded)
            {
                var redirectUrl = string.IsNullOrEmpty(ReturnUrl) ? "/" : ReturnUrl;
                _logger.LogInformation("Redirecting to: {RedirectUrl}", redirectUrl);
                return Redirect(redirectUrl);
            }
            else
            {
                _logger.LogWarning("Login failed for user: {Username}", Username);
                ErrorMessage = "Neispravno korisni?ko ime ili lozinka";
                return Page();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login error for user: {Username}", Username);
            ErrorMessage = $"Greška: {ex.Message}";
            return Page();
        }
    }
}
