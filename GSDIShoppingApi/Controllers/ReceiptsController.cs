using GSDIShoppingApi.Dtos.Receipts;
using GSDIShoppingApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GSDIShoppingApi.Controllers;

[ApiController]
[Route("api/receipts")]
[Authorize]
public class ReceiptsController(IPointsService pointsService) : ControllerBase
{
    /// <summary>
    /// Recebe a chave de acesso lida do QR code da nota, o CNPJ da loja e o
    /// valor. Desde a Fase 4, isto não devolve mais aprovado/rejeitado na
    /// hora: só confirma o recebimento (202) e enfileira a validação de
    /// autenticidade, feita em background pelo NfceValidationWorker. O app
    /// acompanha o desfecho com GET /api/receipts/{id} — ver
    /// ReceiveReceiptResponse/ReceiptStatusResponse.
    /// </summary>
    [HttpPost("validate")]
    public async Task<ActionResult<ReceiveReceiptResponse>> Validate(
        ValidateReceiptRequest request, CancellationToken ct)
    {
        var result = await pointsService.ReceiveReceiptAsync(User.GetUserId(), request, ct);
        return Accepted(result);
    }

    /// <summary>Consulta o status atual de uma nota enviada por este usuário — usado para acompanhar uma nota que ficou PENDENTE (ou EmRevisão) após o POST acima.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ReceiptStatusResponse>> GetStatus(int id, CancellationToken ct)
    {
        var status = await pointsService.GetReceiptStatusAsync(User.GetUserId(), id, ct);
        if (status is null)
        {
            return NotFound();
        }
        return Ok(status);
    }
}
