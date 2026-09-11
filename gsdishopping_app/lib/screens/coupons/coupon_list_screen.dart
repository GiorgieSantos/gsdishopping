import 'package:flutter/material.dart';

import '../../models/coupon.dart';
import '../../services/gsdishopping_api_service.dart';

/// Lista de cupons — de uma campanha específica (aberta a partir de
/// [CampaignListScreen], com [campaignId]/[campaignTitle] informados) ou,
/// sem esses parâmetros, todos os cupons do catálogo.
class CouponListScreen extends StatefulWidget {
  const CouponListScreen({
    super.key,
    required this.api,
    this.campaignId,
    this.campaignTitle,
  });

  final GSDIShoppingApiService api;
  final int? campaignId;
  final String? campaignTitle;

  @override
  State<CouponListScreen> createState() => _CouponListScreenState();
}

class _CouponListScreenState extends State<CouponListScreen> {
  late Future<List<Coupon>> _future;

  @override
  void initState() {
    super.initState();
    _future = widget.api.listCoupons(campaignId: widget.campaignId);
  }

  @override
  Widget build(BuildContext context) {
    final showCampaignLabel = widget.campaignId == null;

    return Scaffold(
      appBar: AppBar(title: Text(widget.campaignTitle ?? 'Cupons disponíveis')),
      body: FutureBuilder<List<Coupon>>(
        future: _future,
        builder: (context, snapshot) {
          if (snapshot.connectionState != ConnectionState.done) {
            return const Center(child: CircularProgressIndicator());
          }
          if (snapshot.hasError) {
            return Center(child: Text('Erro ao carregar cupons: ${snapshot.error}'));
          }
          final coupons = snapshot.data ?? [];
          if (coupons.isEmpty) {
            return const Center(child: Text('Nenhum cupom disponível no momento'));
          }
          return ListView.separated(
            padding: const EdgeInsets.all(16),
            itemCount: coupons.length,
            separatorBuilder: (_, __) => const SizedBox(height: 12),
            itemBuilder: (context, index) {
              final coupon = coupons[index];
              return Card(
                child: ListTile(
                  isThreeLine: showCampaignLabel,
                  title: Text(coupon.title),
                  subtitle: Text(
                    showCampaignLabel
                        ? '${coupon.campaignTitle}\n${coupon.description}'
                        : coupon.description,
                  ),
                  trailing: Text('${coupon.pointsCost} pts'),
                ),
              );
            },
          );
        },
      ),
    );
  }
}
