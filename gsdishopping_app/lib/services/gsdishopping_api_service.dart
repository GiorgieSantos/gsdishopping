import 'package:dio/dio.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../core/config/app_config.dart';
import '../models/campaign.dart';
import '../models/coupon.dart';
import '../models/promotion.dart';
import '../models/store.dart';

class AuthResult {
  const AuthResult({
    required this.token,
    required this.userId,
    required this.name,
    required this.email,
    required this.phone,
    required this.cpf,
    required this.sexo,
    required this.dataNascimento,
  });
  final String token;
  final int userId;
  final String name;
  final String email;
  final String phone;
  final String cpf;
  final String sexo;
  final DateTime dataNascimento;
}

/// Uma linha do extrato — pode ser uma compra (nota fiscal aprovada) ou um
/// ajuste manual de pontos feito por um admin pelo painel (bônus, correção
/// etc.). [type] diferencia os dois ("Compra" ou "Ajuste"); os campos que só
/// fazem sentido pra compra (loja, valor) ficam nulos num ajuste, e vice-versa
/// ([reason] só existe em ajustes). Ver GSDIShoppingApi/Dtos/Wallet/WalletDtos.cs.
class WalletEntry {
  const WalletEntry({
    required this.id,
    required this.type,
    this.storeId,
    this.storeName,
    this.totalValue,
    required this.pointsDelta,
    required this.status,
    this.reason,
    required this.createdAt,
  });
  final int id;
  final String type;
  final int? storeId;
  final String? storeName;
  final double? totalValue;
  final int pointsDelta;
  final String status;
  final String? reason;
  final DateTime createdAt;

  bool get isCompra => type == 'Compra';

  factory WalletEntry.fromJson(Map<String, dynamic> json) => WalletEntry(
        id: json['id'] as int,
        type: json['type'] as String,
        storeId: json['storeId'] as int?,
        storeName: json['storeName'] as String?,
        totalValue: (json['totalValue'] as num?)?.toDouble(),
        pointsDelta: json['pointsDelta'] as int,
        status: json['status'] as String,
        reason: json['reason'] as String?,
        createdAt: DateTime.parse(json['createdAt'] as String),
      );
}

/// Resposta do POST /receipts/validate desde a Fase 4 — não é mais o
/// resultado final (a validação de autenticidade agora acontece em
/// background, no GSDIShoppingApi). Confirma só que a nota foi recebida
/// (ou já rejeitada de cara, por duplicidade/loja não elegível). [status]
/// é o nome do ReceiptStatus do backend ("Pending", "RejectedDuplicate",
/// "RejectedInvalid"...) — para acompanhar o desfecho de uma nota PENDENTE,
/// ver [GSDIShoppingApiService.getReceiptStatus].
class ReceiptSubmissionResult {
  const ReceiptSubmissionResult({
    required this.transactionId,
    required this.status,
    required this.message,
  });
  final int transactionId;
  final String status;
  final String message;

  bool get isPending => status == 'Pending';

  factory ReceiptSubmissionResult.fromJson(Map<String, dynamic> json) => ReceiptSubmissionResult(
        transactionId: json['transactionId'] as int,
        status: json['status'] as String,
        message: json['message'] as String,
      );
}

/// Resposta do GET /receipts/{id} — desfecho (ou status atual) de uma nota
/// que ficou PENDENTE depois do envio.
class ReceiptStatusResult {
  const ReceiptStatusResult({
    required this.transactionId,
    required this.status,
    required this.pointsEarned,
    this.rejectionReason,
    required this.newPointsBalance,
  });
  final int transactionId;
  final String status;
  final int pointsEarned;
  final String? rejectionReason;
  final int newPointsBalance;

  /// Ainda não temos um desfecho definitivo — o app deve continuar
  /// consultando. "PendingReview" já é considerado desfecho (para efeito
  /// de UI): a nota está confirmada, só aguardando um admin revisar.
  bool get isPending => status == 'Pending';

  factory ReceiptStatusResult.fromJson(Map<String, dynamic> json) => ReceiptStatusResult(
        transactionId: json['transactionId'] as int,
        status: json['status'] as String,
        pointsEarned: json['pointsEarned'] as int,
        rejectionReason: json['rejectionReason'] as String?,
        newPointsBalance: json['newPointsBalance'] as int,
      );
}

/// Cliente do backend próprio (GSDIShoppingApi — ASP.NET Core + MySQL).
/// Cuida de autenticação do cliente final, saldo/extrato de pontos,
/// validação de nota fiscal e (via painel de administração) o cadastro
/// de lojas/campanhas/cupons — tudo no mesmo backend e banco.
class GSDIShoppingApiService {
  GSDIShoppingApiService({Dio? dio})
      : _dio = dio ?? Dio(BaseOptions(baseUrl: AppConfig.gsdiShoppingApiBaseUrl)) {
    _dio.interceptors.add(InterceptorsWrapper(
      onRequest: (options, handler) async {
        final token = await _storage.read(key: _tokenKey);
        if (token != null) {
          options.headers['Authorization'] = 'Bearer $token';
        }
        handler.next(options);
      },
    ));
  }

  final Dio _dio;
  final FlutterSecureStorage _storage = const FlutterSecureStorage();
  static const _tokenKey = 'gsdishopping_api_token';

