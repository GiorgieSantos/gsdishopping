# Arquitetura técnica — GSDIShopping (app de fidelidade do shopping)

_Atualizado após a decisão de descontinuar o Squidex e centralizar tudo — catálogo (lojas, campanhas, promoções/cupons), usuários e pontos — num único sistema: o GSDIShoppingApi (ASP.NET Core + MySQL) e um painel de administração próprio por cima dele. Ver seção 8 para o histórico dessa decisão; a versão anterior (híbrida, com Squidex cuidando do catálogo) fica registrada ali para referência._

## 1. Avaliação do plano original

O plano que você trouxe está no caminho certo — usar peças open-source para não pagar por um SaaS de fidelidade pronto é razoável — mas antes de começar a programar valia corrigir alguns pontos que, na prática, mudam como o projeto é construído.

**O `qureshi96/loyalty_app` existe, mas é bem mais simples do que "estrutura pronta" sugere.** Confirmei o repositório: é um projeto pequeno (1 estrela no GitHub), com cinco telas — conta do usuário com saldo de pontos e QR code, login, lista de recompensas, extrato de atividade e configurações. Ele serve bem como referência visual de layout, mas não traz autenticação real nem integração com backend — isso foi construído do zero, usando o repositório apenas como inspiração de UI.

**O Squidex é uma escolha sólida e gratuita, mas só para o catálogo.** É 100% open source sob licença MIT, self-hostável via Docker, com um plano gratuito também no Squidex Cloud. Funciona bem como o painel onde a equipe do shopping cadastra lojas, campanhas e cupons. O sistema de login dele, porém, foi desenhado para os **editores do CMS**, não para autenticar milhares de clientes finais — por isso a arquitetura final (seção 2) usa um backend próprio para tudo que envolve o cliente final: login, saldo de pontos e validação de nota.

**O Flutter ML Kit é a escolha certa para o OCR, mas ele sozinho não resolve a validação anti-fraude.** O pacote `google_mlkit_text_recognition` roda 100% no aparelho — no Android usa o ML Kit do Google, no iOS usa o Vision framework da Apple por baixo do mesmo plugin. Mas **texto de OCR não prova que uma nota fiscal é real**. A validação de autenticidade no Brasil se apoia no **QR code impresso no cupom fiscal (NFC-e ou CF-e/SAT)**, que contém a chave de acesso de 44 dígitos vinculada ao portal da Secretaria da Fazenda do estado emissor. O app lê o QR code (não o texto) como fonte de verdade, e usa o OCR só como conveniência para mostrar um preview do valor ao usuário.

Sobre o Xcode: **não é nele que o app Flutter é desenvolvido** — o desenvolvimento acontece com o Flutter SDK, escrevendo código Dart em VS Code ou Android Studio. O Xcode entra em três momentos: abrir o projeto iOS gerado automaticamente pelo Flutter para configurar a assinatura (conta de desenvolvedor Apple, Bundle ID, provisioning profile), rodar no simulador iOS, e gerar o build de release para TestFlight/App Store. O `README.md` do projeto Flutter detalha esse fluxo.

## 2. Arquitetura final: sistema único (GSDIShoppingApi + MySQL + painel de admin)

Tudo — catálogo (lojas, campanhas, promoções/cupons), usuários do app e pontos — vive num só sistema, com um único banco e um único painel de administração. O app Flutter fala com uma única API; a equipe do shopping opera um único painel, com um único login:

