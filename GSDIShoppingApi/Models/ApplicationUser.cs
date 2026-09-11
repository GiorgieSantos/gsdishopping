namespace GSDIShoppingApi.Models;

/// <summary>
/// Usuário final (cliente do shopping). Este é o único cadastro de
/// autenticação do sistema. O cadastro de lojas, campanhas e cupons
/// (feito por quem administra o programa de fidelidade) vive num painel
/// de administração próprio, sobre este mesmo backend e banco — não há
/// mais dependência do Squidex (ver ARQUITETURA_TECNICA.md, seção 8).
/// </summary>
public class ApplicationUser
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }

    /// <summary>Só dígitos (DDD + número), sem máscara — normalizado no cadastro.</summary>
    public required string Phone { get; set; }

    /// <summary>Só dígitos (11 caracteres), sem máscara — normalizado no cadastro.</summary>
    public required string Cpf { get; set; }

    /// <summary>Um de: "Masculino", "Feminino", "Outro".</summary>
    public required string Sexo { get; set; }

    public required DateOnly DataNascimento { get; set; }

    public required string PasswordHash { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PointTransaction> PointTransactions { get; set; } = new List<PointTransaction>();
    public ICollection<PointsAdjustment> PointsAdjustments { get; set; } = new List<PointsAdjustment>();
}
