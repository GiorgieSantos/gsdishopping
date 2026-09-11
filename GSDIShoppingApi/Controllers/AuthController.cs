using GSDIShoppingApi.Data;
using GSDIShoppingApi.Dtos.Auth;
using GSDIShoppingApi.Models;
using GSDIShoppingApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GSDIShoppingApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    GSDIShoppingDbContext db,
    IJwtTokenService jwtTokenService,
    IConfiguration configuration) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        // --- Nome ---
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Nome é obrigatório.");
        }

        // --- E-mail ---
        if (!DocumentValidators.IsValidEmail(request.Email))
        {
            return BadRequest("Informe um e-mail válido.");
        }

        // --- Senha ---
        var minPasswordLength = configuration.GetValue("Auth:MinPasswordLength", 8);
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < minPasswordLength)
        {
            return BadRequest($"A senha deve ter pelo menos {minPasswordLength} caracteres.");
        }

        // --- Telefone ---
        var phoneDigits = DocumentValidators.OnlyDigits(request.Phone);
        if (!DocumentValidators.IsValidPhone(phoneDigits))
        {
            return BadRequest("Informe um telefone válido, com DDD (10 ou 11 dígitos).");
        }

        // --- CPF ---
        var cpfDigits = DocumentValidators.OnlyDigits(request.Cpf);
        if (!DocumentValidators.IsValidCpf(cpfDigits))
        {
            return BadRequest("Informe um CPF válido.");
        }

        // --- Sexo ---
        if (!DocumentValidators.SexoValoresValidos.Contains(request.Sexo?.Trim() ?? string.Empty))
        {
            return BadRequest("Sexo deve ser um de: Masculino, Feminino, Outro.");
        }

        // --- Data de nascimento ---
        if (!DocumentValidators.IsValidDataNascimento(request.DataNascimento))
        {
            return BadRequest("Informe uma data de nascimento válida.");
        }

        var emailExists = await db.Users.AnyAsync(u => u.Email == request.Email, ct);
        if (emailExists)
        {
            return Conflict("Já existe uma conta com este e-mail.");
        }

        var cpfExists = await db.Users.AnyAsync(u => u.Cpf == cpfDigits, ct);
        if (cpfExists)
        {
            return Conflict("Já existe uma conta com este CPF.");
        }

        var user = new ApplicationUser
        {
            Name = request.Name.Trim(),
            Email = request.Email.Trim(),
            Phone = phoneDigits,
            Cpf = cpfDigits,
            Sexo = request.Sexo!.Trim(),
            DataNascimento = request.DataNascimento,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        var (token, expiresAt) = jwtTokenService.GenerateToken(user);
        return Ok(ToAuthResponse(user, token, expiresAt));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == request.Email, ct);
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized("E-mail ou senha inválidos.");
        }

        var (token, expiresAt) = jwtTokenService.GenerateToken(user);
        return Ok(ToAuthResponse(user, token, expiresAt));
    }

    private static AuthResponse ToAuthResponse(ApplicationUser user, string token, DateTime expiresAt) =>
        new(
            token,
            expiresAt,
            user.Id,
            user.Name,
            user.Email,
            user.Phone,
            user.Cpf,
            user.Sexo,
            user.DataNascimento);
}
