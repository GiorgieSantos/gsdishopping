namespace GSDIShoppingApi.Services;

/// <summary>
/// Abstração sobre "onde ficam as notas aguardando o NfceValidationWorker
/// processar" — de propósito, para não travar o resto do sistema na
/// escolha entre AWS SQS e RabbitMQ (ainda em aberto). A implementação de
/// V1 (<see cref="DatabaseNfceValidationQueue"/>) usa a própria tabela
/// PointTransactions como fila: o INSERT com Status=Pending feito no
/// intake (ver PointsService) já é o "enfileirar", protegido pelo índice
/// único de AccessKey (a mesma barreira de deduplicação que já existia
/// antes da Fase 4, só que agora protegendo a chamada paga em vez da
/// gravação do ponto).
///
/// Quando SQS/RabbitMQ for decidido, troca-se só a implementação — o
/// worker e o resto do sistema continuam dependendo só desta interface.
/// </summary>
public interface INfceValidationQueue
{
    /// <summary>
    /// Sinaliza que uma nota está pronta para ser processada. Na
    /// implementação de V1 isso é um no-op (o registro já está PENDENTE no
    /// banco); numa fila de verdade (SQS/RabbitMQ), é aqui que a mensagem
    /// seria publicada.
    /// </summary>
    Task EnqueueAsync(int pointTransactionId, CancellationToken cancellationToken);

    /// <summary>Pega até <paramref name="maxItems"/> ids de PointTransaction pendentes, para o worker processar nesta rodada.</summary>
    Task<IReadOnlyList<int>> LeaseNextBatchAsync(int maxItems, CancellationToken cancellationToken);
}
