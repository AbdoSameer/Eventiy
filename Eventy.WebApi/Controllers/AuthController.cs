using Application.Features.Authentication.Commands.Login;
using Application.Features.Authentication.Commands.Register;
using Application.Features.Authentication.Commands.RefreshToken;
using Application.Features.Authentication.Commands.ApproveOrganizer;
using Application.Features.Authentication.Responses;
using Infrastructure.Authentication;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Eventy.WebApi.Extensions;

namespace Eventy.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public sealed class AuthController(
        ISender sender,
        IOptions<JwtSettings> jwtOptions) : ControllerBase
    {
        private readonly JwtSettings _jwtSettings = jwtOptions.Value;

        [HttpPost("register")]
        [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Register(
            [FromBody] RegisterUserCommand command,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(command, cancellationToken);
            if (result.IsSuccess && result.Value?.RefreshToken is not null)
            {
                AppendRefreshTokenCookie(result.Value.RefreshToken);
            }
            return result.ToActionResult();
        }

        [HttpPost("login")]
        [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Login(
            [FromBody] LoginCommand command,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(command, cancellationToken);
            if (result.IsSuccess && result.Value?.RefreshToken is not null)
            {
                AppendRefreshTokenCookie(result.Value.RefreshToken);
            }
            return result.ToActionResult();
        }

        [HttpPost("refresh")]
        [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
        {
            var refreshToken = Request.Cookies["refreshToken"];
            if (string.IsNullOrEmpty(refreshToken))
                return Unauthorized();

            var result = await sender.Send(new RefreshTokenCommand(refreshToken), cancellationToken);
            if (result.IsSuccess && result.Value?.RefreshToken is not null)
            {
                AppendRefreshTokenCookie(result.Value.RefreshToken);
            }
            return result.ToActionResult();
        }

        [Authorize]
        [HttpPost("revoke")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Revoke(CancellationToken cancellationToken)
        {
            var refreshToken = Request.Cookies["refreshToken"];
            if (string.IsNullOrEmpty(refreshToken))
                return Ok();

            Response.Cookies.Delete("refreshToken", new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict
            });

            await sender.Send(new RefreshTokenCommand(refreshToken), cancellationToken);
            return Ok();
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("organizers/{userId:guid}/approve")]
        public async Task<IActionResult> ApproveOrganizer(
            Guid userId,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(new ApproveOrganizerCommand(userId), cancellationToken);
            return result.ToActionResult();
        }

        private void AppendRefreshTokenCookie(string refreshToken)
        {
            Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays)
            });
        }
    }

}