```
┌───────────────────────────┐                        ┌────────────────────────────┐
│   GSDIShopping (Flutter)    │  JWT (cliente final)   │   Painel Admin (Blazor)      │
│   App cliente — iOS/Android │ ───────────────────▶  │   /admin/** — dentro do      │
│                              │                        │   mesmo GSDIShoppingApi     │
│  - Carteira de pontos        │                       │  - Lojas                     │
│  - Catálogo de cupons/promo  │                       │  - Campanhas                 │
│  - Scanner (QR + OCR)        │                       │  - Promoções/Cupons          │
│  - Login / Perfil            │                       │  - Usuários e pontos          │
└──────────────┬───────────────┘                       └───────────────┬────────────┘
               │  ▲                                                    │  ▲
               │  │  JWT + saldo/extrato/validação/catálogo             │  │  cookie de sessão
               ▼  │                                                    ▼  │  (mesmo DbContext, direto)
        ┌──────────────────────────────────────────────────────────────────┐
        │  GSDIShoppingApi (ASP.NET Core 8 + MySQL + Blazor Server)         │
        │  - /api/auth/*                (login/cadastro do cliente final)    │
        │  - /api/wallet/*               (saldo/extrato unificado — compras+ajustes) │
        │  - /api/receipts/validate      (validação de nota + pontuação)     │
        │  - /api/catalog/stores/*       (lojas — leitura pública, escrita admin) │
        │  - /api/catalog/campaigns/*    (campanhas — idem)                  │
        │  - /api/catalog/coupons/*      (promoções/cupons — idem)           │
        │  - /api/admin/auth/*           (bootstrap + login de admin, via JWT) │
        │  - /api/admin/users/*          (listar clientes, extrato, ajuste de pontos) │
        │  - /admin/**                   (painel Blazor Server — cookie de sessão) │
        │  Tabelas: Users, PointTransactions, Stores, Campaigns, Coupons,    │
        │  Promotions, AdminUsers, PointsAdjustments                         │
        └──────────────────────────────┬───────────────────────────────────┘
                                        │  POST /validate
                                        ▼
                          ┌───────────────────────────┐
                          │  Seu serviço externo         │
                          │  de validação de nota         │
                          │  fiscal (você implementa)    │
                          └───────────────────────────┘
```

O **app Flutter** fala só com o GSDIShoppingApi: lê o catálogo (lojas, campanhas, promoções) por endpoints públicos de leitura, e usa o token JWT do cliente final para tudo que é sensível — login, saldo/extrato, validação de nota.

O **painel de administração** (Fase 3, entregue) não é um projeto separado: é um conjunto de páginas Blazor Server vivendo dentro do próprio `GSDIShoppingApi`, em `/admin/**`, com login próprio de administrador via cookie de sessão (diferente do JWT do cliente final do app). Como roda no mesmo processo, o painel lê/grava direto no mesmo `DbContext` — sem passar pela própria API HTTP, sem CORS, sem servidor a mais para hospedar. Os endpoints `/api/admin/*` continuam existindo à parte, por JWT, para Swagger/automação (ver seção 10).

O **GSDIShoppingApi** continua cuidando da autenticação do cliente final (JWT, senha com hash bcrypt), do saldo de pontos (como livro-razão — ver seção 5) e da validação da nota fiscal, chamando o **seu serviço externo de validação** — e cuida também do catálogo (lojas/campanhas/promoções), da autenticação de administrador e do painel em si, que antes ficariam no Squidex.

> Esta arquitetura substitui a versão híbrida anterior (Squidex + backend próprio), descontinuada pelo motivo registrado na seção 8.

## 3. Modelo de dados

Tudo no MySQL do GSDIShoppingApi (ver `GSDIShoppingApi/schema.sql`):

| Tabela | Campos principais | Observação |
|---|---|---|
| `Users` | Name, Email (único), Phone, Cpf (único), Sexo, DataNascimento, PasswordHash, CreatedAt | Login do cliente final do app |
| `Stores` | Name, Cnpj (único), Floor, LogoUrl, CreatedAt | Implementada na Fase 1. CNPJ é usado para conferir a nota fiscal e decidir se ela pontua (regra de elegibilidade em `PointsService`) |
| `PointTransactions` | UserId, StoreId (FK para `Stores`), AccessKey (único), TotalValue, PointsEarned, Status, RejectionReason | Livro-razão append-only; `AccessKey` com índice único bloqueia nota duplicada no nível do banco. Os pontos são sempre creditados ao `UserId` — `StoreId` é só o registro de onde a compra foi feita, e só existe se o CNPJ da nota bateu com uma loja cadastrada |
| `Campaigns` | Title, Description, ImageUrl, StartsAt, EndsAt, CreatedAt | Implementada na Fase 2. Ex.: "Semana da Moda" — agrupa `Coupons` |
| `Coupons` | Title, Description, PointsCost, CampaignId (FK, cascade), ImageUrl, ExpiresAt | Implementada na Fase 2. Algo que o cliente resgata gastando pontos — sempre pertence a uma campanha (resgate em si ainda não implementado, só a leitura do catálogo) |
| `Promotions` | Title, Description, DiscountLabel, StoreId (FK p/ `Stores`, nulo = promoção do shopping como um todo), ImageUrl, StartsAt, EndsAt | Implementada na Fase 2. Decisão de produto: **diferente de `Coupon`** — é só um anúncio de desconto de uma loja, sem custo em pontos e sem resgate (ver seção 8.1) |
| `AdminUsers` | Name, Email (único), PasswordHash, CreatedAt | Implementada na Fase 3. Login da equipe do shopping no painel — separado de `Users`, com seu próprio JWT/claims de admin (`ClaimTypes.Role = "Admin"`). Sem coluna `Role`: só existe um papel de admin hoje — ver seção 10 |
| `PointsAdjustments` | UserId (FK), PointsDelta, Reason, AdminUserId (FK), CreatedAt | Implementada na Fase 3. Livro-razão de ajustes manuais de pontos feitos por um admin (bônus/correção/estorno) — mesmo princípio de `PointTransactions` (seção 5), nunca um contador. Entra na mesma soma que forma o saldo do cliente |

