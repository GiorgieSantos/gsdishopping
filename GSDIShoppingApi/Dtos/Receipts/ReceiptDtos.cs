namespace GSDIShoppingApi.Dtos.Receipts;

/// <summary>
/// Enviado pelo app depois que o cliente lê o QR code da nota (e,
/// opcionalmente, confirma o valor via OCR local). Não inclui StoreId de
/// propósito — o backend resolve a loja sozinho a partir do StoreCnpj
/// (ver PointsService), para que a checagem de elegibilidade não dependa
/// de nada que o cliente afirme.
/// </summary>
public record ValidateReceiptRequest(
    string AccessKey,
    string StoreCnpj,
    decimal TotalValue
);

/// <summary>
/// Resposta do POST /api/receipts/validate desde a Fase 4 — deixou de ser
/// o resultado final (aprovado/rejeitado com pontos) porque a validação
/// de autenticidade agora é assíncrona (fila + NfceValidationWorker). O
/// que o app recebe aqui é só a confirmação de que a nota foi recebida (ou
/// rejeitada de cara, por duplicidade/loja não elegível — checagens que
/// não precisam do fornecedor fiscal). Status é o nome do ReceiptStatus
/// ("Pending", "RejectedDuplicate", "RejectedInvalid"...); o app acompanha
/// o desfecho com GET /api/receipts/{id}.
/// </summary>
public record ReceiveReceiptResponse(
    int TransactionId,
    string Status,
    string Message
);

/// <summary>Resposta do GET /api/receipts/{id} — para o app consultar o desfecho de uma nota que ficou PENDENTE.</summary>
public record ReceiptStatusResponse(
    int TransactionId,
    string Status,
    int PointsEarned,
    string? RejectionReason,
    int NewPointsBalance
);
