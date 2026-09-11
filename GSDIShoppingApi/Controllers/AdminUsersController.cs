using GSDIShoppingApi.Data;
using GSDIShoppingApi.Dtos.Admin;
using GSDIShoppingApi.Dtos.Wallet;
using GSDIShoppingApi.Models;
using GSDIShoppingApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GSDIShoppingApi.Controllers;

/// <summary>
/// Gestão de clientes e pontos pelo admin: listar, ver extrato completo
/// (compras + ajustes) e lançar ajustes manuais de pontos. É a mesma
/// leitura/escrita que o painel Blazor usa, exposta também como API para
/// automação/Swagger. Protegido pela policy "AdminOnly" (JWT com claim de
/// role "Admin" — ver Program.cs e JwtTokenService.GenerateAdminToken).
/// </summary>
[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = "AdminOnly")]
public class AdminUsersController(GSDIShoppingDbContext db, IPointsService pointsService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AdminUserSummaryResponse>>> GetUsers(CancellationToken ct)
    {
        var users = await db.Users.OrderBy(u => u.Name).ToListAsync(ct);

        var result = new List<AdminUserSummaryResponse>(users.Count);
        foreach (var user in users)
        {
            var balance = await pointsService.GetBalanceAsync(user.Id, ct);
            result.Add(new AdminUserSummaryResponse(user.Id, user.Name, user.Email, user.Cpf, balance, user.CreatedAt));
        }

        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AdminUserDetailResponse>> GetUserDetail(int id, CancellationToken ct)
    {
        var user = await db.Users.FindAsync([id], ct);
        if (user is null)
        {
            return NotFound();
        }

        var detail = await BuildDetailAsync(user, ct);
        return Ok(detail);
    }

    /// <summary>
    /// Lança um ajuste manual de pontos (bônus, correção, estorno etc.).
    /// Nunca mexe num "saldo" diretamente — só adiciona uma linha em
    /// PointsAdjustments, que entra na soma que forma o saldo (ver
    /// PointsService.GetBalanceAsync) e aparece no extrato do cliente.
    /// </summary>
    [HttpPost("{id:int}/adjust-points")]
    public async Task<ActionResult<AdminUserDetailResponse>> AdjustPoints(
        int id, AdjustPointsRequest request, CancellationToken ct)
    {
        var user = await db.Users.FindAsync([id], ct);
        if (user is null)
        {
            return NotFound();
        }

        if (request.PointsDelta == 0)
        {
            return BadRequest("Informe um ajuste diferente de zero.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BadRequest("Informe o motivo do ajuste — ele fica registrado no extrato do cliente.");
        }

        db.PointsAdjustments.Add(new PointsAdjustment
        {
            UserId = id,
            PointsDelta = request.PointsDelta,
            Reason = request.Reason.Trim(),
            AdminUserId = User.GetAdminId(),
        });
        await db.SaveChangesAsync(ct);

        var detail = await BuildDetailAsync(user, ct);
        return Ok(detail);
    }

    private async Task<AdminUserDetailResponse> BuildDetailAsync(ApplicationUser user, CancellationToken ct)
    {
        var compras = await db.PointTransactions
            .Where(t => t.UserId == user.Id)
            .Select(t => new WalletEntryResponse(
                t.Id, "Compra", t.StoreId, t.Store!.Name, t.TotalValue, t.PointsEarned, t.Status.ToString(), null, t.CreatedAt))
            .ToListAsync(ct);

        var ajustes = await db.PointsAdjustments
            .Where(a => a.UserId == user.Id)
            .Select(a => new WalletEntryResponse(
                a.Id, "Ajuste", null, null, null, a.PointsDelta, "Approved", a.Reason, a.CreatedAt))
            .ToListAsync(ct);

        var extrato = compras.Concat(ajustes).OrderByDescending(e => e.CreatedAt).ToList();
        var balance = await pointsService.GetBalanceAsync(user.Id, ct);

        return new AdminUserDetailResponse(
            user.Id, user.Name, user.Email, user.Phone, user.Cpf, user.DataNascimento, balance, extrato);
    }
}
