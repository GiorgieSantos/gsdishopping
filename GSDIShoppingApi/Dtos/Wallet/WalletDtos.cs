namespace GSDIShoppingApi.Dtos.Wallet;

public record WalletBalanceResponse(int PointsBalance);

/// <summary>
/// Uma linha do extrato — pode ser uma compra (PointTransaction) ou um
/// ajuste manual feito por um admin (PointsAdjustment). "Type" diferencia
/// os dois ("Compra" ou "Ajuste"); os campos que só fazem sentido para um
/// dos tipos ficam nulos no outro (ex.: StoreName só existe em compras,
/// Reason só existe em ajustes).
/// </summary>
public record WalletEntryResponse(
    int Id,
    string Type,
    int? StoreId,
    string? StoreName,
    decimal? TotalValue,
    int PointsDelta,
    string Status,
    string? Reason,
    DateTime CreatedAt
);
