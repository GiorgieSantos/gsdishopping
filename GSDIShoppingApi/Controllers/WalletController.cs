using GSDIShoppingApi.Data;
using GSDIShoppingApi.Dtos.Wallet;
using GSDIShoppingApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GSDIShoppingApi.Controllers;

[ApiController]
[Route("api/wallet")]
[Authorize]
public class WalletController(GSDIShoppingDbContext db, IPointsService pointsService) : ControllerBase
{
    [HttpGet("balance")]
    public async Task<ActionResult<WalletBalanceResponse>> GetBalance(CancellationToken ct)
    {
        var balance = await pointsService.GetBalanceAsync(User.GetUserId(), ct);
        return Ok(new WalletBalanceResponse(balance));
    }

    /// <summary>
    /// Extrato unificado: compras (PointTransactions) e ajustes manuais de
    /// admin (PointsAdjustments), ordenados do mais recente pro mais
    /// antigo. "Type" em cada linha diz qual é qual (ver WalletDtos.cs).
    /// </summary>
    [HttpGet("transactions")]
    public async Task<ActionResult<List<WalletEntryResponse>>> GetTransactions(CancellationToken ct)
    {
        var userId = User.GetUserId();

        var compras = await db.PointTransactions
            .Where(t => t.UserId == userId)
            .Select(t => new WalletEntryResponse(
                t.Id, "Compra", t.StoreId, t.Store!.Name, t.TotalValue, t.PointsEarned, t.Status.ToString(), null, t.CreatedAt))
            .ToListAsync(ct);

        var ajustes = await db.PointsAdjustments
            .Where(a => a.UserId == userId)
            .Select(a => new WalletEntryResponse(
                a.Id, "Ajuste", null, null, null, a.PointsDelta, "Approved", a.Reason, a.CreatedAt))
            .ToListAsync(ct);

        var extrato = compras.Concat(ajustes)
            .OrderByDescending(e => e.CreatedAt)
            .ToList();

        return Ok(extrato);
    }
}
