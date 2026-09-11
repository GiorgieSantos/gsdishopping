import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:gsdishopping_app/app.dart';

void main() {
  testWidgets('App inicializa e mostra a tela de login', (tester) async {
    await tester.pumpWidget(const GSDIShoppingApp());
    expect(find.text('Entrar'), findsWidgets);
  });
}
