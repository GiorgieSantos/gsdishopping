/// Loja parceira do shopping, vinda do GSDIShoppingApi (endpoint
/// GET /api/catalog/stores). Usada para: (a) mostrar ao cliente onde a
/// compra foi feita, e (b) dar um feedback rápido na tela de scanner se o
/// CNPJ lido não é de uma loja do shopping — a checagem que vale de
/// verdade é sempre refeita no backend.
class Store {
  const Store({
    required this.id,
    required this.name,
    required this.cnpj,
    this.floor,
    this.logoUrl,
  });

  final int id;
  final String name;
  final String cnpj;
  final String? floor;
  final String? logoUrl;

  factory Store.fromJson(Map<String, dynamic> json) => Store(
        id: json['id'] as int,
        name: json['name'] as String,
        cnpj: json['cnpj'] as String,
        floor: json['floor'] as String?,
        logoUrl: json['logoUrl'] as String?,
      );
}