## 4. Fluxo de escaneamento e pontuação (implementado)

1. O cliente abre a aba "Escanear" e aponta a câmera para o **QR code** impresso no cupom fiscal.
2. O app decodifica a URL do QR code (`NfceQrData.tryParse`), extraindo a chave de acesso de 44 dígitos e o CNPJ do emitente (embutido nas posições 7–20 da própria chave — não precisa de rede para isso).
3. O app consulta `GET /api/catalog/stores?cnpj=...` no GSDIShoppingApi só para dar feedback rápido: mostra o nome da loja se o CNPJ é conhecido, ou avisa de cara que a nota não pontua se não for. Essa consulta é só conveniência de UX — não é a checagem que vale (ver passo 5).
4. Se a loja foi reconhecida, o app abre a câmera (pacote `camera`) para uma foto do corpo da nota e roda OCR local (`OcrService`) só para mostrar "valor identificado: R$ X" como preview antes de enviar.
5. O app chama `POST /api/receipts/validate` com `{ accessKey, storeCnpj, totalValue }` (autenticado via JWT do cliente final) — **sem enviar storeId**: o backend resolve a loja sozinho a partir do CNPJ, para que a checagem de elegibilidade não dependa de nada que o cliente afirme.
6. O `PointsService` do GSDIShoppingApi: (a) rejeita na hora se a `AccessKey` já existe no banco; (b) resolve a loja pelo CNPJ em `Stores` — se não houver loja cadastrada com esse CNPJ, rejeita com "loja fora do shopping", sem chamar o serviço externo (regra de elegibilidade, implementada na Fase 1); (c) chama o seu serviço externo de validação passando `{ accessKey, storeCnpj, totalValue }`; (d) se aprovado, calcula os pontos (`Points:ReaisPerPoint` no `appsettings.json`, hoje 1 ponto por R$1) e grava a transação com o `StoreId` resolvido no passo (b) — sempre no `UserId` de quem escaneou, nunca na loja; (e) se der corrida entre duas requisições simultâneas com a mesma chave, o índice único do MySQL barra a segunda e ela volta como duplicada — sem depender só da checagem em memória.
7. O app recebe `{ approved, pointsEarned, newPointsBalance }` e atualiza a tela.

### Contrato esperado do seu serviço externo de validação

```
POST {ExternalValidation:BaseUrl}/validate
Body:     { "accessKey": "...", "storeCnpj": "...", "totalValue": 123.45 }
Resposta: { "approved": true, "reason": null }
       ou { "approved": false, "reason": "nota não encontrada na SEFAZ" }
```

Só o arquivo `GSDIShoppingApi/Services/ExternalReceiptValidationService.cs` depende desse formato exato — se o seu serviço usar outro contrato, é o único lugar que precisa mudar.

## 5. Por que o saldo de pontos não é um contador

`PointTransactions` é um livro-razão: o saldo é sempre `SUM(PointsEarned)` das transações aprovadas daquele usuário, nunca um número guardado à parte que precisa ser incrementado. Isso evita uma classe inteira de bugs de concorrência (duas notas processadas ao mesmo tempo dessincronizando um contador) e já entrega o extrato de graça — é por isso que `WalletController.GetBalance` calcula a soma em vez de ler um campo `Balance`.

