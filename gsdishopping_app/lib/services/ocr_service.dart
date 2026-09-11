import 'dart:io';

import 'package:google_mlkit_text_recognition/google_mlkit_text_recognition.dart';

/// Extrai texto de uma foto do cupom fiscal usando reconhecimento on-device
/// (ML Kit no Android, Vision framework da Apple no iOS via o mesmo plugin).
///
/// IMPORTANTE: o texto OCR é usado apenas para exibir um preview do valor
/// ao usuário e como fallback. A validação de autenticidade e o
/// anti-fraude devem se basear no QR code da nota (ver
/// nfce_validation_service.dart), não neste texto — texto de OCR pode ser
/// forjado/editado numa foto e não prova que a nota é real.
class OcrService {
  final TextRecognizer _recognizer =
      TextRecognizer(script: TextRecognitionScript.latin);

  Future<String> extractText(File imageFile) async {
    final inputImage = InputImage.fromFile(imageFile);
    final RecognizedText result = await _recognizer.processImage(inputImage);
    return result.text;
  }

  /// Heurística simples para achar o valor total no texto extraído.
  /// Serve só de preview/confirmação para o usuário antes do envio —
  /// não é a fonte de verdade dos pontos.
  double? guessTotalValue(String rawText) {
    final regex = RegExp(r'(?:total)[^\d]{0,15}(\d{1,3}(?:\.\d{3})*,\d{2})',
        caseSensitive: false);
    final match = regex.firstMatch(rawText);
    if (match == null) return null;
    final normalized =
        match.group(1)!.replaceAll('.', '').replaceAll(',', '.');
    return double.tryParse(normalized);
  }

  Future<void> dispose() => _recognizer.close();
}
