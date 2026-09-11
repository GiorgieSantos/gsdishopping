import 'package:flutter/material.dart';

import '../../services/gsdishopping_api_service.dart';

class WalletScreen extends StatefulWidget {
  const WalletScreen({super.key, required this.api, required this.userName});

  final GSDIShoppingApiService api;
  final String userName;

  @override
  State<WalletScreen> createState() => _WalletScreenState();
}

class _WalletScreenState extends State<WalletScreen> {
  late Future<(int, List<WalletEntry>)> _future;

  @override
  void initState() {
    super.initState();
    _future = _load();
  }

  Future<(int, List<WalletEntry>)> _load() async {
    final balance = await widget.api.getBalance();
    final transactions = await widget.api.getTransactions();
    return (balance, transactions);
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Minha carteira')),
      body: RefreshIndicator(
        onRefresh: () async => setState(() => _future = _load()),
        child: FutureBuilder<(int, List<WalletEntry>)>(
          future: _future,
          builder: (context, snapshot) {
            if (snapshot.connectionState != ConnectionState.done) {
              return const Center(child: CircularProgressIndicator());
            }
            if (snapshot.hasError) {
              return Center(child: Text('Erro ao carregar carteira: ${snapshot.error}'));
            }
            final (balance, transactions) = snapshot.data!;
            return ListView(
              padding: const EdgeInsets.all(16),
              children: [
                Card(
                  child: Padding(
                    padding: const EdgeInsets.all(20),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text('Olá, ${widget.userName}',
                            style: Theme.of(context).textTheme.titleMedium),
                        const SizedBox(height: 8),
                        Text('$balance pontos',
                            style: Theme.of(context).textTheme.headlineMedium),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: 24),
                Text('Extrato', style: Theme.of(context).textTheme.titleMedium),
                const SizedBox(height: 8),
                if (transactions.isEmpty)
                  const ListTile(
                    leading: Icon(Icons.receipt_long),
                    title: Text('Nenhuma movimentação registrada ainda'),
                    subtitle: Text('Escaneie um cupom fiscal para começar a pontuar'),
                  )
                else
                  ...transactions.map((entry) => entry.isCompra
                      ? ListTile(
                          leading: Icon(_statusIcon(entry.status)),
                          title: Text(
                              '${entry.storeName} — R\$ ${(entry.totalValue ?? 0).toStringAsFixed(2)}'),
                          subtitle: Text('${entry.createdAt.toLocal()}'.split('.').first),
                          trailing: Text(entry.status == 'Approved'
                              ? '+${entry.pointsDelta} pts'
                              : _statusLabel(entry.status)),
                        )
                      : ListTile(
                          leading: const Icon(Icons.tune),
                          title: Text(entry.reason ?? 'Ajuste de pontos'),
                          subtitle: Text('${entry.createdAt.toLocal()}'.split('.').first),
                          trailing: Text(
                            entry.pointsDelta >= 0
                                ? '+${entry.pointsDelta} pts'
                                : '${entry.pointsDelta} pts',
                            style: TextStyle(
                              color: entry.pointsDelta >= 0
                                  ? Theme.of(context).colorScheme.secondary
                                  : Theme.of(context).colorScheme.error,
                              fontWeight: FontWeight.w600,
                            ),
                          ),
                        )),
              ],
            );
          },
        ),
      ),
    );
  }

  /// Rótulo em português para o status cru que vem do backend (nome do
  /// ReceiptStatus — ver GSDIShoppingApi/Models/ReceiptStatus.cs). Desde a
  /// Fase 4 existem os estados intermediários Pending/PendingReview, além
  /// dos dois de rejeição que já existiam.
  static String _statusLabel(String status) => switch (status) {
        'Pending' => 'Em análise',
        'PendingReview' => 'Em revisão',
        'RejectedDuplicate' => 'Nota já utilizada',
        'RejectedInvalid' => 'Nota inválida',
        _ => status,
      };

  static IconData _statusIcon(String status) => switch (status) {
        'Approved' => Icons.check_circle_outline,
        'Pending' => Icons.hourglass_top,
        'PendingReview' => Icons.rule_folder_outlined,
        _ => Icons.cancel_outlined,
      };
}
