namespace GSDIShoppingApi.Models;

/// <summary>
/// Parâmetro de negócio configurável pelo painel, em vez de hardcoded no
/// código. Nasceu da regra "nota com CPF diferente do usuário logado":
/// como isso pode mudar por shopping ou campanha, viraria uma tela nova a
/// cada regra desse tipo se ficasse hardcoded — em vez disso, é uma tabela
/// chave/valor genérica (ver Components/Pages/Admin/Parametros.razor), e
/// tanto essa regra quanto outras futuras leem daqui em vez de ganhar tela
/// própria. Quem lê os valores é <see cref="ISystemParametersService"/>.
/// </summary>
public class SystemParameter
{
    public int Id { get; set; }

    /// <summary>
    /// Identificador estável usado pelo código para achar o parâmetro
    /// (ex.: "CpfDivergenteAction"). Não é para o admin inventar chaves
    /// livremente pelo painel — só o código sabe quais chaves lê; o painel
    /// só edita o Value de chaves que já existem (ver seed em Program.cs).
    /// </summary>
    public required string Key { get; set; }

    public required string Value { get; set; }

    /// <summary>Texto explicando o que o parâmetro faz e os valores aceitos, mostrado no painel.</summary>
    public required string Description { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public int? UpdatedByAdminId { get; set; }
    public AdminUser? UpdatedByAdmin { get; set; }
}

/// <summary>Valores aceitos hoje pelo parâmetro "CpfDivergenteAction" — ver NfceValidationWorker.</summary>
public static class CpfDivergenteAction
{
    public const string Aprovar = "Aprovar";
    public const string Revisar = "Revisar";
    public const string Rejeitar = "Rejeitar";

    public static readonly string[] Todos = [Aprovar, Revisar, Rejeitar];
}