Desde a Fase 3, o saldo (`PointsService.GetBalanceAsync`) soma **duas** fontes: `PointTransactions` aprovadas (compras) e `PointsAdjustments` (ajustes manuais de admin — bônus, correção, estorno). A segunda tabela existe separada da primeira porque `PointTransaction.StoreId` é obrigatório (toda compra aconteceu numa loja) — um ajuste manual não tem loja, e misturar os dois exigiria tornar `StoreId` opcional para todo mundo. O extrato do cliente (`GET /api/wallet/transactions`, e a tela equivalente do painel) mistura as duas fontes numa lista só, ordenada por data, com um campo `Type` ("Compra"/"Ajuste") diferenciando a linha.

## 6. O que foi entregue e o que ainda falta

**GSDIShoppingApi (ASP.NET Core 8 + MySQL + Blazor Server)** — código completo: entidades (`ApplicationUser`, `PointTransaction`, `Store`, `Campaign`, `Coupon`, `Promotion`, `AdminUser`, `PointsAdjustment`), `GSDIShoppingDbContext` com os índices únicos e as FKs (`PointTransactions → Stores` restrict, `Coupons → Campaigns` cascade, `Promotions → Stores` set-null, `PointsAdjustments → Users`/`→ AdminUsers` restrict), autenticação JWT do cliente final (`AuthController`, `JwtTokenService`) e de admin (`AdminAuthController`, mesmo `JwtTokenService` com `GenerateAdminToken`), `WalletController` (extrato unificado), `ReceiptsController`, `CatalogController` (`GET` público / `POST` protegido por admin para `stores`, `campaigns`, `coupons`, `promotions`), `AdminUsersController` (listar clientes, extrato, ajuste de pontos), `PointsService` (orquestração da seção 4 + soma de ajustes) e `ExternalReceiptValidationService`. O painel de administração (`Components/Pages/Admin/*`) cobre login, CRUD com upload de imagem para Lojas/Campanhas/Cupons/Promoções, e a tela de Usuários com extrato + ajuste manual de pontos — ver seção 10 para a arquitetura de autenticação por trás disso. Inclui `schema.sql` para bootstrap rápido do MySQL, `migration_fase1.sql`/`migration_fase2.sql`/`migration_fase3.sql` para quem já tinha o banco de fases anteriores, e um `README.md` com o passo a passo de setup e o fluxo de primeiro acesso ao painel.

**Aviso importante:** este código nunca foi compilado neste ambiente — a política de rede daqui bloqueia `api.nuget.org`, então `dotnet restore` não funciona por aqui. É um bloqueio de política, não algo contornável. No seu Mac isso deve funcionar normalmente (acesso comum à internet); rode `dotnet restore && dotnet build` e me avise se aparecer algum erro de compilação — reviso e corrijo. Isso vale ainda mais para o painel Blazor desta fase: é a parte mais nova e mais difícil de revisar sem compilar de verdade. Se o banco já existia de antes da Fase 3, rode `migration_fase3.sql` (aditivo, não mexe no que já existe — ver o `README.md` do backend).

**App Flutter** — as telas de login/cadastro, carteira (saldo + extrato) e scanner já chamam o GSDIShoppingApi de verdade via `GSDIShoppingApiService`, incluindo o fluxo completo de scan de nota descrito na seção 4. O catálogo (lojas, campanhas, cupons, promoções) foi migrado do Squidex para o GSDIShoppingApi na Fase 2. Na Fase 3, a tela de carteira passou a mostrar o extrato unificado (compras e ajustes manuais de admin, ver seção 5). Falta ainda: permissões de câmera nos projetos nativos `ios/`/`android/` — documentadas no `README.md` do app, mas só aplicáveis depois que você rodar `flutter create` (essas pastas ainda não existem); o fluxo de resgate de cupom (gastar pontos), que nenhuma fase cobriu ainda — só a leitura do catálogo; e aplicar o layout do mockup de identidade visual às telas (hoje elas herdam a paleta/tipografia do tema, mas usam listas simples do Material, não a estrutura desenhada no mockup — ver seção 9).

**Painel de administração e catálogo** — completo na Fase 3: `AdminUsers`, `PointsAdjustments`, os endpoints `/api/admin/*`, a escrita protegida do catálogo, e o painel Blazor Server em si (`/admin/**`) com CRUD + upload de imagem para Lojas/Campanhas/Cupons/Promoções e gestão de usuários/pontos. Ver seção 10 para as decisões de arquitetura por trás disso, e o documento de backlog e plano de trabalho para o detalhamento por fase.

## 7. Riscos e próximos passos

