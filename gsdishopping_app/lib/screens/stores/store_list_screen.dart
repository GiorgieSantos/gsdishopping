import 'package:flutter/material.dart';

import '../../models/store.dart';
import '../../services/gsdishopping_api_service.dart';
import '../promotions/promotion_list_screen.dart';
import 'store_detail_screen.dart';

/// Diretório de lojas do shopping (Fase 2). Ligado ao mesmo
/// GSDIShoppingApi que resolve a loja pelo CNPJ no scanner — aqui é só
/// navegação/consulta, sem esse contexto de nota fiscal.
class StoreListScreen extends StatefulWidget {
  const StoreListScreen({super.key, required this.api});

  final GSDIShoppingApiService api;

  @override
  State<StoreListScreen> createState() => _StoreListScreenState();
}

class _StoreListScreenState extends State<StoreListScreen> {
  late Future<List<Store>> _future;

  @override
  void initState() {
    super.initState();
    _future = widget.api.listStores();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Lojas do shopping'),
        actions: [
          IconButton(
            tooltip: 'Todas as promoções',
            icon: const Icon(Icons.local_offer_outlined),
            onPressed: () => Navigator.of(context).push(
              MaterialPageRoute(builder: (_) => PromotionListScreen(api: widget.api)),
            ),
          ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: () async => setState(() => _future = widget.api.listStores()),
        child: FutureBuilder<List<Store>>(
          future: _future,
          builder: (context, snapshot) {
            if (snapshot.connectionState != ConnectionState.done) {
              return const Center(child: CircularProgressIndicator());
            }
            if (snapshot.hasError) {
              return Center(child: Text('Erro ao carregar lojas: ${snapshot.error}'));
            }
            final stores = snapshot.data ?? [];
            if (stores.isEmpty) {
              return const Center(child: Text('Nenhuma loja cadastrada ainda'));
            }
            return ListView.separated(
              padding: const EdgeInsets.all(16),
              itemCount: stores.length,
              separatorBuilder: (_, __) => const SizedBox(height: 10),
              itemBuilder: (context, index) {
                final store = stores[index];
                return Card(
                  child: ListTile(
                    leading: CircleAvatar(
                      child: Text(store.name.isNotEmpty ? store.name[0].toUpperCase() : '?'),
                    ),
                    title: Text(store.name),
                    subtitle: Text(store.floor ?? 'Piso não informado'),
                    trailing: const Icon(Icons.chevron_right),
                    onTap: () => Navigator.of(context).push(
                      MaterialPageRoute(
                        builder: (_) => StoreDetailScreen(api: widget.api, store: store),
                      ),
                    ),
                  ),
                );
              },
            );
          },
        ),
      ),
    );
  }
}
