using System.Text.RegularExpressions;

namespace GSDIShoppingApi.Services;

/// <summary>
/// Validações de formato usadas no cadastro do usuário final. São
/// verificações pragmáticas de formato — não substituem, por exemplo,
/// uma confirmação real de e-mail por link ou uma consulta a uma base
/// oficial de CPF.
/// </summary>
public static class DocumentValidators
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled);

    public static bool IsValidEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email) && EmailRegex.IsMatch(email.Trim());

    /// <summary>Mantém só os dígitos de uma string (remove máscara, espaços etc.).</summary>
    public static string OnlyDigits(string? value) =>
        value is null ? string.Empty : new string(value.Where(char.IsDigit).ToArray());

    /// <summary>Telefone brasileiro: 10 dígitos (fixo, com DDD) ou 11 (celular, com DDD).</summary>
    public static bool IsValidPhone(string digitsOnly) =>
        digitsOnly.Length is 10 or 11;

    /// <summary>
    /// Valida CPF pelo algoritmo padrão de dígito verificador (módulo 11).
    /// Espera receber só os dígitos (11 caracteres) — normalize antes com
    /// <see cref="OnlyDigits"/>.
    /// </summary>
    public static bool IsValidCpf(string digitsOnly)
    {
        if (digitsOnly.Length != 11)
        {
            return false;
        }

        // Sequências como "00000000000" ou "11111111111" passariam pelo
        // cálculo do dígito verificador, mas não são CPFs reais.
        if (digitsOnly.Distinct().Count() == 1)
        {
            return false;
        }

        var numbers = digitsOnly.Select(c => c - '0').ToArray();

        int CalcCheckDigit(int length)
        {
            var sum = 0;
            var weight = length + 1;
            for (var i = 0; i < length; i++)
            {
                sum += numbers[i] * weight;
                weight--;
            }
            var remainder = sum % 11;
            return remainder < 2 ? 0 : 11 - remainder;
        }

        return CalcCheckDigit(9) == numbers[9] && CalcCheckDigit(10) == numbers[10];
    }

    /// <summary>
    /// Valida CNPJ pelo algoritmo padrão de dígito verificador (módulo 11).
    /// Espera receber só os dígitos (14 caracteres) — normalize antes com
    /// <see cref="OnlyDigits"/>.
    /// </summary>
    public static bool IsValidCnpj(string digitsOnly)
    {
        if (digitsOnly.Length != 14)
        {
            return false;
        }

        if (digitsOnly.Distinct().Count() == 1)
        {
            return false;
        }

        var numbers = digitsOnly.Select(c => c - '0').ToArray();

        int CalcCheckDigit(int length)
        {
            var weights = length == 12
                ? new[] { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 }
                : new[] { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
            var sum = 0;
            for (var i = 0; i < length; i++)
            {
                sum += numbers[i] * weights[i];
            }
            var remainder = sum % 11;
            return remainder < 2 ? 0 : 11 - remainder;
        }

        return CalcCheckDigit(12) == numbers[12] && CalcCheckDigit(13) == numbers[13];
    }

    public static readonly IReadOnlySet<string> SexoValoresValidos =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Masculino", "Feminino", "Outro" };

    public static bool IsValidDataNascimento(DateOnly dataNascimento)
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        if (dataNascimento > hoje)
        {
            return false; // não pode nascer no futuro
        }

        var idadeMaxima = hoje.AddYears(-120);
        return dataNascimento >= idadeMaxima;
    }
}
