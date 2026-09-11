import 'dart:io';

import 'package:camera/camera.dart';
import 'package:flutter/material.dart';
import 'package:mobile_scanner/mobile_scanner.dart';

import '../../models/store.dart';
import '../../services/gsdishopping_api_service.dart';
import '../../services/nfce_validation_service.dart';
import '../../services/ocr_service.dart';

/// Fluxo de escaneamento do cupom fiscal em três etapas:
///  1) leitura do QR code impresso na nota (fonte de verdade para
///     antifraude — dá a chave de acesso de 44 dígitos e o CNPJ);
///  2) checagem rápida se o CNPJ é de uma loja do shopping (feedback
///     antecipado — a checagem que vale de verdade é sempre refeita no
///     backend, ver GSDIShoppingApi/Services/PointsService.cs);
///  3) foto do corpo da nota + OCR, usada só para preview do valor.
///
/// O envio final para validação vai para o backend próprio (GSDIShoppingApi),
/// que resolve a loja pelo CNPJ, confere elegibilidade e duplicidade, e
/// delega a autenticidade ao seu serviço externo.
class ReceiptScannerScreen extends StatefulWidget {
  const ReceiptScannerScreen({super.key, required this.api});

  final GSDIShoppingApiService api;

  @override
  State<ReceiptScannerScreen> createState() => _ReceiptScannerScreenState();
}

class _ReceiptScannerScreenState extends State<ReceiptScannerScreen> {
  final OcrService _ocrService = OcrService();

  NfceQrData? _qrData;
  bool _lookingUpStore = false;
  Store? _store;
  bool _storeNotEligible = false;

  CameraController? _cameraController;
  Future<void>? _cameraInitFuture;
  File? _photoFile;
  double? _guessedTotal;

  bool _submitting = false;

  // Desde a Fase 4 a validação de autenticidade roda em background no
  // backend (fila + NfceValidationWorker) — depois do POST inicial, o app
  // acompanha o desfecho com GET /receipts/{id} em vez de receber
  // aprovado/rejeitado na hora. _polling cobre essa espera; _finalStatus
  // (nome do ReceiptStatus) e os campos abaixo cobrem o desfecho, seja ele
  // imediato (duplicidade/loja não elegível) ou vindo do polling.
  bool _polling = false;
  String? _finalStatus;
  String? _finalMessage;
  int? _finalPoints;
  int? _finalBalance;

  @override
  void dispose() {
    _ocrService.dispose();
    _cameraController?.dispose();
    super.dispose();
  }

  Future<void> _onQrDetected(BarcodeCapture capture) async {
    if (_qrData != null) return;
    if (capture.barcodes.isEmpty) return;
    final raw = capture.barcodes.first.rawValue;
    if (raw == null) return;
    final parsed = NfceQrData.tryParse(raw);
    if (parsed == null) return;

    setState(() {
      _qrData = parsed;
      _lookingUpStore = true;
    });

    try {
      final store = await widget.api.findStoreByCnpj(parsed.cnpj);
      if (!mounted) return;
      setState(() {
        _store = store;
        _storeNotEligible = store == null;
        _lookingUpStore = false;
      });
      if (store != null) {
        await _initCamera();
      }
    } catch (_) {
      // Não travamos o fluxo por causa de um erro de rede na checagem
      // antecipada — o usuário ainda pode tentar enviar, e o backend
      // aplica a mesma regra de elegibilidade de qualquer forma.
      if (!mounted) return;
      setState(() {
        _lookingUpStore = false;
        _storeNotEligible = false;
      });
      await _initCamera();
    }
  }