O maior risco do projeto não é técnico, é regulatório/operacional: validar contra a SEFAZ de cada estado tem particularidades (URLs e formatos diferentes por UF) — isso fica inteiramente dentro do seu serviço externo de validação, então o GSDIShoppingApi não precisa mudar quando você adicionar estados. Para o MVP, a checagem de chave duplicada já cobre a fraude mais óbvia (reenviar a mesma nota várias vezes); a profundidade da validação (só checar formato da chave vs. de fato consultar a SEFAZ) é uma decisão que fica inteiramente no seu serviço externo. Também vale gerar migrations do EF Core (`dotnet ef migrations add InitialCreate`) assim que `dotnet restore` funcionar na sua máquina, para ter versionamento de schema em vez de depender só do `schema.sql` — isso fica ainda mais importante agora, com todas as tabelas novas das últimas fases.

Risco atual, de verificação: o painel de administração (Blazor Server) é a parte de código mais nova e a única desta arquitetura que nunca tinha sido usada antes neste projeto — vale um teste manual cuidadoso de cada tela (login, CRUD de cada entidade, upload de imagem, ajuste de pontos) assim que compilar, exatamente pelo motivo do aviso na seção 6. O resgate de cupom (cliente gastar pontos por um cupom) continua não implementado — nenhuma fase até aqui pediu isso; é candidato natural pra uma Fase 4, se fizer sentido.

## 8. Decisão: descontinuar o Squidex e centralizar tudo no GSDIShoppingApi

A arquitetura original (seção 1) escolheu uma solução híbrida: Squidex para o catálogo (lojas/campanhas/cupons), backend próprio para autenticação/pontos. Essa decisão foi revisitada porque surgiu um requisito novo, mais importante que a economia de esforço do Squidex: **a equipe do shopping precisa de um único local para gerenciar tudo** — lojas, campanhas, promoções e usuários/pontos — com um único login.

Com o Squidex mantido, isso exigiria uma de duas coisas: (a) a equipe do shopping usar duas ferramentas diferentes (o Squidex para catálogo, mais alguma outra coisa para usuários/pontos, que nem existia), o que não atende ao requisito; ou (b) construir um painel próprio que funcione como uma "casca" única por cima de dois sistemas — o Squidex por trás para catálogo, o GSDIShoppingApi por trás para usuários/pontos —, o que resolve a experiência do usuário mas mantém dois bancos de dados, dois sistemas para hospedar/manter, e complexidade de integração dos dois lados.

Avaliou-se também se dava para "portar" o que o Squidex oferece pronto (navegação, validações, upload de imagem) diretamente para dentro do GSDIShoppingApi. Não é possível copiar o motor do Squidex — é uma plataforma própria (banco, interface e API dele mesmo), não um componente que se transplanta. Mas dois fatores tornaram a centralização razoável mesmo assim: (1) nenhum schema chegou a ser criado no Squidex para este projeto — não existe uma configuração customizada e testada que estaria sendo descartada, é trabalho que ainda não tinha sido feito de nenhum dos dois jeitos; e (2) os campos e regras de negócio do catálogo já estavam definidos no código do app (`Store`, `Coupon` em `gsdishopping_app/lib/models/`), então recriá-los como tabelas/endpoints no GSDIShoppingApi não é um trabalho de descoberta, é aplicar o que já foi especificado.

**Decisão:** manter tudo em um único sistema — GSDIShoppingApi + MySQL — com um painel de administração web novo, próprio, cobrindo lojas, campanhas, promoções/cupons e usuários/pontos, com autenticação de administrador separada da autenticação do cliente final do app. O Squidex sai do projeto. O app Flutter passa a ler o catálogo do próprio GSDIShoppingApi em vez do Squidex.

O detalhamento de fases e o que precisa ser construído está no documento *Backlog e Plano de Trabalho — V1*.

### 8.1. Decisão: Promoção e Cupom são conceitos diferentes

O backlog original deixou em aberto se "Promoções de lojas" seria o mesmo conceito que `Coupon` (que já existia no código do app) ou uma entidade separada. Decisão: **são diferentes**, e viraram duas tabelas:

- **`Coupon`** — algo que o cliente resgata gastando pontos. Sempre pertence a uma `Campaign` (ex.: um cupom de 10% dentro da campanha "Semana da Moda"). Tem `PointsCost`. O resgate em si (o cliente efetivamente gastar pontos por ele) ainda não foi implementado — a Fase 2 entregou só a leitura do catálogo.
- **`Promotion`** — um anúncio de desconto de uma loja (ex.: "Leve 2 pague 1" na Bella Moda), sem custo em pontos e sem resgate — é só informativo. `StoreId` é opcional: nulo representa uma promoção do shopping como um todo (ex.: "estacionamento grátis às quintas"), não de uma loja específica.

