using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Presentation.Controllers.Mvc;

[AllowAnonymous]
public class AuthController : Controller
{
    [HttpGet]
    public IActionResult Login(string? mode)
    {
        ViewData["InitialMode"] = mode == "register" ? "register" : "login";
        return View();
    }

    [HttpGet]
    public IActionResult Register() => RedirectToAction(nameof(Login), new { mode = "register" });

    [HttpGet]
    public IActionResult ResetPassword() => View();
}
