using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using MongoDB.Driver;
using PartsOrderTracker.Api.Hubs;
using PartsOrderTracker.Api.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration["MongoDb:ConnectionString"];
if (string.IsNullOrWhiteSpace(connectionString))
{
  throw new InvalidOperationException(
    "MongoDb:ConnectionString is required. Configure it with .NET User Secrets for local development.");
}

var databaseName = builder.Configuration["MongoDb:DatabaseName"];
if (string.IsNullOrWhiteSpace(databaseName))
{
  throw new InvalidOperationException("MongoDb:DatabaseName is required.");
}

if (!string.Equals(databaseName, "Parts_order_tracker_dev", StringComparison.Ordinal))
{
  throw new InvalidOperationException(
    "MongoDb:DatabaseName must be exactly 'Parts_order_tracker_dev' (including capitalization).");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
  .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();

builder.Services.AddCors(options =>
{
  options.AddPolicy("UiPolicy", policy =>
  {
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    policy.WithOrigins(["https://localhost:7053", .. allowedOrigins.Where(o => !string.IsNullOrWhiteSpace(o))])
          .AllowAnyHeader()
          .AllowAnyMethod()
          .AllowCredentials();
  });
});

builder.Services.AddControllers()
  .AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddSignalR().AddAzureSignalR(
  builder.Configuration["Azure:SignalR:ConnectionString"]);
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();

builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(connectionString));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IMongoClient>().GetDatabase(databaseName));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ICurrentUserService, ClaimsCurrentUserService>();
builder.Services.AddScoped<PartsService>();
builder.Services.AddScoped<OrdersService>();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
  app.MapOpenApi();
  app.UseSwagger();
  app.UseSwaggerUI();
}

app.UseCors("UiPolicy");

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers().RequireAuthorization();
app.MapHub<OrdersHub>("/hubs/orders");

app.Run();
