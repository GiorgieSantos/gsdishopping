/// Anúncio de desconto de uma loja, vindo do GSDIShoppingApi
/// (`GET /api/catalog/promotions`). Diferente de [Campaign]/cupom: não
/// tem custo em pontos nem resgate — é só um aviso. [storeId] nulo
/// significa uma promoção do shopping como um todo, não de uma loja
/// específica (ver [isMallWide]).
class Promotion {
  const Promotion({
    required this.id,
    required this.title,
    required this.description,
    required this.discountLabel,
    this.storeId,
    this.storeName,
    this.imageUrl,
    this.startsAt,
    this.endsAt,
  });

  final int id;
  final String title;
  final String description;
  final String discountLabel;
  final int? storeId;
  final String? storeName;
  final String? imageUrl;
  final DateTime? startsAt;
  final DateTime? endsAt;

  bool get isMallWide => storeId == null;

  factory Promotion.fromJson(Map<String, dynamic> json) => Promotion(
        id: json['id'] as int,
        title: json['title'] as String,
        description: json['description'] as String,
        discountLabel: json['discountLabel'] as String,
        storeId: json['storeId'] as int?,
        storeName: json['storeName'] as String?,
        imageUrl: json['imageUrl'] as String?,
        startsAt: json['startsAt'] != null ? DateTime.tryParse(json['startsAt'] as String) : null,
        endsAt: json['endsAt'] != null ? DateTime.tryParse(json['endsAt'] as String) : null,
      );
}