Motivo: os dois têm formas de uso e ciclos de vida diferentes — cupom é uma transação (custa pontos, é resgatado, tem uma relação de N-para-1 com uma campanha), promoção é só uma vitrine de desconto (sem custo, sem resgate, e pode ou não vir de uma loja específica). Modelar como uma tabela só exigiria campos que não fazem sentido para um dos dois casos (`PointsCost` nulo em toda promoção, ou `StoreId` nulo em todo cupom).

## 9. Identidade visual (paleta e tipografia)

Antes da Fase 2, foi definida uma primeira direção visual para o app — testada como mockup ("Vitrine GSDIShopping") com quatro telas (Início, Lojas, Escanear, Carteira) no estilo de apps de shopping: cartão de pontos em destaque, botão de escanear como ação central, navegação fixa embaixo.

**Paleta** — tons terrosos com destaque em dourado/latão:

| Papel | Cor | Hex |
|---|---|---|
| Fundo escuro / texto sobre destaque | Tinta | `#241B17` |
| Fundo claro das telas | Marfim | `#F4ECDD` |
| Destaque principal | Latão | `#CD9F49` |
| Destaque principal (tom escuro) | Latão escuro | `#A97B2E` |
| Destaque secundário (categorias) | Verde-mata | `#33493F` |
| Semântica: aprovado | Verde aprovado | `#4C7A5B` |
| Semântica: rejeitado/erro | Terracota | `#A6503D` |

**Tipografia** — Plus Jakarta Sans (família única, variando peso para título e interface — sem fonte serifada).

Nomes de loja, logo e cores exatas são placeholders; a estrutura e a paleta/tipografia é o que ficou definido. Ambas estão registradas em `lib/core/theme/app_theme.dart` (classe `AppColors` + `AppTheme.light()`), aplicadas globalmente via `MaterialApp.theme` em `app.dart` — como as telas usam componentes padrão do Material (`AppBar`, `ElevatedButton`, etc.), elas já herdam essas cores/fonte automaticamente, sem precisar de estilo por tela. Trocar a identidade visual mais pra frente (cor, fonte, logo) volta a ser um ajuste concentrado nesse arquivo.

A tipografia depende do pacote `google_fonts` (adicionado ao `pubspec.yaml`), que baixa a fonte em tempo de execução — funciona bem para desenvolvimento; se quiser evitar essa dependência de rede em produção, dá pra trocar por arquivos de fonte empacotados localmente (`assets/fonts/`) sem mudar a estrutura do arquivo de tema.

O layout completo das quatro telas (estrutura, não só cor/fonte) ainda não foi aplicado ao app de verdade — o mockup é a referência visual para quando essas telas forem construídas/refeitas nas próximas fases.

## 10. Decisões de arquitetura da Fase 3 (painel de administração)

**Blazor Server dentro do mesmo projeto, não um app separado.** A alternativa seria um segundo projeto (outra SPA, outro processo) falando com o GSDIShoppingApi por HTTP. Optou-se por Blazor Server hospedado no próprio `GSDIShoppingApi` (Razor Components + `AddInteractiveServerComponents`) porque o painel e a API já compartilham o mesmo banco e os mesmos modelos — um segundo projeto significaria duplicar DTOs/validações ou reimplementar tudo como chamadas HTTP de um serviço para o outro, sem ganho real (não há necessidade de escalar o painel independentemente da API, nem de outro time mexendo nele). Os componentes do painel leem e gravam direto no `GSDIShoppingDbContext` — mais simples e mais rápido do que o painel chamar a própria API.

**Dois esquemas de autenticação lado a lado, escolhidos pelo caminho da requisição.** O app cliente e o Swagger continuam usando JWT Bearer, sem nenhuma mudança de comportamento. O painel, por ser acessado num navegador comum, usa cookie de sessão — só dá pra fazer login de painel escrevendo um cookie numa resposta HTTP comum (`HttpContext.SignInAsync`), o que não é possível de dentro de um componente Blazor Server já conectado (a conexão SignalR já está aberta, a resposta HTTP inicial já foi enviada). Por isso `Program.cs` registra um esquema de política ("Smart") que encaminha `/admin/**` para o cookie e todo o resto (incluindo `/api/admin/**`) para JWT — e a policy `"AdminOnly"` aceita qualquer um dos dois, então os endpoints `/api/admin/*` continuam utilizáveis por Bearer token (Swagger/automação) mesmo com o painel usando cookie. É por isso, também, que a página de login (`Components/Pages/Admin/Login.razor`) é renderizada estática (sem `@rendermode`), postando para um endpoint comum (`POST /admin/login`) em vez de usar um `EditForm` interativo.