  Future<void> _initCamera() async {
    try {
      final cameras = await availableCameras();
      if (cameras.isEmpty || !mounted) return;
      final controller = CameraController(
        cameras.first,
        ResolutionPreset.medium,
        enableAudio: false,
      );
      final initFuture = controller.initialize();
      setState(() {
        _cameraController = controller;
        _cameraInitFuture = initFuture;
      });
    } catch (_) {
      // Sem câmera disponível (ex.: emulador sem câmera, ou permissão
      // negada) — o usuário ainda consegue enviar sem preview de valor,
      // só digitando 0 e conferindo depois pelo extrato.
    }
  }

  Future<void> _takePhotoAndRunOcr() async {
    final controller = _cameraController;
    if (controller == null || !controller.value.isInitialized) return;
    final XFile file = await controller.takePicture();
    final text = await _ocrService.extractText(File(file.path));
    final total = _ocrService.guessTotalValue(text);
    if (!mounted) return;
    setState(() {
      _photoFile = File(file.path);
      _guessedTotal = total;
    });
  }

  Future<void> _submitForValidation() async {
    final qrData = _qrData;
    if (qrData == null) return;
    setState(() => _submitting = true);
    try {
      final submission = await widget.api.validateReceipt(
        accessKey: qrData.accessKey,
        storeCnpj: qrData.cnpj,
        totalValue: _guessedTotal ?? 0,
      );
      if (!mounted) return;
      if (submission.isPending) {
        // Passou pelas checagens locais (duplicidade, loja elegível) —
        // agora é só aguardar o worker consultar o fornecedor fiscal.
        setState(() {
          _submitting = false;
          _polling = true;
        });
        await _pollStatus(submission.transactionId);
      } else {
        // Rejeitada de cara (duplicidade ou loja não elegível) — não
        // precisou nem entrar na fila.
        setState(() {
          _submitting = false;
          _finalStatus = submission.status;
          _finalMessage = submission.message;
        });
      }
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _submitting = false;
        _finalStatus = 'Erro';
        _finalMessage = 'Não foi possível enviar a nota. Verifique sua conexão e tente novamente.';
      });
    }
  }

  /// Consulta GET /receipts/{id} periodicamente até sair de "Pending" (ou
  /// desistir depois de um tempo — a nota continua sendo processada no
  /// backend mesmo assim, o usuário só não fica preso nesta tela; ver
  /// extrato). Sem exponential backoff/streaming de propósito: para V1,
  /// polling simples de poucos segundos é suficiente e muito mais simples
  /// de manter do que WebSocket/SSE.
  Future<void> _pollStatus(int transactionId) async {
    const maxAttempts = 20;
    const interval = Duration(seconds: 3);

    for (var attempt = 0; attempt < maxAttempts; attempt++) {
      await Future.delayed(interval);
      if (!mounted) return;
      try {
        final status = await widget.api.getReceiptStatus(transactionId);
        if (!status.isPending) {
          setState(() {
            _polling = false;
            _finalStatus = status.status;
            _finalMessage = status.rejectionReason;
            _finalPoints = status.pointsEarned;
            _finalBalance = status.newPointsBalance;
          });
          return;
        }
      } catch (_) {
        // Falha de rede pontual durante o polling — tenta de novo no
        // próximo ciclo em vez de desistir na primeira.
      }
    }

    if (!mounted) return;
    setState(() {
      _polling = false;
      _finalStatus = 'Pending';
      _finalMessage = 'Ainda estamos validando sua nota junto ao fisco — '
          'confira o resultado daqui a pouco no seu extrato.';
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Escanear cupom fiscal')),
      body: Column(
        children: [
          Expanded(child: _buildTopArea()),
          if (_qrData != null && _finalStatus == null && !_polling) _buildBottomPanel(),
          if (_polling) _buildPollingPanel(),
          if (_finalStatus != null) _buildResultPanel(),
        ],
      ),
    );
  }

  Widget _buildTopArea() {
    if (_qrData == null) {
      return MobileScanner(onDetect: _onQrDetected);
    }

    if (_lookingUpStore) {
      return const Center(child: CircularProgressIndicator());
    }

    if (_storeNotEligible) {
      return const Center(
        child: Padding(
          padding: EdgeInsets.all(24),
          child: Text(
            'Esta nota não é de uma loja cadastrada no shopping, então '
            'não pode gerar pontos.',
            textAlign: TextAlign.center,
          ),
        ),
      );
    }

    final controller = _cameraController;
    final initFuture = _cameraInitFuture;
    if (controller == null || initFuture == null) {
      // Sem câmera disponível — deixa seguir sem preview de foto.
      return const Center(
        child: Padding(
          padding: EdgeInsets.all(24),
          child: Text(
            'QR code lido com sucesso.\n'
            'Não foi possível abrir a câmera para o preview do valor — '
            'você ainda pode enviar para validação.',
            textAlign: TextAlign.center,
          ),
        ),
      );
    }

    return FutureBuilder<void>(
      future: initFuture,
      builder: (context, snapshot) {
        if (snapshot.connectionState != ConnectionState.done) {
          return const Center(child: CircularProgressIndicator());
        }
        return Stack(
          fit: StackFit.expand,
          children: [
            CameraPreview(controller),
            Positioned(
              bottom: 16,
              left: 0,
              right: 0,
              child: Center(
                child: _photoFile == null
                    ? FloatingActionButton(
                        onPressed: _takePhotoAndRunOcr,
                        tooltip: 'Fotografar o corpo da nota',
                        child: const Icon(Icons.camera_alt),
                      )
                    : const CircleAvatar(
                        backgroundColor: Colors.white,
                        child: Icon(Icons.check_circle, color: Colors.green),
                      ),
              ),
            ),
          ],
        );
      },
    );
  }

  Widget _buildBottomPanel() {
    return Padding(
      padding: const EdgeInsets.all(16),
      child: Column(
        children: [
          if (_store != null) Text('Loja: ${_store!.name}'),
          Text('Chave de acesso: ${_qrData!.accessKey}'),
          if (_guessedTotal != null)
            Text('Valor identificado (preview): R\$ $_guessedTotal'),
          const SizedBox(height: 12),
          ElevatedButton(
            onPressed: (_submitting || _storeNotEligible) ? null : _submitForValidation,
            child: _submitting
                ? const SizedBox(
                    height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
                : const Text('Enviar para validação'),
          ),
        ],
      ),
    );
  }

  Widget _buildPollingPanel() {
    return const Padding(
      padding: EdgeInsets.all(16),
      child: Column(
        children: [
          CircularProgressIndicator(),
          SizedBox(height: 12),
          Text(
            'Validando sua nota junto ao fisco...\nIsso pode levar alguns segundos.',
            textAlign: TextAlign.center,
          ),
        ],
      ),
    );
  }

  Widget _buildResultPanel() {
    final status = _finalStatus!;
    final (icon, color) = switch (status) {
      'Approved' => (Icons.check_circle, Colors.green),
      'PendingReview' => (Icons.hourglass_top, Colors.orange),
      'Pending' => (Icons.schedule, Colors.orange),
      _ => (Icons.error, Colors.red),
    };
    final message = switch (status) {
      'Approved' => 'Nota validada! Você ganhou ${_finalPoints ?? 0} pontos.',
      'PendingReview' => 'Sua nota ficou em revisão manual '
          '(${_finalMessage ?? "valor ou CPF divergente"}). '
          'Assim que um administrador concluir, você verá o resultado no seu extrato.',
      _ => _finalMessage ?? 'Nota não aprovada.',
    };

    return Padding(
      padding: const EdgeInsets.all(16),
      child: Column(
        children: [
          Icon(icon, color: color, size: 48),
          const SizedBox(height: 8),
          Text(message, textAlign: TextAlign.center),
          if (_finalBalance != null) ...[
            const SizedBox(height: 8),
            Text('Novo saldo: ${_finalBalance} pontos'),
          ],
        ],
      ),
    );
  }
}
