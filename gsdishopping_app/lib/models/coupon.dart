/// Algo que o cliente resgata gastando pontos, vindo do GSDIShoppingApi
/// (`GET /api/catalog/coupons`) — sempre pertence a uma campanha. Antes da
/// Fase 2 isto vinha do Squidex (`fromSquidex`); o Squidex saiu do projeto
/// (ver ARQUITETURA_TECNICA.md, seção 8) e o catálogo agora mora inteiro
/// no GSDIShoppingApi.
class Coupon {
  const Coupon({
    required this.id,
    required this.title,
    required this.description,
    required this.pointsCost,
    required this.campaignId,
    required this.campaignTitle,
    this.imageUrl,
    this.expiresAt,
  });

  final int id;
  final String title;
  final String description;
  final int pointsCost;
  final int campaignId;
  final String campaignTitle;
  final String? imageUrl;
  final DateTime? expiresAt;

  factory Coupon.fromJson(Map<String, dynamic> json) => Coupon(
        id: json['id'] as int,
        title: json['title'] as String,
        description: json['description'] as String,
        pointsCost: json['pointsCost'] as int,
        campaignId: json['campaignId'] as int,
        campaignTitle: json['campaignTitle'] as String,
        imageUrl: json['imageUrl'] as String?,
        expiresAt: json['expiresAt'] != null ? DateTime.tryParse(json['expiresAt'] as String) : null,
      );
}
