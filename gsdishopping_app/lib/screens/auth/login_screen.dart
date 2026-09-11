import 'package:flutter/material.dart';

import '../../services/gsdishopping_api_service.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key, required this.api, required this.onLoggedIn});

  final GSDIShoppingApiService api;
  final ValueChanged<AuthResult> onLoggedIn;

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();
  final _nameController = TextEditingController();
  final _phoneController = TextEditingController();
  final _cpfController = TextEditingController();
  String? _sexo;
  DateTime? _dataNascimento;
  bool _isRegisterMode = false;
  bool _loading = false;
  String? _error;

  static const _sexoOpcoes = ['Masculino', 'Feminino', 'Outro'];

  Future<void> _pickDataNascimento() async {
    final now = DateTime.now();
    final picked = await showDatePicker(
      context: context,
      initialDate: DateTime(now.year - 18, now.month, now.day),
      firstDate: DateTime(now.year - 120),
      lastDate: now,
    );
    if (picked != null) {
      setState(() => _dataNascimento = picked);
    }
  }

  Future<void> _submit() async {
    if (_isRegisterMode && _dataNascimento == null) {
      setState(() => _error = 'Selecione a data de nascimento.');
      return;
    }
    if (_isRegisterMode && _sexo == null) {
      setState(() => _error = 'Selecione o sexo.');
      return;
    }

    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final result = _isRegisterMode
          ? await widget.api.register(
              name: _nameController.text,
              email: _emailController.text,
              password: _passwordController.text,
              phone: _phoneController.text,
              cpf: _cpfController.text,
              sexo: _sexo!,
              dataNascimento: _dataNascimento!,
            )
          : await widget.api.login(
              email: _emailController.text,
              password: _passwordController.text,
            );
      widget.onLoggedIn(result);
    } catch (e) {
      setState(() => _error = 'Não foi possível entrar. Confira os dados e tente novamente.');
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text(_isRegisterMode ? 'Criar conta' : 'Entrar')),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            if (_isRegisterMode) ...[
              Padding(
                padding: const EdgeInsets.only(bottom: 12),
                child: TextField(
                  controller: _nameController,
                  decoration: const InputDecoration(labelText: 'Nome'),
                ),
              ),
              Padding(
                padding: const EdgeInsets.only(bottom: 12),
                child: TextField(
                  controller: _phoneController,
                  decoration: const InputDecoration(
                    labelText: 'Telefone',
                    hintText: 'DDD + número',
                  ),
                  keyboardType: TextInputType.phone,
                ),
              ),
              Padding(
                padding: const EdgeInsets.only(bottom: 12),
                child: TextField(
                  controller: _cpfController,
                  decoration: const InputDecoration(labelText: 'CPF'),
                  keyboardType: TextInputType.number,
                ),
              ),
              Padding(
                padding: const EdgeInsets.only(bottom: 12),
                child: DropdownButtonFormField<String>(
                  value: _sexo,
                  decoration: const InputDecoration(labelText: 'Sexo'),
                  items: _sexoOpcoes
                      .map((opcao) => DropdownMenuItem(value: opcao, child: Text(opcao)))
                      .toList(),
                  onChanged: (value) => setState(() => _sexo = value),
                ),
              ),
              Padding(
                padding: const EdgeInsets.only(bottom: 12),
                child: InkWell(
                  onTap: _pickDataNascimento,
                  child: InputDecorator(
                    decoration: const InputDecoration(labelText: 'Data de nascimento'),
                    child: Text(
                      _dataNascimento == null
                          ? 'Selecionar'
                          : '${_dataNascimento!.day.toString().padLeft(2, '0')}/'
                              '${_dataNascimento!.month.toString().padLeft(2, '0')}/'
                              '${_dataNascimento!.year}',
                    ),
                  ),
                ),
              ),
            ],
            TextField(
              controller: _emailController,
              decoration: const InputDecoration(labelText: 'E-mail'),
              keyboardType: TextInputType.emailAddress,
            ),
            const SizedBox(height: 12),
            TextField(
              controller: _passwordController,
              decoration: const InputDecoration(labelText: 'Senha'),
              obscureText: true,
            ),
            if (_error != null) ...[
              const SizedBox(height: 12),
              Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
            ],
            const SizedBox(height: 24),
            ElevatedButton(
              onPressed: _loading ? null : _submit,
              child: _loading
                  ? const SizedBox(
                      height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
                  : Text(_isRegisterMode ? 'Criar conta' : 'Entrar'),
            ),
            TextButton(
              onPressed: () => setState(() => _isRegisterMode = !_isRegisterMode),
              child: Text(_isRegisterMode
                  ? 'Já tenho conta, entrar'
                  : 'Ainda não tenho conta, criar uma'),
            ),
          ],
        ),
      ),
    );
  }
}
