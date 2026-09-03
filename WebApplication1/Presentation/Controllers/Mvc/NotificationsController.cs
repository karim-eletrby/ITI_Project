using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Presentation.Controllers.Mvc;

[Authorize(AuthenticationSchemes = "MvcCookie")]
public class NotificationsController : Controller
{
    [HttpGet]
    public IActionResult Index() => View();
}
