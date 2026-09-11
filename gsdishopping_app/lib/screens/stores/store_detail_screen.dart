import 'package:flutter/material.dart';

import '../../models/promotion.dart';
import '../../models/store.dart';
import '../../services/gsdishopping_api_service.dart';

class StoreDetailScreen extends StatefulWidget {
  const StoreDetailScreen({super.key, required this.api, required this.store});

  final GSDIShoppingApiService api;
  final Store store;

  @override
  State<StoreDetailScreen> createState() => _StoreDetailScreenState();
}

class _StoreDetailScreenState extends State<StoreDetailScreen> {
  late Future<List<Promotion>> _future;

  @override
  void initState() {
    super.initState();
    _future = widget.api.listPromotions(storeId: widget.store.id);
  }

  @override
  Widget build(BuildContext context) {
    final store = widget.store;
    return Scaffold(
      appBar: AppBar(title: Text(store.name)),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(store.name, style: Theme.of(context).textTheme.titleLarge),
                  const SizedBox(height: 6),
                  Text(store.floor ?? 'Piso não informado'),
                  const SizedBox(height: 6),
                  Text('CNPJ: ${store.cnpj}', style: Theme.of(context).textTheme.bodySmall),
                ],
              ),
            ),
          ),
          const SizedBox(height: 24),
          Text('Promoções desta loja', style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 8),
          FutureBuilder<List<Promotion>>(
            future: _future,
            builder: (context, snapshot) {
              if (snapshot.connectionState != ConnectionState.done) {
                return const Padding(
                  padding: EdgeInsets.symmetric(vertical: 24),
                  child: Center(child: CircularProgressIndicator()),
                );
              }
              if (snapshot.hasError) {
                return Text('Erro ao carregar promoções: ${snapshot.error}');
              }
              final promotions = snapshot.data ?? [];
              if (promotions.isEmpty) {
                return const Text('Nenhuma promoção ativa nesta loja no momento.');
              }
              return Column(
                children: promotions
                    .map((p) => Card(
                          child: ListTile(
                            leading: const Icon(Icons.local_offer_outlined),
                            title: Text(p.title),
                            subtitle: Text(p.description),
                            trailing: Text(
                              p.discountLabel,
                              style: const TextStyle(fontWeight: FontWeight.bold),
                            ),
                          ),
                        ))
                    .toList(),
              );
            },
          ),
        ],
      ),
    );
  }
}
