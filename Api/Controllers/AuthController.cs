using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PromoImmo.Domain;
using PromoImmo.Infrastructure;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace PromoImmo.Api.Controllers;

public class LoginDto { [Required] public string UserName { get; set; } = ""; [Required] public string Password { get; set; } = ""; }
public class UserCreateDto
{
    [Required] public string UserName { get; set; } = "";
    [Required] public string NomComplet { get; set; } = "";
    [Required] public string Password { get; set; } = "";
    [Required] public string Role { get; set; } = "";
}
public class ChangePasswordDto { [Required] public string Ancien { get; set; } = ""; [Required] public string Nouveau { get; set; } = ""; }
public class ResetPasswordDto { [Required] public string Nouveau { get; set; } = ""; }

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<AppUser> _users;
    private readonly IConfiguration _cfg;
    private readonly AppDbContext _db;
    public AuthController(UserManager<AppUser> users, IConfiguration cfg, AppDbContext db) { _users = users; _cfg = cfg; _db = db; }

    private async Task Audit(string? userId, string userName, string action, string details)
    {
        _db.AuditLogs.Add(new AuditLog { UserId = userId, UserName = userName, Action = action, Entite = "Authentification",
            Details = details, Ip = HttpContext.Connection.RemoteIpAddress?.ToString() });
        await _db.SaveChangesAsync();
    }

    private static string Erreurs(IdentityResult r) => string.Join(" ", r.Errors.Select(e => e.Description));

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var user = await _users.FindByNameAsync(dto.UserName);
        if (user is null)
        {
            await Audit(null, dto.UserName, "Connexion échouée", "Utilisateur inconnu");
            return Unauthorized(new { message = "Identifiants incorrects." });
        }
        if (await _users.IsLockedOutAsync(user))
        {
            await Audit(user.Id, user.UserName!, "Connexion refusée", "Compte verrouillé ou désactivé");
            return StatusCode(423, new { message = "Compte verrouillé (trop d'échecs) ou désactivé. Réessayez plus tard ou contactez le gérant." });
        }
        if (!await _users.CheckPasswordAsync(user, dto.Password))
        {
            await _users.AccessFailedAsync(user);
            var verrouille = await _users.IsLockedOutAsync(user);
            await Audit(user.Id, user.UserName!, "Connexion échouée", verrouille ? "Mot de passe incorrect — compte verrouillé" : "Mot de passe incorrect");
            return Unauthorized(new { message = verrouille ? "Trop d'échecs : compte verrouillé pendant 15 minutes." : "Identifiants incorrects." });
        }
        await _users.ResetAccessFailedCountAsync(user);

        var roles = await _users.GetRolesAsync(user);
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, user.Id), new(ClaimTypes.Name, user.UserName!) };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_cfg["Jwt:Key"]!));
        var jwt = new JwtSecurityToken(claims: claims, expires: DateTime.UtcNow.AddHours(10),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        await Audit(user.Id, user.UserName!, "Connexion", "Connexion réussie");
        return Ok(new { token = new JwtSecurityTokenHandler().WriteToken(jwt), nom = user.NomComplet, roles });
    }

    [HttpPost("change-password"), Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
    {
        var user = await _users.FindByIdAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (user is null) return Unauthorized();
        var res = await _users.ChangePasswordAsync(user, dto.Ancien, dto.Nouveau);
        if (!res.Succeeded) return BadRequest(new { message = Erreurs(res) });
        await Audit(user.Id, user.UserName!, "Changement de mot de passe", "Par l'utilisateur");
        return NoContent();
    }

    [HttpGet("users"), Authorize(Roles = "Gerant")]
    public async Task<IActionResult> Users()
    {
        var list = await _users.Users.AsNoTracking().OrderBy(u => u.UserName).ToListAsync();
        var res = new List<object>();
        foreach (var u in list)
        {
            var roles = await _users.GetRolesAsync(u);
            var lock_ = u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow;
            var desactive = u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow.AddYears(50);
            res.Add(new { u.Id, u.UserName, u.NomComplet, Roles = roles, Verrouille = lock_ && !desactive, Desactive = desactive });
        }
        return Ok(res);
    }

    [HttpPost("users"), Authorize(Roles = "Gerant")]
    public async Task<IActionResult> CreateUser(UserCreateDto dto)
    {
        if (!DbSeeder.Roles.Contains(dto.Role)) return BadRequest(new { message = "Rôle invalide." });
        var u = new AppUser { UserName = dto.UserName, NomComplet = dto.NomComplet, EmailConfirmed = true };
        var res = await _users.CreateAsync(u, dto.Password);
        if (!res.Succeeded) return BadRequest(new { message = Erreurs(res) });
        await _users.AddToRoleAsync(u, dto.Role);
        await Audit(User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name ?? "?", "Création de compte", $"{dto.UserName} ({dto.Role})");
        return Ok(new { u.Id });
    }

    [HttpPost("users/{id}/reset-password"), Authorize(Roles = "Gerant")]
    public async Task<IActionResult> ResetPassword(string id, ResetPasswordDto dto)
    {
        var u = await _users.FindByIdAsync(id);
        if (u is null) return NotFound();
        var token = await _users.GeneratePasswordResetTokenAsync(u);
        var res = await _users.ResetPasswordAsync(u, token, dto.Nouveau);
        if (!res.Succeeded) return BadRequest(new { message = Erreurs(res) });
        await Audit(User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name ?? "?", "Réinitialisation de mot de passe", u.UserName!);
        return NoContent();
    }

    [HttpPost("users/{id}/deverrouiller"), Authorize(Roles = "Gerant")]
    public async Task<IActionResult> Unlock(string id)
    {
        var u = await _users.FindByIdAsync(id);
        if (u is null) return NotFound();
        await _users.SetLockoutEndDateAsync(u, null);
        await _users.ResetAccessFailedCountAsync(u);
        await Audit(User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name ?? "?", "Compte activé / déverrouillé", u.UserName!);
        return NoContent();
    }

    [HttpPost("users/{id}/desactiver"), Authorize(Roles = "Gerant")]
    public async Task<IActionResult> Disable(string id)
    {
        if (id == User.FindFirstValue(ClaimTypes.NameIdentifier)) return BadRequest(new { message = "Vous ne pouvez pas désactiver votre propre compte." });
        var u = await _users.FindByIdAsync(id);
        if (u is null) return NotFound();
        await _users.SetLockoutEnabledAsync(u, true);
        await _users.SetLockoutEndDateAsync(u, DateTimeOffset.UtcNow.AddYears(100));
        await Audit(User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name ?? "?", "Compte désactivé", u.UserName!);
        return NoContent();
    }
}
