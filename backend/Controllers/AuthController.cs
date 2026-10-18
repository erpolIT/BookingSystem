using BookingSystem.Contracts;
using BookingSystem.Models;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BookingSystem.Controllers;

[ApiController]
[Route("api/auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;

    public AuthController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn)
    {
        _users = users;
        _signIn = signIn;
    }

    [HttpGet("csrf")]
    public IActionResult GetCsrfToken([FromServices] IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new { token = tokens.RequestToken });
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var user = await _users.GetUserAsync(User);
        return Ok(new { user = user is null ? null : new { id = user.Id, email = user.Email, isAdministrator = User.IsInRole("Administrator") } });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(AuthRequest request)
    {
        var email = request.Email.Trim();
        var user = new ApplicationUser { UserName = email, Email = email };
        var result = await _users.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(error.Code.Contains("Password") ? nameof(request.Password) : nameof(request.Email),
                    error.Code.Contains("Password")
                        ? "Password must contain at least 10 characters, an uppercase letter, a lowercase letter, a number and a special character."
                        : "Unable to create an account with the supplied details.");
            return ValidationProblem(ModelState);
        }
        // Registration does not sign in automatically; a lost response can be resolved by logging in.
        return StatusCode(StatusCodes.Status201Created, new { message = "Account created. You can now sign in." });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(AuthRequest request)
    {
        var result = await _signIn.PasswordSignInAsync(request.Email.Trim(), request.Password,
            isPersistent: false, lockoutOnFailure: true);
        if (!result.Succeeded)
            return Unauthorized(new { message = "Invalid email or password, or the account is temporarily locked." });
        var user = await _users.FindByEmailAsync(request.Email.Trim());
        return Ok(new { user = new { id = user!.Id, email = user.Email, isAdministrator = await _users.IsInRoleAsync(user, "Administrator") } });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return NoContent();
    }
}
