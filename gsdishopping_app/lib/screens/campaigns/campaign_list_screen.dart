import 'package:flutter/material.dart';

import '../../models/campaign.dart';
import '../../services/gsdishopping_api_service.dart';
import '../coupons/coupon_list_screen.dart';

class CampaignListScreen extends StatefulWidget {
  const CampaignListScreen({super.key, required this.api});

  final GSDIShoppingApiService api;

  @override
  State<CampaignListScreen> createState() => _CampaignListScreenState();
}

class _CampaignListScreenState extends State<CampaignListScreen> {
  late Future<List<Campaign>> _future;

  @override
  void initState() {
    super.initState();
    _future = widget.api.listCampaigns();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Campanhas')),
      body: FutureBuilder<List<Campaign>>(
        future: _future,
        builder: (context, snapshot) {
          if (snapshot.connectionState != ConnectionState.done) {
            return const Center(child: CircularProgressIndicator());
          }
          if (snapshot.hasError) {
            return Center(child: Text('Erro ao carregar campanhas: ${snapshot.error}'));
          }
          final campaigns = snapshot.data ?? [];
          if (campaigns.isEmpty) {
            return const Center(child: Text('Nenhuma campanha ativa no momento'));
          }
          return ListView.separated(
            padding: const EdgeInsets.all(16),
            itemCount: campaigns.length,
            separatorBuilder: (_, __) => const SizedBox(height: 10),
            itemBuilder: (context, index) {
              final campaign = campaigns[index];
              return Card(
                child: ListTile(
                  leading: const Icon(Icons.campaign_outlined),
                  title: Text(campaign.title),
                  subtitle: Text(campaign.description),
                  trailing: const Icon(Icons.chevron_right),
                  onTap: () => Navigator.of(context).push(
                    MaterialPageRoute(
                      builder: (_) => CouponListScreen(
                        api: widget.api,
                        campaignId: campaign.id,
                        campaignTitle: campaign.title,
                      ),
                    ),
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
