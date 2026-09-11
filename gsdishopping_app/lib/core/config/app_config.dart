/// Configuração central de ambiente do app.
///
/// Até a Fase 1, o app falava com duas APIs (Squidex para o catálogo +
/// GSDIShoppingApi para login/pontos). Desde a decisão de descontinuar o
/// Squidex (ver ARQUITETURA_TECNICA.md, seção 8), o catálogo (lojas,
/// campanhas, cupons, promoções) também mora no GSDIShoppingApi — só
/// existe uma URL base para configurar.
class AppConfig {
  AppConfig._();

  /// URL base do backend (GSDIShoppingApi — ASP.NET Core/MySQL). Cuida de
  /// autenticação do cliente final, saldo/extrato de pontos, validação de
  /// nota fiscal e catálogo (lojas/campanhas/cupons/promoções).
  /// Em dev local no simulador iOS, "localhost" já aponta pro seu Mac.
  /// Em dispositivo físico/Android emulator, troque pelo IP da máquina.
  static const String gsdiShoppingApiBaseUrl = String.fromEnvironment(
    'GSDISHOPPING_API_BASE_URL',
    defaultValue: 'https://localhost:7100/api',
  );
}
