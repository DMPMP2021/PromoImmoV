using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PromoImmo.Api.Hubs;
using PromoImmo.Api.Services;
using PromoImmo.Domain;
using PromoImmo.Infrastructure;
using System.Text;
using System.Text.Json.Serialization;

// Npgsql 6 : accepte les DateTime locaux / sans Kind (DateTime.Now, dates venant d'Angular)
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("QUITTANCEAppCon"),
        npg => npg.MigrationsHistoryTable("__EFMigrationsHistory", "immo")));

builder.Services.AddIdentityCore<AppUser>(o =>
    {
        o.Password.RequiredLength = 8;
        o.Lockout.AllowedForNewUsers = true;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();

var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            IssuerSigningKey = key,
            ValidateIssuerSigningKey = true
        };
        // SignalR envoie le JWT en query string
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddScoped<BrandingService>();
builder.Services.AddAuthorization();
builder.Services.AddSignalR();
builder.Services.AddScoped<Notifier>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<RentabiliteService>();
builder.Services.AddScoped<ImpayesService>();
builder.Services.AddScoped<RappelsService>();
builder.Services.AddHostedService<BackupService>();
builder.Services.AddHostedService<AlertesService>();
builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});
builder.Services.AddCors(o => o.AddPolicy("ng", p =>
    p.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    await DbSeeder.SeedAsync(scope.ServiceProvider);

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("ng");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");
app.Run();
