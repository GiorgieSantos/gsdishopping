import 'package:flutter/material.dart';

import '../../services/gsdishopping_api_service.dart';

class ProfileScreen extends StatelessWidget {
  const ProfileScreen({
    super.key,
    required this.name,
    required this.email,
    required this.phone,
    required this.cpf,
    required this.sexo,
    required this.dataNascimento,
    required this.api,
    required this.onSignedOut,
  });

  final String name;
  final String email;
  final String phone;
  final String cpf;
  final String sexo;
  final DateTime dataNascimento;
  final GSDIShoppingApiService api;
  final VoidCallback onSignedOut;

  String get _dataNascimentoFormatada =>
      '${dataNascimento.day.toString().padLeft(2, '0')}/'
      '${dataNascimento.month.toString().padLeft(2, '0')}/'
      '${dataNascimento.year}';

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Meu perfil')),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          ListTile(leading: const Icon(Icons.person), title: Text(name)),
          ListTile(leading: const Icon(Icons.email), title: Text(email)),
          ListTile(leading: const Icon(Icons.phone), title: Text(phone)),
          ListTile(leading: const Icon(Icons.badge), title: Text(cpf)),
          ListTile(leading: const Icon(Icons.wc), title: Text(sexo)),
          ListTile(
            leading: const Icon(Icons.cake),
            title: Text(_dataNascimentoFormatada),
          ),
          const Divider(),
          ListTile(
            leading: const Icon(Icons.logout),
            title: const Text('Sair'),
            onTap: () async {
              await api.signOut();
              onSignedOut();
            },
          ),
        ],
      ),
    );
  }
}