**Cadastro do primeiro admin é bootstrap-only, não uma tela.** `POST /api/admin/auth/register` só funciona enquanto não existir nenhum `AdminUser` — depois do primeiro, passa a responder 403 permanentemente. Diferente do padrão "endpoint aberto temporário até a Fase 3" usado no catálogo (Fases 1/2): aqui o fechamento é definitivo por design, para nunca deixar uma porta de "virar admin" aberta. Cadastrar um segundo admin autenticado (pelo próprio painel, já logado) não foi pedido nesta fase e não foi implementado — hoje só há um usuário administrador.

**Ajuste manual de pontos como tabela separada, não reaproveitando `PointTransaction`.** Mesma lógica da decisão de `Promotion` vs. `Coupon` (seção 8.1): os dois conceitos têm campos obrigatórios que não fazem sentido para o outro (`PointTransaction.StoreId` é obrigatório — toda compra tem uma loja; um ajuste de admin não tem). `PointsAdjustment` é uma tabela irmã, também append-only, que entra na mesma soma que forma o saldo do cliente (seção 5) e aparece misturada ao extrato de compras, com um campo `Type` diferenciando a linha — decisão tomada para que o cliente veja de onde vieram os pontos dele, em vez de um bônus "aparecer do nada" no saldo sem explicação no extrato.

## 11. Fase 4: validação assíncrona (fila + worker + Infosimples)

**De síncrono para assíncrono: a motivação era dupla.** Até a Fase 3, `POST /api/receipts/validate` chamava o serviço externo de validação dentro do próprio request HTTP — o cliente ficava esperando a latência de uma chamada de rede a um fornecedor pago para saber se ganhou pontos. Isso tinha dois problemas: (1) prendia o request à disponibilidade/latência de um serviço de terceiro, e (2) não dava para aplicar deduplicação de forma barata *antes* de gastar uma consulta paga, porque a checagem de "já existe essa chave?" e a chamada externa aconteciam na mesma passada. A Fase 4 desacopla os dois: o intake (`PointsService.ReceiveReceiptAsync`) grava a nota como `Pending` e devolve na hora (202 Accepted); um `NfceValidationWorker`, rodando em background, é quem consulta o fornecedor e decide o desfecho.

**Deduplicação antes da fila, via `INSERT` + índice único, não via `SELECT` antes.** Um `SELECT ... WHERE AccessKey = @chave` antes de enfileirar não é suficiente sob concorrência: duas requisições com a mesma chave podem passar pelo `SELECT` ao mesmo tempo, antes de qualquer uma delas gravar. A garantia real é o índice único em `PointTransactions.AccessKey` (já existente desde a Fase 1) — o `INSERT` com `Status = Pending` é a própria ação de "enfileirar", e o banco garante que só uma dessas requisições concorrentes consegue gravar; a outra recebe uma violação de chave duplicada (MySQL error 1062), traduzida numa resposta amigável. Isso significa que, mesmo sob centenas de chamadas simultâneas para a mesma nota, no máximo uma consulta paga ao fornecedor fiscal é feita — o mesmo padrão que `PointsService` já usava desde a Fase 1 para proteger a gravação de pontos, agora reaproveitado para proteger a chamada paga.

**`INfceProvider` substitui `IExternalReceiptValidationService`.** A interface da Fase 1 devolvia só "aprovado sim/não", porque toda a decisão acontecia de forma síncrona, no meio do request. A partir da Fase 4, quem decide aprovar/rejeitar/mandar para revisão é o `NfceValidationWorker` — o provider só relata o que encontrou (nota existe? autorizada? cancelada? CNPJ e valor oficiais? CPF do consumidor, se houver?). Isso separa claramente "nossa responsabilidade" (as regras de negócio: duplicidade, elegibilidade de loja, valor divergente, CPF divergente) de "responsabilidade do fornecedor fiscal" (confirmar que a nota existe e está autorizada) — a mesma divisão que o Carlos propôs na discussão da arquitetura. Duas implementações existem hoje: `MockNfceProvider` (sem rede, usado em Development) e `InfoSimplesNfceProvider` (produção — ver ressalva sobre os nomes de campo não terem sido confirmados contra uma consulta real, no comentário do próprio arquivo e no README).

