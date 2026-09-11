using System.Security.Claims;
using System.Text;
using GSDIShoppingApi.Data;
using GSDIShoppingApi.Models;
using GSDIShoppingApi.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// --- Banco de dados (MySQL via Pomelo) ---
// Dois jeitos de consumir o mesmo DbContext, de propósito: os componentes
// Blazor do painel usam a fábrica (IDbContextFactory) — um componente
// Blazor Server vive pela duração inteira da conexão (circuit), então
// guardar um DbContext injetado direto nele (em vez de criar um novo por
// operação) é um erro clássico de concorrência/tempo de vida; os
// controllers de API e os demais serviços (PointsService, o worker etc.)
// recebem um GSDIShoppingDbContext "scoped por requisição" normal.
//
// IMPORTANTE sobre como isso é registrado: só chamamos
// AddDbContextFactory (que registra DbContextOptions<T> como Singleton) e
// depois derivamos o DbContext scoped A PARTIR da fábrica — nunca
// chamamos AddDbContext separadamente. Registrar os dois lado a lado
// (como este arquivo fazia até a primeira vez que alguém rodou
// `dotnet run` de verdade) quebra: AddDbContext registra
// DbContextOptions<T> como Scoped, AddDbContextFactory só tenta registrar
// essa mesma opção se ainda não existir (TryAdd) — como AddDbContext já
// tinha registrado primeiro, o Singleton IDbContextFactory<T> acabava
// dependendo de um DbContextOptions<T> Scoped, e o ASP.NET Core recusa
// isso na validação de DI ("Cannot consume scoped service ... from
// singleton ..."). O padrão abaixo é o recomendado pela própria
// documentação do EF Core para esse cenário misto.
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default não configurada.");

// Versão fixa em vez de ServerVersion.AutoDetect(connectionString) — de
// propósito. AutoDetect abre uma conexão de verdade com o MySQL na hora
// de montar as DbContextOptions, e isso roda de novo dentro da validação
// automática de DI que o ASP.NET Core faz em builder.Build() (em
// Development, ValidateOnBuild constrói cada singleton registrado,
// incluindo o IDbContextFactory<GSDIShoppingDbContext> abaixo). Se o
// MySQL não estiver no ar, ou as credenciais estiverem erradas, nesse
// instante, o app nem sobe — e o erro chega embrulhado numa
// AggregateException de DI ("Some services are not able to be
// constructed"), sem dizer claramente "não consegui conectar no MySQL".
// Fixando a versão, a primeira conexão de verdade só acontece quando um
// DbContext é efetivamente usado (ex.: no seed dos parâmetros, logo
// abaixo) — erro mais direto de entender se o banco não estiver
// acessível. Ajuste "Database:MySqlVersion" no appsettings se a versão do
// seu MySQL for diferente de 8.0.
var mySqlVersion = new MySqlServerVersion(
    Version.Parse(builder.Configuration["Database:MySqlVersion"] ?? "8.0.36"));
void ConfigureDb(DbContextOptionsBuilder options) =>
    options.UseMySql(connectionString, mySqlVersion);
builder.Services.AddDbContextFactory<GSDIShoppingDbContext>(ConfigureDb);
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IDbContextFactory<GSDIShoppingDbContext>>().CreateDbContext());

// --- Autenticação: JWT (API) + cookie (painel Blazor), lado a lado ---
// O app cliente e o Swagger/automação continuam usando Bearer JWT, sem
// mudanças. O painel de administração (/admin/**) é acessado num
// navegador comum, então usa cookie de sessão — é o próprio
// HttpContext.SignInAsync("AdminCookie", ...) feito no POST /admin/login
// mais abaixo. O esquema "Smart" decide qual dos dois usar por caminho da
// requisição: /admin/** (páginas do painel) vai de cookie, todo o resto
// (a API, incluindo /api/admin/**) continua indo de JWT como sempre foi.
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key não configurada.");
const string AdminCookieScheme = "AdminCookie";
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = "Smart";
        options.DefaultAuthenticateScheme = "Smart";
        options.DefaultChallengeScheme = "Smart";
    })
    .AddPolicyScheme("Smart", "JWT (API) ou cookie (painel), por caminho", options =>
    {
        options.ForwardDefaultSelector = context =>
            context.Request.Path.StartsWithSegments("/admin")
                ? AdminCookieScheme
                : JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        };
    })
    .AddCookie(AdminCookieScheme, options =>
    {
        options.LoginPath = "/admin/login";
        options.AccessDeniedPath = "/admin/login";
        options.Cookie.Name = "GSDIShoppingAdmin";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", policy => policy
        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, AdminCookieScheme)
        .RequireRole("Admin"));

// --- Serviços de aplicação ---
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IPointsService, PointsService>();
builder.Services.AddScoped<ISystemParametersService, SystemParametersService>();
builder.Services.AddScoped<ImageUploadService>();

// --- Fase 4: fila de validação + worker + fornecedor fiscal ---
// A fila é singleton porque quem a injeta (o worker, abaixo) também é
// singleton — ver DatabaseNfceValidationQueue para por que isso é seguro
// (usa IDbContextFactory em vez de DbContext scoped).
builder.Services.AddSingleton<INfceValidationQueue, DatabaseNfceValidationQueue>();

