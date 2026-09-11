import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';

/// Paleta e tipografia da direção visual aprovada em set/2026 (mockup
/// "Vitrine GSDIShopping" — tons terrosos com destaque em dourado/latão,
/// pensada pra lembrar apps de shopping mais premium).
///
/// Isto é o único lugar que deveria mudar se a identidade visual mudar —
/// as telas usam os componentes padrão do Material (AppBar, ElevatedButton,
/// Card, NavigationBar etc.), então elas herdam essas cores/fontes
/// automaticamente em vez de ter cada uma com estilo próprio.
class AppColors {
  AppColors._();

  static const ink = Color(0xFF241B17); // fundo escuro / texto sobre dourado
  static const sand = Color(0xFFF4ECDD); // fundo claro das telas
  static const brass = Color(0xFFCD9F49); // destaque principal
  static const brassDeep = Color(0xFFA97B2E); // destaque, tom mais escuro
  static const forest = Color(0xFF33493F); // destaque secundário (categorias)
  static const forestSoft = Color(0xFF4A6459);
  static const brick = Color(0xFFA6503D); // semântica: rejeitado / erro
  static const ok = Color(0xFF4C7A5B); // semântica: aprovado / sucesso
  static const charcoal = Color(0xFF2A2118); // texto principal
  static const charcoalDim = Color(0xFF7A6C5E); // texto secundário
}

class AppTheme {
  AppTheme._();

  /// Tipografia: Plus Jakarta Sans para título e interface, variando só o
  /// peso (uma família só, sem serifada) — a opção "C · Clean / neobank" do
  /// mockup, escolhida no lugar de uma combinação com fonte serifada.
  ///
  /// Depende do pacote `google_fonts` (adicionado ao pubspec.yaml). Ele
  /// baixa a fonte em tempo de execução na primeira vez — normal, mas
  /// exige rede; se preferir empacotar a fonte localmente (offline/App
  /// Store review mais previsível), dá pra trocar por
  /// `GoogleFonts.plusJakartaSansTextTheme()` → um `FontLoader` com os
  /// arquivos .ttf em `assets/fonts/`, sem mudar o resto deste arquivo.
  static ThemeData light() {
    final baseTextTheme = GoogleFonts.plusJakartaSansTextTheme();

    final colorScheme = ColorScheme.fromSeed(
      seedColor: AppColors.brass,
      brightness: Brightness.light,
    ).copyWith(
      primary: AppColors.brassDeep,
      onPrimary: AppColors.sand,
      secondary: AppColors.forest,
      onSecondary: AppColors.sand,
      surface: AppColors.sand,
      onSurface: AppColors.charcoal,
      error: AppColors.brick,
      onError: AppColors.sand,
    );

    return ThemeData(
      useMaterial3: true,
      colorScheme: colorScheme,
      scaffoldBackgroundColor: AppColors.sand,
      textTheme: baseTextTheme.apply(
        bodyColor: AppColors.charcoal,
        displayColor: AppColors.charcoal,
      ),
      appBarTheme: AppBarTheme(
        backgroundColor: AppColors.sand,
        foregroundColor: AppColors.charcoal,
        elevation: 0,
        titleTextStyle: GoogleFonts.plusJakartaSans(
          fontSize: 20,
          fontWeight: FontWeight.w700,
          color: AppColors.charcoal,
        ),
      ),
      elevatedButtonTheme: ElevatedButtonThemeData(
        style: ElevatedButton.styleFrom(
          backgroundColor: AppColors.ink,
          foregroundColor: AppColors.sand,
          textStyle: GoogleFonts.plusJakartaSans(fontWeight: FontWeight.w700),
          padding: const EdgeInsets.symmetric(vertical: 14, horizontal: 20),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
        ),
      ),
    );
  }
}
