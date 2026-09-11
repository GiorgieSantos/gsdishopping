/// Campanha do shopping (ex.: "Semana da Moda"), vinda do GSDIShoppingApi
/// (`GET /api/catalog/campaigns`). Agrupa os [Coupon]s que o cliente pode
/// ver enquanto ela estiver ativa.
class Campaign {
  const Campaign({
    required this.id,
    required this.title,
    required this.description,
    this.imageUrl,
    this.startsAt,
    this.endsAt,
  });

  final int id;
  final String title;
  final String description;
  final String? imageUrl;
  final DateTime? startsAt;
  final DateTime? endsAt;

  factory Campaign.fromJson(Map<String, dynamic> json) => Campaign(
        id: json['id'] as int,
        title: json['title'] as String,
        description: json['description'] as String,
        imageUrl: json['imageUrl'] as String?,
        startsAt: json['startsAt'] != null ? DateTime.tryParse(json['startsAt'] as String) : null,
        endsAt: json['endsAt'] != null ? DateTime.tryParse(json['endsAt'] as String) : null,
      );
}
