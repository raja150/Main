
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using TranSmart.Core.Result;

namespace TranSmart.API.Services
{
	public class TokenService : ITokenService
	{
		public IConfiguration _configuration { get; }
		public TokenService(IConfiguration configuration)
		{
			_configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
		}
		public string GenerateAccessToken(IEnumerable<Claim> claims, string key)
		{
			var secretKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
			var signinCredentials = new SigningCredentials(secretKey, SecurityAlgorithms.HmacSha256);

			var token = new JwtSecurityToken(_configuration["Tokens:Issuer"],
			  _configuration["Tokens:Issuer"],
			  claims,
			  expires: DateTime.Now.AddMinutes(1),
			  signingCredentials: signinCredentials);

			return new JwtSecurityTokenHandler().WriteToken(token).ToString();
		}
		public string GenerateAccessToken(IEnumerable<Claim> claims)
		{
			return GenerateAccessToken(claims, _configuration["Tokens:Key"]);
		}

		public string GenerateRefreshToken()
		{
			var randomNumber = new byte[32];
			using (var rng = RandomNumberGenerator.Create())
			{
				rng.GetBytes(randomNumber);
				return Convert.ToBase64String(randomNumber);
			}
		}


		public Result<ClaimsPrincipal> GetPrincipalFromExpiredToken(string token)
		{
			return GetPrincipalFromExpiredToken(token, _configuration["Tokens:Key"]);
		}
		public Result<ClaimsPrincipal> GetPrincipalFromExpiredToken(string token, string key)
		{
			Result<ClaimsPrincipal> result = new Result<ClaimsPrincipal>();
			var tokenValidationParameters = new TokenValidationParameters
			{
				ValidateAudience = false, //you might want to validate the audience and issuer depending on your use case
				ValidateIssuer = false,
				ValidateIssuerSigningKey = true,
				IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
				ValidateLifetime = false //here we are saying that we don't care about the token's expiration date
			};

			var tokenHandler = new JwtSecurityTokenHandler();
			SecurityToken securityToken;
			try
			{
				var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out securityToken);
				var jwtSecurityToken = securityToken as JwtSecurityToken;
				if (jwtSecurityToken == null || !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
				{
					result.AddMessageItem(new MessageItem(ErrMessages.Invalid_Access_Token));
				}
				else
				{
					result.ReturnValue = principal;
				}
			}
			catch { result.AddMessageItem(new MessageItem(ErrMessages.Invalid_Access_Token)); }

			return result;
		}
		public bool IsTokenExpired(string token ,string key)
		{
			var tokenValidationParameters = new TokenValidationParameters
			{
				ClockSkew = TimeSpan.Zero,
				ValidateAudience = true,
				ValidateIssuer = true,
				ValidateIssuerSigningKey = true,
				IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
				ValidIssuer = _configuration["Tokens:Issuer"],
				ValidAudience = _configuration["Tokens:Issuer"],
				ValidateLifetime = true
			};

			var tokenHandler = new JwtSecurityTokenHandler();
			try
			{
				_ = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);
				return securityToken is JwtSecurityToken jwtSecurityToken &&
					jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.OrdinalIgnoreCase);
			}
			catch { return false; }
		}
		public bool IsTokenExpired(string token)
		{
			var tokenValidationParameters = new TokenValidationParameters
			{
				ClockSkew = TimeSpan.Zero,
				ValidateAudience = true,
				ValidateIssuer = true,
				ValidateIssuerSigningKey = true,
				IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Tokens:Key"])),
				ValidIssuer = _configuration["Tokens:Issuer"],
				ValidAudience = _configuration["Tokens:Issuer"],
				ValidateLifetime = true
			};

			var tokenHandler = new JwtSecurityTokenHandler();
			try
			{
				_ = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);
				return securityToken is JwtSecurityToken jwtSecurityToken &&
					jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.OrdinalIgnoreCase);
			}
			catch { return false; }
		}
	}
}
