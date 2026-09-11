namespace GSDIShoppingApi.Models;

public enum ReceiptStatus
{
    Approved = 0,
    RejectedDuplicate = 1,
    RejectedInvalid = 2,

    /// <summary>
    /// Fase 4: nota passou pelas checagens locais (duplicidade, loja
    /// elegível) e está na fila aguardando o <c>NfceValidationWorker</c>
    /// consultar o fornecedor fiscal (INfceProvider). Estado transitório —
    /// toda linha PENDENTE deve, mais cedo ou mais tarde, virar Approved,
    /// RejectedInvalid ou PendingReview. Nunca soma pontos (ver
    /// PointsService.GetBalanceAsync).
    /// </summary>
    Pending = 3,

    /// <summary>
    /// Fase 4: o fornecedor confirmou que a nota existe e está autorizada,
    /// mas alguma regra de negócio pediu revisão humana em vez de
    /// aprovação automática (hoje: valor divergente do informado pelo
    /// app). Fica visível no extrato do cliente como "em revisão" e some
    /// da lista de trabalho do worker; quem decide o desfecho final é um
    /// admin, via ajuste manual de pontos (PointsAdjustment) — não existe
    /// hoje uma tela dedicada de "aprovar/rejeitar revisão", é tratado como
    /// mais um caso do fluxo de ajustes já existente no painel.
    /// </summary>
    PendingReview = 4,
}
