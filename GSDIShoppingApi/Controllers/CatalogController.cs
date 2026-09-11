using GSDIShoppingApi.Data;
using GSDIShoppingApi.Dtos.Catalog;
using GSDIShoppingApi.Models;
using GSDIShoppingApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GSDIShoppingApi.Controllers;

/// <summary>
/// Leitura pública do catálogo: lojas, campanhas (com seus cupons) e
/// promoções de loja — sem autenticação, é o que o app do cliente consome.
/// A escrita (POST) agora exige login de admin (policy "AdminOnly" — ver
/// Program.cs): o painel Blazor cadastra direto pelo DbContext, mas estes
/// endpoints continuam aqui, protegidos, para Swagger/automação.
/// </summary>
[ApiController]
[Route("api/catalog")]
public class CatalogController(GSDIShoppingDbContext db) : ControllerBase
{
    /// <summary>
    /// Lista lojas, opcionalmente filtrando por CNPJ. É isto que o app usa
    /// para descobrir, a partir do CNPJ lido no QR code da nota, se a
    /// compra foi numa loja do shopping — e para preencher o "onde foi
    /// feita a compra" no extrato.
    /// </summary>
    [HttpGet("stores")]
    public async Task<ActionResult<List<StoreResponse>>> GetStores(
        [FromQuery] string? cnpj, CancellationToken ct)
    {
        var query = db.Stores.AsQueryable();

        if (!string.IsNullOrWhiteSpace(cnpj))
        {
            var digits = DocumentValidators.OnlyDigits(cnpj);
            query = query.Where(s => s.Cnpj == digits);
        }

        var stores = await query
            .OrderBy(s => s.Name)
            .Select(s => new StoreResponse(s.Id, s.Name, s.Cnpj, s.Floor, s.LogoUrl))
            .ToListAsync(ct);

        return Ok(stores);
    }

    [HttpPost("stores")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<StoreResponse>> CreateStore(CreateStoreRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Nome da loja é obrigatório.");
        }

        var cnpjDigits = DocumentValidators.OnlyDigits(request.Cnpj);
        if (!DocumentValidators.IsValidCnpj(cnpjDigits))
        {
            return BadRequest("Informe um CNPJ válido.");
        }

        var cnpjExists = await db.Stores.AnyAsync(s => s.Cnpj == cnpjDigits, ct);
        if (cnpjExists)
        {
            return Conflict("Já existe uma loja cadastrada com este CNPJ.");
        }

        var store = new Store
        {
            Name = request.Name.Trim(),
            Cnpj = cnpjDigits,
            Floor = string.IsNullOrWhiteSpace(request.Floor) ? null : request.Floor.Trim(),
            LogoUrl = string.IsNullOrWhiteSpace(request.LogoUrl) ? null : request.LogoUrl.Trim(),
        };

        db.Stores.Add(store);
        await db.SaveChangesAsync(ct);

        return Ok(new StoreResponse(store.Id, store.Name, store.Cnpj, store.Floor, store.LogoUrl));
    }

    // ---- Campanhas ----

    [HttpGet("campaigns")]
    public async Task<ActionResult<List<CampaignResponse>>> GetCampaigns(CancellationToken ct)
    {
        var campaigns = await db.Campaigns
            .OrderByDescending(c => c.StartsAt)
            .Select(c => new CampaignResponse(c.Id, c.Title, c.Description, c.ImageUrl, c.StartsAt, c.EndsAt))
            .ToListAsync(ct);

        return Ok(campaigns);
    }

    [HttpPost("campaigns")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<CampaignResponse>> CreateCampaign(CreateCampaignRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest("Título da campanha é obrigatório.");
        }

        var campaign = new Campaign
        {
            Title = request.Title.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim(),
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
        };

        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync(ct);

        return Ok(new CampaignResponse(campaign.Id, campaign.Title, campaign.Description, campaign.ImageUrl, campaign.StartsAt, campaign.EndsAt));
    }

    // ---- Cupons ----

