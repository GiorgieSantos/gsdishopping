using GSDIShoppingApi.Dtos.Wallet;

namespace GSDIShoppingApi.Dtos.Admin;

public record AdminRegisterRequest(string Name, string Email, string Password);

public record AdminLoginRequest(string Email, string Password);

public record AdminAuthResponse(string Token, DateTime ExpiresAt, int AdminId, string Name, string Email);

public record AdminUserSummaryResponse(int Id, string Name, string Email, string Cpf, int PointsBalance, DateTime CreatedAt);

public record AdminUserDetailResponse(
    int Id,
    string Name,
    string Email,
    string Phone,
    string Cpf,
    DateOnly DataNascimento,
    int PointsBalance,
    List<WalletEntryResponse> Extrato
);

/// <summary>PointsDelta positivo credita, negativo debita/corrige. Reason é obrigatório e fica gravado no extrato do cliente.</summary>
public record AdjustPointsRequest(int PointsDelta, string Reason);
