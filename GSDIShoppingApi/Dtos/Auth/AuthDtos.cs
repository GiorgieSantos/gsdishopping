namespace GSDIShoppingApi.Dtos.Auth;

public record RegisterRequest(
    string Name,
    string Email,
    string Password,
    string Phone,
    string Cpf,
    string Sexo,
    DateOnly DataNascimento);

public record LoginRequest(string Email, string Password);

public record AuthResponse(
    string Token,
    DateTime ExpiresAt,
    int UserId,
    string Name,
    string Email,
    string Phone,
    string Cpf,
    string Sexo,
    DateOnly DataNascimento);