    /// <summary>Lista cupons, opcionalmente só de uma campanha (é assim que a tela de campanha do app busca os cupons dela).</summary>
    [HttpGet("coupons")]
    public async Task<ActionResult<List<CouponResponse>>> GetCoupons([FromQuery] int? campaignId, CancellationToken ct)
    {
        var query = db.Coupons.Include(c => c.Campaign).AsQueryable();

        if (campaignId is not null)
        {
            query = query.Where(c => c.CampaignId == campaignId);
        }

        var coupons = await query
            .OrderBy(c => c.ExpiresAt)
            .Select(c => new CouponResponse(
                c.Id, c.Title, c.Description, c.PointsCost, c.CampaignId, c.Campaign!.Title, c.ImageUrl, c.ExpiresAt))
            .ToListAsync(ct);

        return Ok(coupons);
    }

    [HttpPost("coupons")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<CouponResponse>> CreateCoupon(CreateCouponRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest("Título do cupom é obrigatório.");
        }
        if (request.PointsCost <= 0)
        {
            return BadRequest("Custo em pontos precisa ser maior que zero.");
        }

        var campaign = await db.Campaigns.FindAsync([request.CampaignId], ct);
        if (campaign is null)
        {
            return BadRequest("Campanha informada não existe — cadastre a campanha primeiro.");
        }

        var coupon = new Coupon
        {
            Title = request.Title.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            PointsCost = request.PointsCost,
            CampaignId = request.CampaignId,
            ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim(),
            ExpiresAt = request.ExpiresAt,
        };

        db.Coupons.Add(coupon);
        await db.SaveChangesAsync(ct);

        return Ok(new CouponResponse(
            coupon.Id, coupon.Title, coupon.Description, coupon.PointsCost, coupon.CampaignId, campaign.Title, coupon.ImageUrl, coupon.ExpiresAt));
    }

    // ---- Promoções ----

    /// <summary>Lista promoções, opcionalmente só de uma loja. Sem filtro, retorna tanto promoções de loja quanto promoções gerais do shopping (StoreId nulo).</summary>
    [HttpGet("promotions")]
    public async Task<ActionResult<List<PromotionResponse>>> GetPromotions([FromQuery] int? storeId, CancellationToken ct)
    {
        var query = db.Promotions.Include(p => p.Store).AsQueryable();

        if (storeId is not null)
        {
            query = query.Where(p => p.StoreId == storeId);
        }

        var promotions = await query
            .OrderByDescending(p => p.StartsAt)
            .Select(p => new PromotionResponse(
                p.Id, p.Title, p.Description, p.DiscountLabel, p.StoreId, p.Store != null ? p.Store.Name : null,
                p.ImageUrl, p.StartsAt, p.EndsAt))
            .ToListAsync(ct);

        return Ok(promotions);
    }

    [HttpPost("promotions")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<PromotionResponse>> CreatePromotion(CreatePromotionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest("Título da promoção é obrigatório.");
        }
        if (string.IsNullOrWhiteSpace(request.DiscountLabel))
        {
            return BadRequest("Informe o texto do desconto (ex.: \"20% OFF\").");
        }

        Store? store = null;
        if (request.StoreId is not null)
        {
            store = await db.Stores.FindAsync([request.StoreId.Value], ct);
            if (store is null)
            {
                return BadRequest("Loja informada não existe. Deixe StoreId nulo para uma promoção geral do shopping.");
            }
        }

        var promotion = new Promotion
        {
            Title = request.Title.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            DiscountLabel = request.DiscountLabel.Trim(),
            StoreId = request.StoreId,
            ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim(),
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
        };

        db.Promotions.Add(promotion);
        await db.SaveChangesAsync(ct);

        return Ok(new PromotionResponse(
            promotion.Id, promotion.Title, promotion.Description, promotion.DiscountLabel, promotion.StoreId,
            store?.Name, promotion.ImageUrl, promotion.StartsAt, promotion.EndsAt));
    }
}
