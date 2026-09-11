# gsdishopping_app

Scaffold inicial do app de fidelidade do shopping. Este pacote traz a
estrutura de código Dart/Flutter (models, services, telas); os projetos
nativos `ios/` e `android/` **precisam ser gerados na sua máquina** com o
Flutter SDK instalado — eles dependem da sua versão do Flutter/Xcode e não
fazem sentido gerar fora do seu Mac.

## 1. Pré-requisitos no Mac

- Xcode (App Store) + Command Line Tools (`xcode-select --install`)
- Flutter SDK: `brew install --cask flutter` ou seguindo
  https://docs.flutter.dev/get-started/install/macos
- Uma conta de desenvolvedor Apple (gratuita serve para rodar no simulador
  e no seu próprio iPhone; a conta paga — US$ 99/ano — só é necessária
  para publicar na App Store)
- CocoaPods: `sudo gem install cocoapods`

Rode `flutter doctor` e resolva qualquer item marcado com "✗" antes de
prosseguir.

## 2. Gerar os projetos nativos

Dentro desta pasta:

```bash
flutter create . --project-name gsdishopping_app --org br.com.seudominio
flutter pub get
```

Isso cria `ios/` e `android/` a partir dos templates oficiais do Flutter,
mantendo o `lib/` que já está pronto aqui.

## 2.5. Permissões de câmera (obrigatório para o scanner funcionar)

Os pacotes `camera` (foto do corpo da nota) e `mobile_scanner` (leitura do
QR code) só funcionam com essas permissões declaradas. Sem isso o app
trava ou nega o acesso à câmera silenciosamente.

**iOS** — em `ios/Runner/Info.plist`, adicione (como irmã das outras
chaves, dentro do `<dict>` principal):

```xml
<key>NSCameraUsageDescription</key>
<string>Usamos a câmera para ler o QR code e fotografar o cupom fiscal.</string>
```

**Android** — em `android/app/src/main/AndroidManifest.xml`, adicione
dentro da tag `<manifest>` (fora da `<application>`):

```xml
<uses-permission android:name="android.permission.CAMERA" />
<uses-feature android:name="android.hardware.camera" android:required="false" />
<uses-feature android:name="android.hardware.camera.autofocus" android:required="false" />
```

## 3. Abrir no Xcode (para assinatura e build de iOS)

```bash
open ios/Runner.xcworkspace
```

No Xcode: selecione o target **Runner** → aba **Signing & Capabilities** →
escolha seu Team (conta Apple) → ajuste o Bundle Identifier (ex.:
`br.com.seudominio.gsdishoppingapp`). Isso é tudo que o Xcode faz aqui — o
desenvolvimento das telas continua em `lib/`, editado no VS Code/Android
Studio; o Xcode entra só nesta etapa de assinatura/build e para rodar no
simulador iOS.

## 4. Configurar as URLs dos dois backends

O app fala com duas APIs diferentes, de propósito (ver documento de
arquitetura): o Squidex para o catálogo (lojas/campanhas/cupons) e o
GSDIShoppingApi (o projeto ASP.NET Core entregue junto) para login, carteira
de pontos e validação de nota fiscal. Ao rodar/buildar, passe as duas via
`--dart-define`:

```bash
flutter run \
  --dart-define=SQUIDEX_BASE_URL=https://SEU-DOMINIO-SQUIDEX/api \
  --dart-define=SQUIDEX_APP_NAME=shopping-fidelidade \
  --dart-define=GSDISHOPPING_API_BASE_URL=https://localhost:7100/api
```

(ver `lib/core/config/app_config.dart`; a porta do GSDIShoppingApi aparece no
console quando você roda `dotnet run` nele — ver o README daquele projeto)

## 5. Rodar

```bash
flutter run
```

Escolha o simulador iOS ou um dispositivo físico quando solicitado.

## O que ainda falta implementar

- Schemas no Squidex (Campaign, Coupon) — Store já saiu do Squidex e passou
  a viver no GSDIShoppingApi (tabela `Stores`, endpoint
  `/api/catalog/stores`); ver o documento de arquitetura para os campos
  sugeridos das duas tabelas que faltam
- Subir o backend GSDIShoppingApi (projeto ASP.NET Core entregue junto) e
  cadastrar pelo menos uma loja de teste nele (ver o README daquele
  projeto, seção "Cadastrar lojas de teste") — sem isso, login/carteira
  funcionam mas o scan de nota sempre rejeita por "loja fora do shopping"
- Testar o fluxo de scanner num dispositivo/emulador de verdade (câmera
  real) — feito e revisado neste ambiente, mas nunca rodado numa câmera
  física; o comportamento em emuladores sem câmera é degradar de forma
  graciosa (deixa enviar sem preview de foto), vale confirmar que isso
  também é o desejado
