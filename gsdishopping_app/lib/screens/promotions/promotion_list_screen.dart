import 'package:flutter/material.dart';

import '../../models/promotion.dart';
import '../../services/gsdishopping_api_service.dart';

/// Todas as promoções do catálogo — de lojas específicas e gerais do
/// shopping (ver [Promotion.isMallWide]) juntas. Aberta a partir da tela
/// de Lojas; a promoção de uma loja específica também aparece filtrada em
/// StoreDetailScreen.
class PromotionListScreen extends StatefulWidget {
  const PromotionListScreen({super.key, required this.api});

  final GSDIShoppingApiService api;

  @override
  State<PromotionListScreen> createState() => _PromotionListScreenState();
}

class _PromotionListScreenState extends State<PromotionListScreen> {
  late Future<List<Promotion>> _future;

  @override
  void initState() {
    super.initState();
    _future = widget.api.listPromotions();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Promoções')),
      body: FutureBuilder<List<Promotion>>(
        future: _future,
        builder: (context, snapshot) {
          if (snapshot.connectionState != ConnectionState.done) {
            return const Center(child: CircularProgressIndicator());
          }
          if (snapshot.hasError) {
            return Center(child: Text('Erro ao carregar promoções: ${snapshot.error}'));
          }
          final promotions = snapshot.data ?? [];
          if (promotions.isEmpty) {
            return const Center(child: Text('Nenhuma promoção no momento'));
          }
          return ListView.separated(
            padding: const EdgeInsets.all(16),
            itemCount: promotions.length,
            separatorBuilder: (_, __) => const SizedBox(height: 10),
            itemBuilder: (context, index) {
              final promo = promotions[index];
              return Card(
                child: ListTile(
                  leading: const Icon(Icons.local_offer_outlined),
                  title: Text(promo.title),
                  subtitle: Text(
                    promo.isMallWide ? promo.description : '${promo.storeName} · ${promo.description}',
                  ),
                  trailing: Text(
                    promo.discountLabel,
                    style: const TextStyle(fontWeight: FontWeight.bold),
                  ),
                ),
              );
            },
          );
        },
      ),
    );
  }
}
