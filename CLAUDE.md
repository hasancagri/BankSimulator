# CLAUDE.md

Claude Code'a bu repo'da rehberlik eder. **Gerçek-kaynak sırası:** kod + bu dosya > Claude memory >
Obsidian vault (`~/dev/DevNotes/Sanal POS Eğitimi/`).

**Mimari + kod konvansiyonları (taşınabilir katman): @docs/conventions.md** — DDD/VSA/CQRS, Critter Stack,
Result/Options pattern. PaymentGateway ile AYNI konvansiyon (devralındı). Bu dosya yalnız BU projeye özeldir.

## Proje

**Banka simülatörü** — DenizBank'ın üye-işyerine gösterdiği iki yüzü taklit eder: (1) **ödeme yüzü**
InterVpos protokolü (`/mpi/*`, form-urlencoded istek + `;;` ayraçlı cevap + SHA1 hash), (2) **operasyon
yüzü** API Market POS uçları (JSON zarf + SubscriptionKey). Hedef: PaymentGateway adapter'ının ve mews/pos
InterPos gateway'inin (nihai uyumluluk ölçütü) karşısında çalışmak. Mülakat-hazırlığı: her karar gerekçeli.

**Çok-banka, tek repo (4b):** banka-bağımsız Core + banka başına **facade** (ilk: MaviBank/InterVpos;
ileride SarıBank aynı kod farklı config, TuruncuBank farklı protokol). Yeni banka = yeni facade klasörü.

> **Durum (bootstrap TAMAM, 2026-10-21):** iskelet kuruldu (Aspire + Marten/Wolverine + yalın Common);
> Domain BOŞ. **İlk dilim = Faz 1 NonSecure Auth** (spec-kit `001-nonsecure-auth` yazılacak). Kapsam:
> `POST /mpi/Default.aspx` TxnType=Auth + `;;` encoder + Merchant/Card seed + Transaction event stream +
> test-kart senaryo motoru. Void/Refund/Status/batch/3DS/operasyon-API/onboarding = sonraki fazlar.

## Komutlar

Repo kökünden. Çözüm: `BankSimulator.slnx`. Paket sürümleri yalnız `Directory.Packages.props` (CPM).

```bash
dotnet build                                              # tüm çözüm
dotnet run --project src/aspire/AppHost/AppHost.csproj    # tüm sistem (Aspire; Postgres 5434 + RabbitMQ)
dotnet test                                               # testler (MaviBank.Api.Tests)
```

- **Sistemi hep Aspire AppHost'tan başlat**; conn-string'i (bankDb/rabbitmq) Aspire'dan gelir.
- **Marten şeması otomatik** (`ApplyAllDatabaseChangesOnStartup`) — migration komutu yok.
- Postgres host portu **5434** (PG 5433, varsayılan 5432 ile çakışmasın — çoklu Aspire dev).

## BC haritası (storm çıktısı — doldurulacak)

Tek BC = kendi şeması (`mavibank`). Faz 1 aggregate/doküman adayları:

| Tip | Saklama | Ne | Faz |
|---|---|---|---|
| `Transaction` | Marten **event stream** (`[AggregateHandler]`) | Auth/decline; para hareketi kaydı; PAN saklanmaz | 1 |
| `Merchant` | düz doküman (seed) | ShopCode/UserCode/UserPass/SecretKey/SubscriptionKey + komisyon + bloke | 1 |
| `Card` | düz doküman (seed) | test kartları + senaryo bayrağı | 1 |
| `Batch` | aggregate | gün sonu/komisyon/bloke | 2 |
| `Reconciliation` | `Process/` | gece rapor çek, PG defteriyle karşılaştır | 2/3 |

Storm artefaktı: `~/dev/DevNotes/Sanal POS Eğitimi/EventStorming - Banka İçi + Dayanıklılık (Storm 2).md`.

## Projeye özel kurallar + tuzaklar

- **`;;` cevap, JSON DEĞİL.** InterVpos tel: istek `application/x-www-form-urlencoded`, cevap `key=value;;...`.
  `;;` encoder (handler-içi helper eğilimi). Tel sadakati pazarlıksız (uyumluluk ölçütü = mews/pos InterPos).
- **Determinizm.** Aynı girdi aynı sonuç — test hedefi. Sentinel test-kart → canned sonuç; **gerçek bakiye
  modeli YOK** (Faz 1; `51` yalnız sentinel bayrağı). Gerçek bakiye/hesap ledger'ı sonraki faz.
- **Gerçek dış bağımlılık yok.** SMS yok → `ICodeDeliveryChannel` (Faz 4, 3DS); gerçek kart ağı yok.
- **PAN saklanmaz** — Transaction yalnız maskeli + token + ref tutar (SAQ A-EP paralel disiplin).
- **Handler'da literal YASAK** — kullanıcıdan gelmeyen her değer `Options/` POCO'sundan (conventions Config).
- **Wolverine keşfi ad-son-eki bazlı:** `*Handler`/`*Consumer` ile bitmeyen handler taramayla BULUNMAZ →
  `opts.Discovery.IncludeType(...)` şart (çoğul "Handlers" sessizce yutulur).
- **MCP:** banka **operasyon yüzü** MCP serbest (Faz 3, AgentPlatform gateway'e mount + AP IdP scope);
  **ödeme icrası (Auth) MCP'ye GİRMEZ** — protokol rayı (bkz memory `decisions_unified_mcp_agentplatform`).

## Yapma listesi

- **Ödeme akışını MCP'ye açma** (Auth/çekim protokol; MCP yalnız operatör düzlemi).
- **Banka wire tipini paylaşılan lib'e çıkarma** — facade içinde nested (anti-corruption).
- **`IConfiguration`'dan doğrudan okuma** / handler'da literal (Options pattern).
- **Gerçek SMS / dış kart ağı** bağlama (determinizm bozulur).
- **Marketplace/sub-merchant/payout** (CreateSubMerchant vb.) — kapsam dışı (pivot kararı).
