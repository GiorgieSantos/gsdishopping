using Microsoft.AspNetCore.Components.Forms;

namespace GSDIShoppingApi.Services;

/// <summary>
/// Salva imagens enviadas pelo painel (componente InputFile) em
/// wwwroot/uploads/&lt;pasta&gt;/ e devolve a URL relativa (ex.:
/// "/uploads/lojas/guid.jpg") pro campo ImageUrl/LogoUrl do registro. Sem
/// redimensionar nem otimizar — só grava o arquivo enviado, com um limite
/// de tamanho pra não deixar alguém mandar um arquivo gigante sem querer.
/// </summary>
public class ImageUploadService(IWebHostEnvironment env)
{
    private const long MaxBytes = 5_000_000; // 5 MB

    public async Task<string> SaveAsync(IBrowserFile file, string folder, CancellationToken ct)
    {
        var extension = Path.GetExtension(file.Name);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".jpg";
        }
        var fileName = $"{Guid.NewGuid():N}{extension}";

        var targetDir = Path.Combine(env.WebRootPath, "uploads", folder);
        Directory.CreateDirectory(targetDir);

        var targetPath = Path.Combine(targetDir, fileName);
        await using var input = file.OpenReadStream(MaxBytes, ct);
        await using var output = File.Create(targetPath);
        await input.CopyToAsync(output, ct);

        return $"/uploads/{folder}/{fileName}";
    }
}
