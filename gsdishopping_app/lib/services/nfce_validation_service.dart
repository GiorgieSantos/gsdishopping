/// Resultado da leitura do QR code impresso na NFC-e/CF-e (SAT).
class NfceQrData {
  const NfceQrData({required this.accessKey, required this.rawUrl});

  /// Chave de acesso de 44 dígitos, presente na URL do QR code
  /// (parâmetro "p=" na maioria dos estados). É o identificador único
  /// da nota — usado pelo backend para bloquear pontuação duplicada.
  final String accessKey;
  final String rawUrl;

  /// CNPJ do emitente, embutido na própria chave de acesso (posições
  /// 7 a 20, padrão ENCAT). Extraído localmente, sem precisar de rede.
  String get cnpj => accessKey.substring(6, 20);

  static NfceQrData? tryParse(String qrRawValue) {
    // Cada estado tem um domínio de SEFAZ próprio, mas o padrão do
    // parâmetro "p=" com a chave de 44 dígitos + campos adicionais
    // separados por "|" é comum à maioria das UFs (padrão ENCAT).
    final uri = Uri.tryParse(qrRawValue);
    if (uri == null) return null;
    final p = uri.queryParameters['p'];
    if (p == null || p.length < 44) return null;
    final accessKey = p.substring(0, 44);
    if (!RegExp(r'^\d{44}$').hasMatch(accessKey)) return null;
    return NfceQrData(accessKey: accessKey, rawUrl: qrRawValue);
  }
}

/// A validação "de verdade" (checar duplicidade, CNPJ cadastrado e,
/// opcionalmente, confirmar a nota junto ao seu serviço externo) roda no
/// backend próprio — ver GSDIShoppingApiService.validateReceipt, que chama
/// POST /api/receipts/validate no GSDIShoppingApi (ASP.NET Core). Este arquivo
/// cuida só da parte que faz sentido rodar no aparelho: decodificar o QR.
