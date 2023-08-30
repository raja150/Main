using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Reflection;
using System.Text;
using Transmart.TS4API;
using TranSmart.API;
using TranSmart.API.Domain.Models;
using TranSmart.API.Extensions;
using TranSmart.API.Extensions.DI;
using TranSmart.API.Filter;
using TranSmart.API.Services;
using TranSmart.Core;
using TranSmart.Core.Result;
using TranSmart.Core.Util;
using TranSmart.Data;
using TranSmart.Data.DependencyInjection;
using TranSmart.Data.Repository.HelpDesk;
using TranSmart.Data.Repository.Leave;
using TranSmart.Service;

WebApplicationBuilder builder = WebApplication.CreateBuilder();

#region Logging

var configuration = new ConfigurationBuilder()
			 // Read from your appsettings.json.
			 .AddJsonFile("appsettings.json")
			 // Read from your secrets.
			 .AddUserSecrets<Program>(optional: true)
			 .AddEnvironmentVariables()
			 .Build();

Log.Logger = new LoggerConfiguration()
	.ReadFrom.Configuration(configuration)
	.CreateLogger();
builder.Host.UseSerilog();

#endregion

#region CS Validation
//builder.Services.ValidateConnectionStrings().ValidateOnStart();

#endregion

#region Services

builder.Services.AddCors();

#pragma warning disable CS0618 // Type or member is obsolete
builder.Services.AddControllers()
	.AddNewtonsoftJson(x => x.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore)
	.AddFluentValidation(configurationExpression: fv => fv.RegisterValidatorsFromAssemblyContaining<RolePrivilegeModel>());
#pragma warning restore CS0618 // Type or member is obsolete

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<SetBranchDataFilter>();

builder.Services.AddMvc().
	ConfigureApiBehaviorOptions(o =>
	{
		o.InvalidModelStateResponseFactory = actionContext =>
		{
			var error = new BaseResult
			{
				Messages = actionContext.ModelState
				.Where(e => e.Value.Errors.Count > 0)
				.Select(e => new MessageItem
				(
					field: e.Key,
					description: e.Value.Errors.First().ErrorMessage
				)).ToList()
			};

			return new BadRequestObjectResult(error);
		};

	});

//This registers the service layer: I only register the classes who name ends with "Service" (at the moment)
builder.Services.RegisterAssemblyPublicNonGenericClasses(Assembly.GetAssembly(typeof(IBaseService<>)))
	.Where(c => c.Name.EndsWith("Service")).AsPublicImplementedInterfaces(ServiceLifetime.Scoped);

_ = builder.Services.RegisterAssemblyPublicNonGenericClasses(Assembly.GetAssembly(typeof(ILeaveBalanceRepository)))
  .AsPublicImplementedInterfaces(ServiceLifetime.Scoped);
_ = builder.Services.AddScoped<ITicketRepository, TicketRepository>();
_ = builder.Services.AddScoped<IApplicationUser, ApplicationUser>();
_ = builder.Services.AddScoped<ITokenService, TokenService>();
_ = builder.Services.AddScoped<ISsoService, SsoService>();
_ = builder.Services.AddTransient<ICacheService, CacheService>();
_ = builder.Services.AddTransient<ISearchService, SearchService>();

_ = builder.Services.AddAutoMapper(typeof(TranSmart.MappingProfile));

builder.Services.RefitConfig(builder.Configuration["TS4API"]);
builder.Services.RegisterManagedServices();
builder.Services.AddScoped<LoginEmployeeActionFilter>();
builder.Services.AddScoped<IFileServerService, FileServerService>();

#endregion

#region Login Setup

builder.Services.AddScoped<TranSmart.API.Services.IAuthenticationService, LocalAuthenticationService>();

#endregion


#region Auth Key

