using Application.Exceptions;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;



namespace Presentation.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public abstract class ApiController : ControllerBase
    {
        protected string CurrentUserId
        {
            get
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                    throw new UnauthorizedException("User is not authenticated.");

                return userId;
            }
        }
    }
}
