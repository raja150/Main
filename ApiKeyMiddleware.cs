using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using TranSmart.API.Services;
using TranSmart.Core.Util;
using TranSmart.Domain.Models.Cache;

namespace TranSmart.API.Extensions
{
	public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
	{
		private readonly ICacheService _cacheService;
		private readonly ITokenService _tokenService;
		public ApiKeyAuthenticationHandler(
			IOptionsMonitor<AuthenticationSchemeOptions> options,
			ILoggerFactory logger,
			UrlEncoder encoder,
			ISystemClock clock,
			ICacheService cacheService,
			ITokenService tokenService
		) : base(options, logger, encoder, clock)
		{
			_tokenService = tokenService;
			_cacheService = cacheService;
		}
		protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
		{
			string token = Request.Headers["Authorization"];
			//When both cases failed means User token is null and server doesn't contain x-API header
			if (string.IsNullOrEmpty(token) && !Request.Headers.ContainsKey(StringUtil.APIKey))
			{
				return await Task.FromResult(AuthenticateResult.Fail("Unauthorized"));
			}
			//if user is valid then validate the JWT token
			if (!string.IsNullOrEmpty(token) && !_tokenService.IsTokenExpired(token.Split("Bearer ")[1]))
			{
				return await Task.FromResult(AuthenticateResult.Fail("Unauthorized"));
			}

			string apiKeyToValidate = Request.Headers[StringUtil.APIKey];

			// Validating x-API key
			if (!string.IsNullOrEmpty(apiKeyToValidate))
			{
				//getting user details from database with x-API key
				var user = await _cacheService.GetUserByApiKey(apiKeyToValidate);
				if (user != null)
				{
					return await Task.FromResult(AuthenticateResult.Success(ApiKeyClaims(user)));
				}
				return await Task.FromResult(AuthenticateResult.Fail("Unauthorized"));
			}
			return await Task.FromResult(AuthenticateResult.Success(DefaultClaims()));
		}
		private AuthenticationTicket ApiKeyClaims(ApiKeyCache entity)
		{
			var claims = new[]{
						new Claim("id", entity.UserId.ToString()),
						new Claim("uid", entity.Name),
						new Claim("eid", entity.EmployeeId.HasValue?entity.EmployeeId.Value.ToString():string.Empty),
						new Claim(ClaimTypes.Role, entity.RoleId.ToString()),
					};
			var identity = new ClaimsIdentity(claims, Scheme.Name);
			var principal = new ClaimsPrincipal(identity);
			var ticket = new AuthenticationTicket(principal, Scheme.Name);
			return ticket;
		}
		private AuthenticationTicket DefaultClaims()
		{
			var dIdentity = new ClaimsIdentity(null, Scheme.Name);
			var dPrincipal = new ClaimsPrincipal(dIdentity);
			var dticket = new AuthenticationTicket(dPrincipal, Scheme.Name);
			return dticket;
		}
	}
}
