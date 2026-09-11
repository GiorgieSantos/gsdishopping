import 'package:flutter/material.dart';

import 'core/theme/app_theme.dart';
import 'screens/auth/login_screen.dart';
import 'screens/campaigns/campaign_list_screen.dart';
import 'screens/profile/profile_screen.dart';
import 'screens/scanner/receipt_scanner_screen.dart';
import 'screens/stores/store_list_screen.dart';
import 'screens/wallet/wallet_screen.dart';
import 'services/gsdishopping_api_service.dart';

class GSDIShoppingApp extends StatefulWidget {
  const GSDIShoppingApp({super.key});

  @override
  State<GSDIShoppingApp> createState() => _GSDIShoppingAppState();
}

class _GSDIShoppingAppState extends State<GSDIShoppingApp> {
  final GSDIShoppingApiService _gsdiShoppingApi = GSDIShoppingApiService();
  AuthResult? _session;

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'GSDIShopping',
      theme: AppTheme.light(),
      debugShowCheckedModeBanner: false,
      home: _session == null
          ? LoginScreen(
              api: _gsdiShoppingApi,
              onLoggedIn: (result) => setState(() => _session = result),
            )
          : _HomeShell(
              gsdiShoppingApi: _gsdiShoppingApi,
              session: _session!,
              onSignedOut: () => setState(() => _session = null),
            ),
    );
  }
}

class _HomeShell extends StatefulWidget {
  const _HomeShell({
    required this.gsdiShoppingApi,
    required this.session,
    required this.onSignedOut,
  });

  final GSDIShoppingApiService gsdiShoppingApi;
  final AuthResult session;
  final VoidCallback onSignedOut;

  @override
  State<_HomeShell> createState() => _HomeShellState();
}

class _HomeShellState extends State<_HomeShell> {
  int _index = 0;

  @override
  Widget build(BuildContext context) {
    final pages = [
      WalletScreen(api: widget.gsdiShoppingApi, userName: widget.session.name),
      StoreListScreen(api: widget.gsdiShoppingApi),
      ReceiptScannerScreen(api: widget.gsdiShoppingApi),
      CampaignListScreen(api: widget.gsdiShoppingApi),
      ProfileScreen(
        name: widget.session.name,
        email: widget.session.email,
        phone: widget.session.phone,
        cpf: widget.session.cpf,
        sexo: widget.session.sexo,
        dataNascimento: widget.session.dataNascimento,
        api: widget.gsdiShoppingApi,
        onSignedOut: widget.onSignedOut,
      ),
    ];

    return Scaffold(
      body: pages[_index],
      bottomNavigationBar: NavigationBar(
        selectedIndex: _index,
        onDestinationSelected: (i) => setState(() => _index = i),
        destinations: const [
          NavigationDestination(icon: Icon(Icons.account_balance_wallet), label: 'Carteira'),
          NavigationDestination(icon: Icon(Icons.storefront_outlined), label: 'Lojas'),
          NavigationDestination(icon: Icon(Icons.qr_code_scanner), label: 'Escanear'),
          NavigationDestination(icon: Icon(Icons.campaign_outlined), label: 'Campanhas'),
          NavigationDestination(icon: Icon(Icons.person), label: 'Perfil'),
        ],
      ),
    );
  }
}
