using GSDIShoppingApi.Data;
using GSDIShoppingApi.Dtos.Admin;
using GSDIShoppingApi.Models;
using GSDIShoppingApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GSDIShoppingApi.Controllers;

/// <summary>
/// Autenticação de administrador via API (JWT) — útil pelo Swagger/curl e
/// para automação. O login do painel Blazor (/admin/login) tem seu próprio
/// fluxo baseado em cookie (ver Program.cs), mas confere as credenciais da
/// mesma tabela AdminUsers.
/// </summary>
[ApiController]
[Route("api/admin/auth")]
public class AdminAuthController(
    GSDIShoppingDbContext db,
    IJwtTokenService jwtTokenService,
    IConfiguration configuration) : ControllerBase
{
    /// <summary>
    /// Cria o primeiro administrador do sistema. Só funciona enquanto não
    /// existir nenhum AdminUser ainda — depois do primeiro, este endpoint
    /// passa a responder 403 permanentemente (ao contrário dos endpoints
    /// de catálogo, que ficam abertos só até a Fase 3; aqui o bloqueio é
    /// definitivo por design, para não deixar uma porta de "virar admin"
    /// sempre aberta). Novos administradores, depois do primeiro, seriam
    /// cadastrados já autenticado — não implementado nesta fase por não
    /// ter sido pedido; me avise se precisar de mais de um admin.
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<AdminAuthResponse>> Register(AdminRegisterRequest request, CancellationToken ct)
    {
        var anyAdminExists = await db.AdminUsers.AnyAsync(ct);
        if (anyAdminExists)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                "Já existe um administrador cadastrado. Peça para um admin já logado criar o seu acesso.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Nome é obrigatório.");
        }

        if (!DocumentValidators.IsValidEmail(request.Email))
        {
            return BadRequest("Informe um e-mail válido.");
        }

        var minPasswordLength = configuration.GetValue("Auth:MinPasswordLength", 8);
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < minPasswordLength)
        {
            return BadRequest($"A senha deve ter pelo menos {minPasswordLength} caracteres.");
        }

        var admin = new AdminUser
        {
            Name = request.Name.Trim(),
            Email = request.Email.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
        };

        db.AdminUsers.Add(admin);
        await db.SaveChangesAsync(ct);

        var (token, expiresAt) = jwtTokenService.GenerateAdminToken(admin);
        return Ok(new AdminAuthResponse(token, expiresAt, admin.Id, admin.Name, admin.Email));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AdminAuthResponse>> Login(AdminLoginRequest request, CancellationToken ct)
    {
        var admin = await db.AdminUsers.SingleOrDefaultAsync(a => a.Email == request.Email, ct);
        if (admin is null || !BCrypt.Net.BCrypt.Verify(request.Password, admin.PasswordHash))
        {
            return Unauthorized("E-mail ou senha inválidos.");
        }

        var (token, expiresAt) = jwtTokenService.GenerateAdminToken(admin);
        return Ok(new AdminAuthResponse(token, expiresAt, admin.Id, admin.Name, admin.Email));
    }
}