// Default is Cookie, for API is JWT
builder.Services.AddAuthentication(options =>
{
	options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
		StringUtil.APIKey, options => { })
.AddJwtBearer(cfg =>
{
	cfg.RequireHttpsMetadata = false;
	cfg.SaveToken = true;


	cfg.TokenValidationParameters = new TokenValidationParameters()
	{
		ClockSkew = TimeSpan.Zero,
		RequireExpirationTime = true,
		ValidateLifetime = true,
		ValidateIssuer = false,
		ValidIssuer = builder.Configuration["Tokens:Issuer"],
		ValidAudience = builder.Configuration["Tokens:Issuer"],
		IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Tokens:Key"])),
		RequireSignedTokens = true,
	};

});

builder.Services.AddAuthorization(options =>
{
	var defaultAuthorizationPolicyBuilder = new AuthorizationPolicyBuilder(
		JwtBearerDefaults.AuthenticationScheme);

	defaultAuthorizationPolicyBuilder =
		defaultAuthorizationPolicyBuilder.RequireAuthenticatedUser();

	options.DefaultPolicy = defaultAuthorizationPolicyBuilder.Build();
});

#endregion

#region DbConnection

string dbConnString = builder.Configuration.GetConnectionString("TS4DB");

builder.Services.AddDbContext<TranSmartContext>(
	options =>
	options.UseSqlServer(dbConnString, builder => builder.MigrationsAssembly(typeof(Program).Assembly.FullName)))
	.AddUnitOfWork<TranSmartContext>();

#endregion

#region Swagger

builder.Services.AddSwaggerGen(c =>
{
	c.SwaggerDoc("v1", new OpenApiInfo { Title = "HIRS API", Version = "v1" });
	c.CustomSchemaIds(type => type.ToString());
	c.DescribeAllParametersInCamelCase();

	// To Enable authorization using Swagger (JWT)    
	c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
	{
		Name = "Authorization",
		Type = SecuritySchemeType.ApiKey,
		Scheme = "Bearer",
		BearerFormat = "JWT",
		In = ParameterLocation.Header,
		Description = "Enter 'Bearer' [space] and then your valid token in the text input below.\r\n\r\nExample: \"Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9\"",
	});

	c.AddSecurityRequirement(new OpenApiSecurityRequirement
				{
					{
						  new OpenApiSecurityScheme
							{
								Reference = new OpenApiReference
								{
									Type = ReferenceType.SecurityScheme,
									Id = "Bearer"
								}
							},
							Array.Empty<string>()

					}
				});
});
#endregion

#region Application start

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
	app.UseDeveloperExceptionPage();
}
#region Master Data

using (var scope = app.Services.CreateScope())
{
	var services = scope.ServiceProvider;
	try
	{
		var context = services.GetRequiredService<TranSmartContext>();
		await TranSmart.Data.SeedData.TranSmartContextSeed.SeedAsync(context);
		await TranSmart.Data.SeedData.TranSmartContextData.SeedAsync(context);
	}
	catch (Exception ex)
	{
		throw new InvalidOperationException(ex.Message);
	}
}

#endregion

// global CORS policy
app.UseCors(x => x
.AllowAnyOrigin()
.AllowAnyMethod()
.AllowAnyHeader());
_ = app.UseApiExceptionHandling();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
	endpoints.MapControllers();
});
app.UseAuthentication();

app.UseDefaultFiles();

app.UseStaticFiles(new StaticFileOptions()
{
	FileProvider = new PhysicalFileProvider(
	 Path.Combine(app.Environment.ContentRootPath, "images")),
	RequestPath = "/images"
});

app.UseStaticFiles(new StaticFileOptions()
{
	FileProvider = new PhysicalFileProvider(
	Path.Combine(app.Environment.ContentRootPath, "images")),
	RequestPath = "/API/images"
});

app.UseSwagger();
app.UseSwaggerUI(c =>
{
	c.SwaggerEndpoint("/swagger/v1/swagger.json", "HRIS API V1");
});
try
{
	app.Run();
}
catch (Exception ex)
{
	throw new Exception(ex.Message);
}


#endregion