  Future<AuthResult> register({
    required String name,
    required String email,
    required String password,
    required String phone,
    required String cpf,
    required String sexo,
    required DateTime dataNascimento,
  }) async {
    final response = await _dio.post<Map<String, dynamic>>('/auth/register', data: {
      'name': name,
      'email': email,
      'password': password,
      'phone': phone,
      'cpf': cpf,
      'sexo': sexo,
      // yyyy-MM-dd — o backend espera um DateOnly.
      'dataNascimento':
          '${dataNascimento.year.toString().padLeft(4, '0')}-'
          '${dataNascimento.month.toString().padLeft(2, '0')}-'
          '${dataNascimento.day.toString().padLeft(2, '0')}',
    });
    return _saveAuthResponse(response.data!);
  }

  Future<AuthResult> login({required String email, required String password}) async {
    final response = await _dio.post<Map<String, dynamic>>('/auth/login', data: {
      'email': email,
      'password': password,
    });
    return _saveAuthResponse(response.data!);
  }

  Future<AuthResult> _saveAuthResponse(Map<String, dynamic> data) async {
    final token = data['token'] as String;
    await _storage.write(key: _tokenKey, value: token);
    return AuthResult(
      token: token,
      userId: data['userId'] as int,
      name: data['name'] as String,
      email: data['email'] as String,
      phone: data['phone'] as String,
      cpf: data['cpf'] as String,
      sexo: data['sexo'] as String,
      dataNascimento: DateTime.parse(data['dataNascimento'] as String),
    );
  }

  Future<void> signOut() => _storage.delete(key: _tokenKey);

  Future<bool> get isLoggedIn async => (await _storage.read(key: _tokenKey)) != null;

  Future<int> getBalance() async {
    final response = await _dio.get<Map<String, dynamic>>('/wallet/balance');
    return response.data!['pointsBalance'] as int;
  }

  Future<List<WalletEntry>> getTransactions() async {
    final response = await _dio.get<List<dynamic>>('/wallet/transactions');
    return response.data!
        .cast<Map<String, dynamic>>()
        .map(WalletEntry.fromJson)
        .toList();
  }

  /// Busca lojas cadastradas, opcionalmente filtrando por CNPJ. Usado na
  /// tela de scanner para descobrir, a partir do CNPJ lido no QR code, se
  /// a nota é de uma loja do shopping — e para mostrar o nome da loja ao
  /// usuário antes de enviar para validação. Devolve null se nenhuma loja
  /// tiver esse CNPJ (a checagem que vale de verdade é sempre refeita no
  /// backend, isso aqui é só feedback antecipado).
  Future<Store?> findStoreByCnpj(String cnpj) async {
    final response = await _dio.get<List<dynamic>>(
      '/catalog/stores',
      queryParameters: {'cnpj': cnpj},
    );
    final items = response.data ?? const [];
    if (items.isEmpty) return null;
    return Store.fromJson(items.first as Map<String, dynamic>);
  }

  /// Lista todas as lojas do shopping — usado na tela "Lojas" (Fase 2),
  /// diferente de [findStoreByCnpj] (que filtra uma só, para o scanner).
  Future<List<Store>> listStores() async {
    final response = await _dio.get<List<dynamic>>('/catalog/stores');
    return (response.data ?? const [])
        .cast<Map<String, dynamic>>()
        .map(Store.fromJson)
        .toList();
  }

  Future<List<Campaign>> listCampaigns() async {
    final response = await _dio.get<List<dynamic>>('/catalog/campaigns');
    return (response.data ?? const [])
        .cast<Map<String, dynamic>>()
        .map(Campaign.fromJson)
        .toList();
  }

  /// Cupons de uma campanha (é assim que a tela de campanha busca os dela)
  /// ou, sem [campaignId], todos os cupons do catálogo.
  Future<List<Coupon>> listCoupons({int? campaignId}) async {
    final response = await _dio.get<List<dynamic>>(
      '/catalog/coupons',
      queryParameters: {if (campaignId != null) 'campaignId': campaignId},
    );
    return (response.data ?? const [])
        .cast<Map<String, dynamic>>()
        .map(Coupon.fromJson)
        .toList();
  }

  /// Promoções de uma loja (tela de detalhe da loja) ou, sem [storeId],
  /// todas — inclui tanto promoções de loja quanto promoções gerais do
  /// shopping (ver [Promotion.isMallWide]).
  Future<List<Promotion>> listPromotions({int? storeId}) async {
    final response = await _dio.get<List<dynamic>>(
      '/catalog/promotions',
      queryParameters: {if (storeId != null) 'storeId': storeId},
    );
    return (response.data ?? const [])
        .cast<Map<String, dynamic>>()
        .map(Promotion.fromJson)
        .toList();
  }

  /// Envia a chave de acesso lida do QR code (+ CNPJ extraído dela) para o
  /// backend. Não envia storeId: o próprio backend resolve a loja a partir
  /// do storeCnpj e aplica a checagem de elegibilidade lá. Desde a Fase 4,
  /// isto só confirma o recebimento — a validação de autenticidade roda
  /// em background; use [getReceiptStatus] para acompanhar o desfecho
  /// quando o status vier "Pending".
  Future<ReceiptSubmissionResult> validateReceipt({
    required String accessKey,
    required String storeCnpj,
    required double totalValue,
  }) async {
    final response = await _dio.post<Map<String, dynamic>>('/receipts/validate', data: {
      'accessKey': accessKey,
      'storeCnpj': storeCnpj,
      'totalValue': totalValue,
    });
    return ReceiptSubmissionResult.fromJson(response.data!);
  }

  /// Consulta o desfecho de uma nota que ficou "Pending" após o envio.
  Future<ReceiptStatusResult> getReceiptStatus(int transactionId) async {
    final response = await _dio.get<Map<String, dynamic>>('/receipts/$transactionId');
    return ReceiptStatusResult.fromJson(response.data!);
  }
}
