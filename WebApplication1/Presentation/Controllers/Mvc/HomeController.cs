using Microsoft.AspNetCore.Mvc;

namespace Presentation.Controllers.Mvc;

public class HomeController : Controller
{
    public IActionResult Index() => RedirectToAction("Index", "Feed");

    public IActionResult Error()
    {
        ViewData["Title"] = "Something went wrong";
        return View("~/Views/Shared/Error.cshtml");
    }
}
