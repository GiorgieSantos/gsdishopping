# GSDIShoppingApi

Backend próprio (ASP.NET Core 8 + MySQL) responsável por: autenticação do
cliente final, saldo/extrato de pontos, a validação assíncrona de nota
fiscal (fila + worker + fornecedor fiscal, desde a Fase 4), e o cadastro
de lojas, campanhas e cupons (via painel de administração). Não há mais
dependência do Squidex — tudo vive neste backend e no nosso MySQL.

## Aviso importante sobre este ambiente

Este código foi escrito e revisado nesta sessão, com .NET 8 SDK e MySQL
instalados e funcionando aqui. Mas **não consegui rodar `dotnet restore`
neste sandbox**: a política de rede desta sessão bloqueia o acesso a
`api.nuget.org` (onde ficam os pacotes Pomelo.EntityFrameworkCore.MySql,
JWT Bearer e BCrypt.Net-Next que o projeto usa) — é um bloqueio de
política, não algo que dá para contornar por aqui. Ou seja: não consegui
compilar nem testar de ponta a ponta neste ambiente. No seu Mac isso não
deve ser um problema (acesso normal à internet), mas rode os passos abaixo
com atenção e me avise se aparecer algum erro de compilação — reviso e
corrijo.

**Fase 4 (fila/worker/Infosimples) também foi escrita sob essa mesma
limitação** — revisei o código com cuidado (assinaturas, usings, tipos),
mas sem `dotnet build` de verdade. O ponto de maior incerteza é
`Services/InfoSimplesNfceProvider.cs`: não tive acesso à documentação
autenticada da sua conta Infosimples, então a rota e os nomes de campo lá
seguem a convenção pública que a Infosimples documenta — teste com uma
consulta real antes de confiar em produção (o comentário no topo do
arquivo detalha isso).

