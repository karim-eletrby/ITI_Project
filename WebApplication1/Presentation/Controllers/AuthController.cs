using Application.DTOs.Auth;
using Application.Exceptions;
using Application.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace Presentation.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ApiController
    {
        private readonly IAuthService _authService;
        private readonly IFileStorageService _fileStorage;

        public AuthController(IAuthService authService, IFileStorageService fileStorage)
        {
            _authService = authService;
            _fileStorage = fileStorage;
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto dto, CancellationToken ct)
        {
            var result = await _authService.RegisterAsync(dto, ct);
            return Ok(result.ToSuccessResponse());
        }

        [HttpPost("verify-email")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailOtpDto dto, CancellationToken ct)
        {
            var result = await _authService.VerifyEmailAsync(dto, ct);
            return Ok(result.ToSuccessResponse());
        }

        [HttpPost("resend-verification")]
        [AllowAnonymous]
        public async Task<IActionResult> ResendVerification([FromBody] ResendEmailOtpDto dto, CancellationToken ct)
        {
            var result = await _authService.ResendVerificationOtpAsync(dto, ct);
            return Ok(result.ToSuccessResponse());
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto dto, CancellationToken ct)
        {
            var result = await _authService.LoginAsync(dto, ct);
            return Ok(result.ToSuccessResponse());
        }

        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto dto, CancellationToken ct)
        {
            var result = await _authService.ForgotPasswordAsync(dto, ct);
            return Ok(result.ToSuccessResponse());
        }

        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto dto, CancellationToken ct)
        {
            var result = await _authService.ResetPasswordAsync(dto, ct);
            return Ok(result.ToSuccessResponse());
        }

        [HttpPost("refresh-token")]
        [AllowAnonymous]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto dto, CancellationToken ct)
        {
            var result = await _authService.RefreshTokenAsync(dto, ct);
            return Ok(result.ToSuccessResponse());
        }

        [HttpPost("revoke-token")]
        [Authorize]
        public async Task<IActionResult> RevokeToken([FromBody] RevokeTokenRequestDto dto, CancellationToken ct)
        {
            var result = await _authService.RevokeTokenAsync(dto, ct);
            return Ok(result.ToSuccessResponse());
        }

        /// <summary>
        /// Exchanges a valid same-origin bearer-authenticated request for an HttpOnly
        /// MVC cookie. This lets Razor page navigations authenticate without exposing
        /// the JWT in a URL or requiring JavaScript to add headers to every link.
        /// </summary>
        [HttpPost("mvc-session")]
        [Authorize]
        public async Task<IActionResult> CreateMvcSession(CancellationToken ct)
        {
            var identity = new System.Security.Claims.ClaimsIdentity(User.Claims, "MvcCookie");
            var principal = new System.Security.Claims.ClaimsPrincipal(identity);

            await HttpContext.SignInAsync("MvcCookie", principal, new AuthenticationProperties
            {
                IsPersistent = false,
                AllowRefresh = true
            });

            return NoContent();
        }

        [HttpPost("mvc-signout")]
        [AllowAnonymous]
        public async Task<IActionResult> SignOutMvcSession()
        {
            await HttpContext.SignOutAsync("MvcCookie");
            return NoContent();
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUserProfile(CancellationToken ct)
        {
            var result = await _authService.GetProfileAsync(CurrentUserId, ct);
            return Ok(result.ToSuccessResponse());
        }

        [HttpGet("profile/{userId}")]
        [Authorize]
        public async Task<IActionResult> GetUserProfile(string userId, CancellationToken ct)
        {
            var result = await _authService.GetProfileAsync(userId, ct);
            return Ok(result.ToSuccessResponse());
        }

        [HttpPut("profile")]
        [Authorize]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto, CancellationToken ct)
        {
            var result = await _authService.UpdateProfileAsync(CurrentUserId, dto, ct);
            return Ok(result.ToSuccessResponse());
        }

        [HttpPost("change-email/request")]
        [Authorize]
        public async Task<IActionResult> RequestChangeEmail([FromBody] RequestChangeEmailDto dto, CancellationToken ct)
        {
            var result = await _authService.RequestChangeEmailAsync(CurrentUserId, dto, ct);
            return Ok(result.ToSuccessResponse());
        }

        [HttpPost("change-email/confirm")]
        [Authorize]
        public async Task<IActionResult> ConfirmChangeEmail([FromBody] VerifyChangeEmailDto dto, CancellationToken ct)
        {
            var result = await _authService.ConfirmChangeEmailAsync(CurrentUserId, dto, ct);
            return Ok(result.ToSuccessResponse());
        }

        [HttpPost("profile-picture")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme + ",MvcCookie")]
        [RequestSizeLimit(6_000_000)]
        public async Task<IActionResult> UploadProfilePicture([FromForm] IFormFile file, CancellationToken ct)
        {
            if (file is null || file.Length == 0)
                return BadRequest(new { message = "Choose a photo to upload." });

            try
            {
                var current = await _authService.GetProfileAsync(CurrentUserId, ct);
                await using var stream = file.OpenReadStream();
                var url = await _fileStorage.SaveAsync(stream, file.FileName, StoredMediaKind.ProfileImage, file.ContentType, ct);
                _fileStorage.DeleteByUrl(current.Data?.ProfilePictureUrl);

                var result = await _authService.UpdateProfileAsync(CurrentUserId,
                    new UpdateProfileDto(null, null, null, url, null), ct);
                return Ok(result.ToSuccessResponse());
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (AppException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message, errors = ex.Errors });
            }
        }

        [HttpPost("cover-picture")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme + ",MvcCookie")]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> UploadCoverPicture([FromForm] IFormFile file, CancellationToken ct)
        {
            if (file is null || file.Length == 0)
                return BadRequest(new { message = "Choose a cover photo to upload." });

            try
            {
                var current = await _authService.GetProfileAsync(CurrentUserId, ct);
                await using var stream = file.OpenReadStream();
                var url = await _fileStorage.SaveAsync(stream, file.FileName, StoredMediaKind.CoverImage, file.ContentType, ct);
                _fileStorage.DeleteByUrl(current.Data?.CoverPictureUrl);

                var result = await _authService.UpdateProfileAsync(CurrentUserId,
                    new UpdateProfileDto(null, null, null, null, url), ct);
                return Ok(result.ToSuccessResponse());
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (AppException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message, errors = ex.Errors });
            }
        }
    }
}
