# PEÇAS AUTOMOTIVAS — Peças Originais Direto da Fábrica

E-commerce de peças automotivas OEM em **ASP.NET Core 8 MVC** (C# 12) + EF Core + Identity, com API REST (JWT) para app mobile e PWA instalável.

## Rodando localmente

Pré-requisitos: .NET SDK 8 e (opcional) Node 18+ para recompilar o CSS.

```bash
dotnet restore
dotnet run            # https://localhost:7043
```

No ambiente **Development** o projeto usa SQLite (`pecasautomotivas.db`, criado sozinho) e carrega dados de demonstração.

Login do admin (somente dev): `admin@pecasautomotivas.com.br` / `Admin@Pecas2026!`
Para testar como cliente, crie uma conta em **Criar conta** (os clientes `cliente1..6@exemplo.com.br` do seed têm senha aleatória e servem só para popular o histórico).

## Configuração de produção

`appsettings.json` vem **sem segredos**. A aplicação não sobe sem uma chave JWT de 32+ bytes.

```bash
# variáveis de ambiente (ou dotnet user-secrets em dev)
Jwt__Key="<chave aleatória com 64 caracteres ou mais>"
Seed__AdminPassword="<senha forte do primeiro admin>"
Database__Provider="SqlServer"
ConnectionStrings__SqlServer="Server=...;Database=PecasAutomotivas;..."
```

Gerar uma chave: `openssl rand -base64 64`.

Em produção `Seed:DadosDemonstracao` fica `false`: nenhum pedido ou depoimento fictício é criado. O histórico público mostra somente pedidos reais confirmados no painel, e nome/depoimento só aparecem quando o cliente marca a autorização (LGPD).

### Migrations (opcional)

Sem migrations o banco é criado com `EnsureCreated`. Para versionar o schema:

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add Inicial
dotnet ef database update
```

Com migrations presentes, a aplicação passa a usar `Migrate()` na inicialização.

## CSS (Tailwind)

O `wwwroot/css/site.css` já vem compilado. Se alterar views ou `Styles/input.css`:

```bash
npm install
npm run css:build     # ou npm run css:watch
```

## Trocar a logo

Procure por `<!-- INSERIR LOGO PEÇAS AUTOMOTIVAS AQUI -->`. Todas as telas usam o partial `Views/Shared/_Logo.cshtml`: basta trocar o conteúdo dele por
`<img src="~/images/logo.png" alt="PEÇAS AUTOMOTIVAS" class="h-10 w-auto" />` e salvar a imagem em `wwwroot/images/logo.png`.

## API mobile (`/api/v1`)

| Método | Rota | Auth |
|---|---|---|
| POST | `/api/v1/auth/token` `{ email, senha }` | — |
| GET | `/api/v1/pecas?termo=&montadora=&modelo=&ano=&categoria=&fabricanteId=&pagina=` | — |
| GET | `/api/v1/pecas/{id}` | — |
| GET | `/api/v1/pecas/oem/{codigo}` | — |
| GET | `/api/v1/montadoras`, `/api/v1/montadoras/{montadora}/modelos` | — |
| GET | `/api/v1/historico?pagina=` | — |
| GET | `/api/v1/me` | Bearer |
| GET/POST | `/api/v1/pedidos` | Bearer |
| GET | `/api/v1/pedidos/{id}` | Bearer |

Exemplo de pedido:

```json
POST /api/v1/pedidos
Authorization: Bearer <token>
{ "itens": [{ "pecaId": 1, "quantidade": 2 }], "cidade": "Curitiba", "estado": "PR", "veiculoAplicado": "VW Polo 2021" }
```

## Segurança implementada

Identity com senha forte e lockout (5 tentativas / 15 min) na Web e na API; rate limit em login e API; cookies `__Host-` HttpOnly, Secure e SameSite=Strict; anti-CSRF em todo POST; CSP estrita sem scripts/estilos inline; `X-Frame-Options: DENY`, `nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`, HSTS; consultas só via EF Core/LINQ; upload validado por extensão **e** magic bytes, limite de 5 MB e nome em GUID; proteção contra IDOR nos pedidos.

## Limitações conhecidas

- Não há gateway de pagamento nem cálculo de frete: o pedido é registrado e a equipe combina pagamento/envio. Integre Mercado Pago, Pagar.me ou similar antes de vender de verdade.
- Páginas de termos de uso e política de privacidade precisam ser escritas (o cadastro já exige o aceite).