**A fila em si (SQS vs. RabbitMQ) ficou deliberadamente em aberto — e isso não bloqueou a V1.** Em vez de escolher uma tecnologia de fila para poder começar a implementar, `INfceValidationQueue` abstrai "onde ficam as notas pendentes" atrás de uma interface, e a implementação de V1 (`DatabaseNfceValidationQueue`) usa a própria tabela `PointTransactions` como fila — "enfileirar" é o `INSERT` com `Status = Pending` que já precisava acontecer de qualquer forma (para a deduplicação, acima); "consumir" é o worker fazendo polling por linhas `Pending`. Isso evita depender de infraestrutura externa (AWS SQS) ou de mais um processo para manter no ar (RabbitMQ) só para validar a arquitetura — quando SQS/RabbitMQ for decidido, troca-se só essa implementação, sem tocar no worker nem no intake. A limitação aceita conscientemente: sem uma segunda instância do worker, não há corrida em cima da mesma linha; se um dia isso mudar, é o lugar certo para adicionar uma coluna de "claim" (ou migrar para uma fila de verdade, que já resolve isso de graça).

**O worker roda dentro do mesmo processo da API (V1), não como um deploy separado.** `NfceValidationWorker` é um `BackgroundService` hospedado via `AddHostedService` no próprio `GSDIShoppingApi` — não existe hoje um segundo executável/deploy para o worker. Isso é consistente com a decisão da Fase 3 de manter o painel Blazor dentro do mesmo processo (seção 10): para o volume esperado da V1 (só RJ), um processo único é mais simples de operar do que dois. Se o volume crescer a ponto de precisar escalar o worker independentemente da API, a extração para um processo/deploy separado é direta — a lógica já está isolada em `NfceValidationWorker` + `INfceProvider` + `INfceValidationQueue`, sem acoplamento com os controllers HTTP.

**Por que "SEFAZ direto" não está na V1, mesmo o RJ tendo um portal público sem certificado.** A pesquisa da Fase 4 confirmou que `consultadfe.fazenda.rj.gov.br` bloqueia tráfego de IPs de datacenter/nuvem — testado tanto por fetch direto quanto pelo navegador do próprio Carlos (que funcionou). Como o `NfceValidationWorker` roda em infraestrutura em nuvem, ele tomaria o mesmo bloqueio. Por isso a cadeia de fornecedores da V1 é só `InfoSimplesNfceProvider` (que já resolve esse acesso do lado deles) — a consulta direta ao SEFAZ do RJ fica descartada para V1, não por complexidade de implementação, mas porque a origem de rede (nuvem) não tem acesso.

**Parâmetros configuráveis: uma tabela chave/valor genérica, não uma tela por regra.** A necessidade nasceu da regra "CPF da nota diferente do usuário logado", que pode variar por shopping ou campanha — hardcoded ou numa tela dedicada, cada regra nova exigiria uma tela nova. `SystemParameter` (chave, valor, descrição) resolve isso de forma genérica: o código é quem sabe quais chaves existem e o que cada uma significa (criadas via seed em `Program.cs`, na primeira vez que o código passa a lê-las); o painel (`/admin/parametros`) só edita o valor. Hoje só existe `CpfDivergenteAction` (Aprovar/Revisar/Rejeitar), mas a tela já está pronta para receber outros parâmetros futuros sem precisar de código novo no painel.

**Lacunas conhecidas, deixadas de propósito fora do escopo da V1** (documentadas também no README e nos comentários do código, para não se perderem): não há política de retry com limite nem fila de erro (DLQ) — uma consulta que falha repetidamente fica `Pending` para sempre, tentada a cada rodada de polling; a regra "nota fora do período da campanha" do desenho original não foi implementada, porque o schema atual não liga `PointTransaction` a uma `Campaign`; e uma nota em `PendingReview` não tem uma tela dedicada de "aprovar/rejeitar revisão" — o desfecho hoje é um ajuste manual de pontos (`PointsAdjustment`) pelo admin, reaproveitando o fluxo já existente desde a Fase 3.
