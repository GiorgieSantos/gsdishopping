namespace GSDIShoppingApi.Dtos.Catalog;

public record StoreResponse(int Id, string Name, string Cnpj, string? Floor, string? LogoUrl);

/// <summary>Cadastro de loja — exige login de admin (policy "AdminOnly").</summary>
public record CreateStoreRequest(string Name, string Cnpj, string? Floor, string? LogoUrl);

public record CampaignResponse(
    int Id,
    string Title,
    string Description,
    string? ImageUrl,
    DateTime? StartsAt,
    DateTime? EndsAt);

/// <summary>Cadastro de campanha — exige login de admin (policy "AdminOnly").</summary>
public record CreateCampaignRequest(
    string Title,
    string Description,
    string? ImageUrl,
    DateTime? StartsAt,
    DateTime? EndsAt);

public record CouponResponse(
    int Id,
    string Title,
    string Description,
    int PointsCost,
    int CampaignId,
    string CampaignTitle,
    string? ImageUrl,
    DateTime? ExpiresAt);

/// <summary>Cadastro de cupom — exige login de admin (policy "AdminOnly").</summary>
public record CreateCouponRequest(
    string Title,
    string Description,
    int PointsCost,
    int CampaignId,
    string? ImageUrl,
    DateTime? ExpiresAt);

public record PromotionResponse(
    int Id,
    string Title,
    string Description,
    string DiscountLabel,
    int? StoreId,
    string? StoreName,
    string? ImageUrl,
    DateTime? StartsAt,
    DateTime? EndsAt);

/// <summary>
/// Cadastro de promoção — exige login de admin (policy "AdminOnly").
/// StoreId nulo = promoção do shopping como um todo, não de uma loja específica.
/// </summary>
public record CreatePromotionRequest(
    string Title,
    string Description,
    string DiscountLabel,
    int? StoreId,
    string? ImageUrl,
    DateTime? StartsAt,
    DateTime? EndsAt);