// Em Development usamos o INfceProvider "dublê" (sem rede, sem custo) —
// ver MockNfceProvider. Fora de Development, o provider real é a
// Infosimples (ver InfoSimplesNfceProvider — confira o comentário lá
// antes de ir para produção, os nomes de campo ainda precisam ser
// validados contra uma consulta real).
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddScoped<INfceProvider, MockNfceProvider>();
}
else
{
    builder.Services.AddHttpClient<INfceProvider, InfoSimplesNfceProvider>(client =>
    {
        var baseUrl = builder.Configuration["InfoSimples:BaseUrl"] ?? "https://api.infosimples.com/";
        client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(
            builder.Configuration.GetValue("InfoSimples:HttpTimeoutSeconds", 320));
    });
}

// O worker roda dentro deste mesmo processo/servidor (ver
// ARQUITETURA_TECNICA.md §11) — não é um deploy separado em V1.
builder.Services.AddHostedService<NfceValidationWorker>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Sem isto, o Swagger não mostra o botão "Authorize" — dá para ver os
    // endpoints protegidos na lista, mas não tem como testar nenhum deles
    // pela UI (POST /receipts/validate, /wallet/*, /admin/*), só por
    // curl com o header Authorization manual. Com isto: depois de logar
    // (POST /api/auth/login ou /api/admin/auth/login) e copiar o "token"
    // da resposta, clique em "Authorize" no topo da página e cole SÓ o
    // token (o Swagger já adiciona o prefixo "Bearer " sozinho).
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Cole aqui só o token (sem o prefixo \"Bearer \") — vem de POST /api/auth/login (cliente final) ou /api/admin/auth/login (admin).",
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer",
                },
            },
            Array.Empty<string>()
        },
    });
});

// --- Painel de administração (Blazor Server, dentro deste mesmo projeto) ---
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Libera o app Flutter (em dev) a chamar esta API de qualquer origem.
// Em produção, troque WithOrigins("*") pelos domínios reais do seu app.
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

// --- Seed dos parâmetros configuráveis conhecidos pelo código ---
// Só cria a linha se a chave ainda não existir — não sobrescreve um valor
// que o admin já tenha ajustado pelo painel. Roda aqui (a cada start) em
// vez de num script SQL à parte porque é exatamente esse o objetivo da
// tela de Parâmetros: o CÓDIGO é quem sabe quais chaves existem e o que
// cada uma significa; o painel só edita o Value (ver
// Components/Pages/Admin/Parametros.razor).
using (var seedScope = app.Services.CreateScope())
{
    var seedDb = seedScope.ServiceProvider.GetRequiredService<GSDIShoppingDbContext>();
    if (!await seedDb.SystemParameters.AnyAsync(p => p.Key == "CpfDivergenteAction"))
    {
        seedDb.SystemParameters.Add(new SystemParameter
        {
            Key = "CpfDivergenteAction",
            Value = CpfDivergenteAction.Revisar,
            Description = "O que fazer quando o CPF gravado na nota fiscal é diferente do CPF do usuário logado. " +
                "Valores aceitos: Aprovar, Revisar, Rejeitar. Pode variar por shopping/campanha no futuro.",
        });
        await seedDb.SaveChangesAsync();
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles(); // serve wwwroot/ — inclui as imagens enviadas pelo painel em wwwroot/uploads/
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapControllers();

// --- Login/logout do painel: fora dos componentes Blazor de propósito ---
// Um componente Blazor Server interativo roda sobre uma conexão
// já aberta (SignalR) — nesse ponto a resposta HTTP inicial já foi
// enviada, então não dá mais para escrever um cookie nela
// (HttpContext.SignInAsync precisa rodar ANTES da resposta ser
// finalizada). Por isso login/logout são endpoints comuns, e a página
// Components/Pages/Admin/Login.razor é renderizada estática (sem
// @rendermode), postando pra cá como um formulário HTML normal.
app.MapPost("/admin/login-submit", async (HttpContext http, GSDIShoppingDbContext db) =>
{
    var form = await http.Request.ReadFormAsync();
    var email = form["email"].ToString().Trim();
    var password = form["password"].ToString();
    var returnUrl = form["returnUrl"].ToString();

    var admin = await db.AdminUsers.SingleOrDefaultAsync(a => a.Email == email);
    if (admin is null || !BCrypt.Net.BCrypt.Verify(password, admin.PasswordHash))
    {
        var failTarget = "/admin/login?erro=1"
            + (string.IsNullOrEmpty(returnUrl) ? "" : $"&returnUrl={Uri.EscapeDataString(returnUrl)}");
        return Results.Redirect(failTarget);
    }

    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, admin.Id.ToString()),
        new(ClaimTypes.Name, admin.Name),
        new(ClaimTypes.Email, admin.Email),
        new(ClaimTypes.Role, "Admin"),
    };
    var identity = new ClaimsIdentity(claims, AdminCookieScheme);
    await http.SignInAsync(AdminCookieScheme, new ClaimsPrincipal(identity));

    var safeReturn = !string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith('/') && returnUrl.StartsWith("/admin")
        ? returnUrl
        : "/admin/lojas";
    return Results.Redirect(safeReturn);
});

app.MapPost("/admin/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(AdminCookieScheme);
    return Results.Redirect("/admin/login");
});

app.MapRazorComponents<GSDIShoppingApi.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