**Bug real encontrado no primeiro `dotnet run` de verdade (corrigido):**
`Program.cs` registrava `AddDbContext<GSDIShoppingDbContext>` e
`AddDbContextFactory<GSDIShoppingDbContext>` lado a lado desde a Fase 3 —
isso nunca tinha sido testado até você rodar localmente, e quebra: o
`AddDbContext` registra `DbContextOptions<T>` como Scoped, e o
`IDbContextFactory<T>` (Singleton) acaba dependendo dessa opção Scoped, o
que o ASP.NET Core recusa na validação de DI ("Cannot consume scoped
service ... from singleton ..."). Corrigido registrando só
`AddDbContextFactory` e derivando o `DbContext` scoped a partir dela
(`AddScoped(sp => factory.CreateDbContext())`) — o padrão recomendado pela
documentação do EF Core para esse cenário. Ver o comentário em `Program.cs`
para o detalhe completo.

## 1. Pré-requisitos no Mac

- .NET 8 SDK: `brew install --cask dotnet-sdk` ou https://dotnet.microsoft.com/download/dotnet/8.0
- MySQL local (`brew install mysql && brew services start mysql`) ou um
  container Docker (`docker run -d -p 3306:3306 -e MYSQL_ROOT_PASSWORD=root mysql:8`)

## 2. Criar o banco

```bash
mysql -u root -p -e "CREATE DATABASE gsdishopping_app CHARACTER SET utf8mb4;"
mysql -u root -p gsdishopping_app < schema.sql
```

> **Se você já tinha criado este banco antes** (na Fase 0, ex.: pra testar
> login): não precisa apagar o banco todo. O `schema.sql` mudou só em duas
> coisas — a tabela `Stores` (nova) e `PointTransactions.StoreId`, que
> deixou de ser texto livre e virou chave estrangeira para `Stores`. A
> tabela `Users` (login/cadastro) não muda em nada, seus usuários de teste
> continuam valendo. Rode a migração incremental que cuida só dessas duas
> tabelas:
> `mysql -u root -p gsdishopping_app < migration_fase1.sql`
> (ela recria `PointTransactions` do zero — normal, essa tabela não tinha
> uso real na Fase 0 porque a checagem de elegibilidade não existia ainda;
> se você tiver transações de teste ali que queira preservar de verdade, me
> avise antes de rodar). Só apague o banco inteiro se preferir recomeçar do
> zero por algum outro motivo.

> **Se você já rodou a Fase 1** e só precisa das tabelas novas da Fase 2
> (`Campaigns`, `Coupons`, `Promotions` — catálogo de campanhas/cupons/
> promoções): é só isso, aditivo, nada existente muda.
> `mysql -u root -p gsdishopping_app < migration_fase2.sql`

> **Se você já rodou a Fase 2** e só precisa das tabelas novas da Fase 3
> (`AdminUsers`, `PointsAdjustments` — login do painel e ajustes manuais
> de pontos): também é só isso, aditivo.
> `mysql -u root -p gsdishopping_app < migration_fase3.sql`

> **Se você já rodou a Fase 3** e só precisa da tabela nova da Fase 4
> (`SystemParameters` — parâmetros configuráveis pelo painel, ex.: regra de
> CPF divergente): também é só isso, aditivo.
> `mysql -u root -p gsdishopping_app < migration_fase4.sql`

## 2.5. Sem o fornecedor fiscal configurado ainda?

Não precisa fazer nada: em `ASPNETCORE_ENVIRONMENT=Development` (o padrão
local), a API já usa sozinha o `MockNfceProvider` (ver
`Services/MockNfceProvider.cs`) — aprova qualquer nota com chave de acesso
no formato certo (44 dígitos), sem precisar de rede nem de conta na
Infosimples. É assim que dá para testar o fluxo completo (app →
GSDIShoppingApi → fila → worker → pontos) antes de configurar o fornecedor
de verdade. Quando for para produção, configure `InfoSimples:Token` (seção
3) — fora de Development a API troca automaticamente para
`InfoSimplesNfceProvider`.

> Existe também um projeto separado, `ValidationMock/` (entregue na Fase 1,
> na pasta ao lado desta) — ele simulava o contrato síncrono antigo por
> HTTP. Não é mais usado pelo fluxo da Fase 4 (que já tem seu próprio mock
> embutido, acima), mas continua no repo caso você ainda o use para outra
> coisa.

## 3. Configurar segredos (não deixe hardcoded em produção)

Edite `appsettings.Development.json` (ou use `dotnet user-secrets`) com:

- `ConnectionStrings:Default` — usuário/senha do seu MySQL local
- `Jwt:Key` — uma string aleatória de pelo menos 32 caracteres
- `InfoSimples:Token` — só necessário fora de Development (produção): o
  token da sua conta Infosimples (https://api.infosimples.com/consultas/docs).
  Em Development, a API usa o `MockNfceProvider` e ignora esta chave — ver
  seção 2.5.

## 4. Restaurar, compilar e rodar

```bash
dotnet restore
dotnet build
dotnet run
```

A API sobe por padrão em `https://localhost:7xxx` (a porta exata aparece
no console — também está em `Properties/launchSettings.json`). Com
`ASPNETCORE_ENVIRONMENT=Development`, o Swagger fica disponível em
`/swagger` para testar os endpoints pelo navegador.

Se preferir gerar as migrations do EF Core em vez de usar `schema.sql`:

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate
dotnet ef database update
```

## 5. Testar com curl

```bash
# Cadastro
curl -k -X POST https://localhost:7xxx/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"name":"Maria","email":"maria@exemplo.com","password":"senha123","phone":"11987654321","cpf":"52998224725","sexo":"Outro","dataNascimento":"1990-01-01"}'

# Login (copie o "token" da resposta)
curl -k -X POST https://localhost:7xxx/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"maria@exemplo.com","password":"senha123"}'

# Saldo (endpoint autenticado)
curl -k https://localhost:7xxx/api/wallet/balance \
  -H "Authorization: Bearer SEU_TOKEN_AQUI"
```

## 5.5. Cadastrar lojas de teste (catálogo)

Antes de testar o scan de nota, cadastre pelo menos uma loja — sem isso,
`POST /api/receipts/validate` sempre rejeita por "loja fora do shopping"
(essa é a regra de elegibilidade da Fase 1). Pode ser pelo Swagger
(`/swagger`, endpoint `POST /api/catalog/stores`) ou por curl:

```bash
curl -k -X POST https://localhost:7xxx/api/catalog/stores \
  -H "Content-Type: application/json" \
  -d '{"name":"Loja Teste","cnpj":"11222333000181","floor":"L1"}'

# Conferir que ficou salva (e testar o filtro por CNPJ, que é o que o app usa):
curl -k "https://localhost:7xxx/api/catalog/stores?cnpj=11222333000181"
```

Esse endpoint de cadastro está **sem autenticação de propósito**, só para
você conseguir dar carga de teste antes do painel de administração
(Fase 3) existir. Quando o painel chegar, ele passa a exigir login de
admin.

## 5.6. Cadastrar campanhas, cupons e promoções de teste (Fase 2)

Mesma ideia da seção anterior — sem isso, as telas novas do app (Lojas,
Campanhas, Promoções) carregam vazias. Lembrando a diferença entre os
dois conceitos: **cupom** é algo que o cliente resgata gastando pontos, e
sempre pertence a uma campanha; **promoção** é só um anúncio de desconto
de uma loja (ou do shopping, se `storeId` for nulo), sem custo em pontos.

```bash
# 1) Campanha — anote o "id" da resposta, é o campaignId do cupom abaixo
curl -k -X POST https://localhost:7xxx/api/catalog/campaigns \
  -H "Content-Type: application/json" \
  -d '{"title":"Semana da Moda","description":"Até 20% em lojas selecionadas","startsAt":"2026-09-10T00:00:00Z","endsAt":"2026-09-20T00:00:00Z"}'

# 2) Cupom dentro dessa campanha (troque campaignId pelo id retornado acima)
curl -k -X POST https://localhost:7xxx/api/catalog/coupons \
  -H "Content-Type: application/json" \
  -d '{"title":"10% OFF na Bella Moda","description":"Válido na loja física","pointsCost":150,"campaignId":1}'

# 3) Promoção de uma loja específica (troque storeId por um id de loja já cadastrada — ver seção 5.5)
curl -k -X POST https://localhost:7xxx/api/catalog/promotions \
  -H "Content-Type: application/json" \
  -d '{"title":"Segunda em dobro","description":"Compre 1 leve 2 em toda a loja","discountLabel":"Leve 2 pague 1","storeId":1}'

# 4) Promoção geral do shopping (storeId nulo)
curl -k -X POST https://localhost:7xxx/api/catalog/promotions \
  -H "Content-Type: application/json" \
  -d '{"title":"Estacionamento grátis","description":"Toda quinta-feira, após as 18h","discountLabel":"Grátis"}'

# Conferir:
curl -k https://localhost:7xxx/api/catalog/campaigns
curl -k "https://localhost:7xxx/api/catalog/coupons?campaignId=1"
curl -k https://localhost:7xxx/api/catalog/promotions
```

A partir da Fase 3, estes quatro endpoints de cadastro (lojas, campanhas,
cupons, promoções) passaram a exigir login de admin — ver seção 5.7.

## 5.7. Painel de administração (Fase 3)

O painel roda dentro deste mesmo backend (Blazor Server), em
`https://localhost:7xxx/admin`. É lá que você cadastra lojas, campanhas,
cupons e promoções pela interface (em vez de curl/Swagger), com upload de
imagem, e gerencia clientes: ver saldo e extrato de cada um, e lançar
ajustes manuais de pontos (bônus, correção, estorno) — sempre com um
motivo, que fica registrado no extrato do cliente.

**Primeiro acesso — crie o admin inicial:**

O painel não tem tela de "criar conta" (de propósito — ver
`Controllers/AdminAuthController.cs`). O primeiro administrador é criado
uma única vez, pelo Swagger ou curl, e essa rota se fecha sozinha depois
(passa a responder 403) assim que existir 1 admin:

```bash
curl -k -X POST https://localhost:7xxx/api/admin/auth/register \
  -H "Content-Type: application/json" \
  -d '{"name":"Seu Nome","email":"admin@gsdishopping.com.br","password":"umaSenhaForte123"}'
```

Depois disso, entre pelo navegador em `/admin/login` com esse e-mail/senha.

**Como funciona por baixo:** o app cliente e o Swagger continuam
autenticando com JWT (Bearer), como sempre. O painel Blazor usa cookie de
sessão (login em `/admin/login`, 8h de validade) — os dois esquemas
convivem no mesmo backend (ver `Program.cs`, esquema "Smart" +
policy `"AdminOnly"`), sem CORS nem servidor separado, porque o painel
consulta o banco direto (mesmo `DbContext`), sem passar pela própria API.

**Ledger, não contador:** um ajuste manual de pontos não mexe num "saldo"
em lugar nenhum — ele só adiciona uma linha em `PointsAdjustments`, e o
saldo do cliente continua sendo sempre a soma (compras aprovadas + ajustes),
calculada na hora (ver `PointsService.GetBalanceAsync`). Isso é o mesmo
princípio de `PointTransactions` desde a Fase 1 (seção "Por que o saldo de
pontos não é uma coluna", no fim deste arquivo) — só que agora a soma tem
duas fontes em vez de uma. O extrato do cliente na Fase 3 passou a misturar
os dois tipos de linha (compra e ajuste), tanto no app quanto no painel.

## 5.8. Fase 4 — validação assíncrona (fila + worker + parâmetros)

A partir da Fase 4, `POST /api/receipts/validate` deixou de devolver
aprovado/rejeitado na hora. Ele só faz as checagens que não dependem do
fornecedor fiscal (duplicidade, loja elegível) e devolve **202 Accepted**:

```bash
curl -k -X POST https://localhost:7xxx/api/receipts/validate \
  -H "Authorization: Bearer SEU_TOKEN_AQUI" \
  -H "Content-Type: application/json" \
  -d '{"accessKey":"33260900000000000000650010000000011000000019","storeCnpj":"11222333000181","totalValue":99.90}'

# Resposta: { "transactionId": 1, "status": "Pending", "message": "Nota recebida!..." }
```

Em background, o `NfceValidationWorker` (roda dentro deste mesmo processo,
hospedado via `AddHostedService` — não é um deploy separado em V1) puxa as
notas PENDENTES, consulta o `INfceProvider` configurado e aplica as
regras de negócio (nota cancelada → rejeita; CNPJ oficial não bate com a
loja → rejeita; valor divergente → revisão; CPF divergente → conforme o
parâmetro `CpfDivergenteAction`; válida → aprova). O app acompanha o
desfecho com:

```bash
curl -k https://localhost:7xxx/api/receipts/1 \
  -H "Authorization: Bearer SEU_TOKEN_AQUI"

# Resposta: { "transactionId": 1, "status": "Approved", "pointsEarned": 99, "rejectionReason": null, "newPointsBalance": 99 }
```

Em Development, com o `MockNfceProvider` (seção 2.5), isso costuma
resolver em poucos segundos (o worker faz polling a cada
`NfceQueue:PollIntervalSeconds`, 5s por padrão).

**Parâmetros configuráveis:** `/admin/parametros` é uma tela genérica de
chave/valor — não é para cadastrar dados novos, é para ajustar regras que
o código já conhece. Hoje só existe `CpfDivergenteAction` (o que fazer
quando o CPF gravado na nota é diferente do usuário logado: Aprovar,
Revisar ou Rejeitar), criado automaticamente no primeiro start da API
depois da migração (seed em `Program.cs`). A ideia é que outras regras
configuráveis no futuro entrem na mesma tela, sem precisar de tela nova a
cada uma.

**Limitações conhecidas desta V1** (documentadas também nos comentários do
código, para não se perderem):
- A "fila" é a própria tabela `PointTransactions` (polling) — não é SQS
  nem RabbitMQ ainda. Ver `Services/INfceValidationQueue.cs`: é o ponto de
  troca quando (se) isso for decidido, sem tocar no resto do sistema.
- Não há retry com limite nem fila de erro (DLQ): se a consulta ao
  fornecedor falhar, a nota fica PENDENTE e é tentada de novo a cada
  rodada de polling, indefinidamente.
- A regra "nota fora do período da campanha" do desenho original não foi
  implementada — o schema não liga `PointTransaction` a uma `Campaign`
  ainda.
- Uma nota que cai em `PendingReview` (revisão manual) não tem tela
  própria de "aprovar/rejeitar revisão" — hoje o desfecho é um ajuste
  manual de pontos (`PointsAdjustment`) pelo admin, na tela de extrato do
  cliente (seção 5.7), como qualquer outro ajuste.

## 6. INfceProvider — trocando de fornecedor fiscal

Quem valida a autenticidade da nota junto ao fisco é uma implementação de
`Services/INfceProvider.cs` (`ConsultarAsync(chaveAcesso) -> NfceResult`).
Hoje existem duas: `MockNfceProvider` (dev, sem rede — seção 2.5) e
`InfoSimplesNfceProvider` (produção, real). Pra trocar de fornecedor no
futuro (ex.: somar a NFe.io como fallback, ou trocar de vez), crie uma
nova classe implementando `INfceProvider` e troque o registro em
`Program.cs` — nenhum outro arquivo (worker, controllers, PointsService)
precisa saber qual fornecedor está por trás.

## Estrutura

```
GSDIShoppingApi/
  Program.cs              # wiring: EF Core/MySQL, JWT + cookie admin, CORS, DI, Blazor, worker, seed de parâmetros
  Models/                 # ApplicationUser, PointTransaction, ReceiptStatus, Store, Campaign, Coupon,
                           # Promotion, AdminUser, PointsAdjustment, SystemParameter
  Data/GSDIShoppingDbContext.cs
  Dtos/                   # contratos de request/response
  Services/
    JwtTokenService.cs
    INfceProvider.cs                      # contrato com o fornecedor fiscal (ver seção 6)
    InfoSimplesNfceProvider.cs            # implementação real (produção)
    MockNfceProvider.cs                   # implementação "dublê" (Development)
    INfceValidationQueue.cs               # abstração da fila (ver seção 5.8)
    DatabaseNfceValidationQueue.cs        # implementação V1 (polling na própria tabela)
    NfceValidationWorker.cs               # BackgroundService: consome a fila, aplica as regras, grava o resultado
    ISystemParametersService.cs / SystemParametersService.cs   # leitura dos parâmetros configuráveis
    PointsService.cs                      # intake: duplicidade + elegibilidade + grava PENDENTE (ver seção 5.8)
    DocumentValidators.cs                 # validação de e-mail, telefone, CPF, CNPJ
    ImageUploadService.cs                 # salva imagens enviadas pelo painel em wwwroot/uploads/
  Controllers/
    AuthController.cs        # /api/auth/register, /api/auth/login (cliente final)
    WalletController.cs      # /api/wallet/balance, /api/wallet/transactions
    ReceiptsController.cs    # /api/receipts/validate (202), /api/receipts/{id} (status)
    CatalogController.cs     # /api/catalog/{stores,campaigns,coupons,promotions} — leitura pública, escrita exige admin
    AdminAuthController.cs   # /api/admin/auth/register (bootstrap), /api/admin/auth/login
    AdminUsersController.cs  # /api/admin/users — listar clientes, ver extrato, ajustar pontos
  Components/              # painel de administração (Blazor Server) — ver seção 5.7
    Pages/Admin/            # Login, Lojas, Campanhas, Cupons, Promoções, Usuários, extrato, Parâmetros
    Layout/
  wwwroot/                 # css do painel + uploads/ (imagens enviadas)
  schema.sql
  migration_fase1.sql      # Stores + PointTransactions.StoreId (rodar só se o banco já existia antes da Fase 1)
  migration_fase2.sql      # Campaigns + Coupons + Promotions (rodar só se o banco já existia antes da Fase 2)
  migration_fase3.sql      # AdminUsers + PointsAdjustments (rodar só se o banco já existia antes da Fase 3)
  migration_fase4.sql      # SystemParameters (rodar só se o banco já existia antes da Fase 4)
```

## Por que o saldo de pontos não é uma coluna

`PointTransactions` é um livro-razão (append-only): o saldo é sempre a
soma dos pontos das transações aprovadas (`PointsService.GetBalanceAsync`),
nunca um contador guardado à parte. Desde a Fase 4, transações PENDENTES
ou em PendingReview nunca entram nessa soma — só `Approved` conta; o saldo
só muda quando o `NfceValidationWorker` (ou um admin, via ajuste manual)
decide o desfecho final. Isso evita bugs de concorrência (duas
notas processadas ao mesmo tempo dessincronizando um contador) e já dá o
extrato de graça. O índice único em `AccessKey` é a garantia, no nível do
banco, de que a mesma nota nunca gera pontos duas vezes.

Desde a Fase 3, `PointsAdjustments` (ajustes manuais feitos por um admin)
segue o mesmo princípio: também é append-only, e entra na mesma soma que
forma o saldo — nunca um contador separado que alguém poderia
dessincronizar. Ver seção 5.7.
