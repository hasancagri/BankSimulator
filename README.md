# BankSimulator

Banka POS simülatörü — gerçek bir bankanın (DenizBank referans alınarak) üye-işyerine gösterdiği
**iki yüzü** taklit eder:

1. **Ödeme yüzü** — sanal POS protokolü: `POST /mpi/*`, form-urlencoded istek + `;;` ayraçlı cevap + hash imza.
2. **Operasyon yüzü** — POS/mutabakat API uçları: JSON zarf + `SubscriptionKey` header.

Amaç: [PaymentGateway](../PaymentGateway) banka-adaptörlerinin karşısında **deterministik** olarak
çalışmak (uçtan-uca test), gerçek banka / kart ağı / SMS bağlamadan.

## Mimari — çok-banka, tek repo

Banka-bağımsız **Core** + banka başına **facade**. Yeni banka = yeni facade klasörü (çekirdek değişmez).

| Banka | Servis | Protokol | Tel |
|---|---|---|---|
| MaviBank | `MaviBank.Api` | InterVpos | form-urlencoded + `;;` + SHA1 |
| SarıBank | `SariBank.Api` | InterVpos | aynı kod, farklı config |
| TuruncuBank | `TuruncuBank.Api` | TuruncuXml | XML + SHA512 |

Paylaşılan: `Simulator.Core` (Transaction/Merchant/Card/Batch domain + ops), `Simulator.InterVpos`
(ortak InterVpos tel: `;;` encoder, hash), `Common` (Result/Options/DI altyapısı).

## Çalıştırma

Repo kökünden. Çözüm: `BankSimulator.slnx`. Paket sürümleri yalnız `Directory.Packages.props` (CPM).

```bash
dotnet build                                              # tüm çözüm
dotnet run --project src/aspire/AppHost/AppHost.csproj    # tüm sistem (Aspire: Postgres + RabbitMQ)
dotnet test                                               # testler (MaviBank.Api.Tests)
```

- **Sistemi hep Aspire AppHost'tan başlat** — conn-string (bankDb/rabbitmq) Aspire'dan gelir.
- **Marten şeması otomatik** (`ApplyAllDatabaseChangesOnStartup`) — migration komutu yok.
- **Portlar:** Postgres host **5434** (PG 5433 ile çakışmaz); banka servisleri sabit **5301/5302/5303**
  (PG↔sim iki-repo wiring determinizmi).

## Proje yapısı

```
src/aspire/      AppHost (orkestrasyon) + ServiceDefaults
src/others/      Common · Simulator.Core (domain+ops) · Simulator.InterVpos (ortak tel)
src/services/    MaviBank.Api · SariBank.Api · TuruncuBank.Api (facade'lar)
tests/           MaviBank.Api.Tests
specs/           spec-kit dilimleri (yol haritası)
docs/            conventions.md (taşınabilir mimari — PG ile ortak)
```

## Yol haritası (specs/)

| Spec | Kapsam |
|---|---|
| `001-nonsecure-auth` | `/mpi/Default.aspx` TxnType=Auth + `;;` encoder + Merchant/Card seed + Transaction event stream + test-kart senaryo motoru (fraud taksonomisi dahil) |
| `002-batch-settlement-ops` | Batch yaşam döngüsü + gün-sonu/komisyon/bloke + operasyon-API uçları (POS list/batch summary) |
| `003-three-d-secure` | 3DModel akışı + ACS + callback hash + `ICodeDeliveryChannel` (SMS soyutlaması) |

Güncel durum için ilgili `specs/<dilim>/` (spec.md/tasks.md) ve git geçmişi otoritedir.

## Temel kurallar (ayrıntı: `docs/conventions.md` + `CLAUDE.md`)

- **`;;` cevap, JSON değil** — InterVpos tel sadakati pazarlıksız (uyumluluk ölçütü: mews/pos InterPos).
- **Determinizm** — aynı girdi aynı sonuç; sentinel test-kart → canned sonuç. Faz 1'de **gerçek bakiye
  modeli yok** (`51` = yetersiz bakiye yalnız sentinel bayrağı).
- **PAN saklanmaz** — Transaction yalnız maskeli + token + referans tutar.
- **Gerçek dış bağımlılık yok** — SMS/kart ağı bağlanmaz (determinizm bozulur).
- **Config = Options pattern** — handler'da literal yasak; `IConfiguration` doğrudan okunmaz.
- **MCP yalnız operasyon yüzü** (Faz 3) — ödeme icrası (Auth) MCP'ye girmez (protokol rayı).

## PaymentGateway ile ilişki

BankSimulator, PG'nin banka-adaptörlerinin (`InterVposBankAdapter`, `TuruncuBankAdapter`) **karşı
tarafıdır**. PG çekim/durum/void/refund/3DS isteklerini bu simülatöre atar; simülatör banka cevabını
üretir. İki repo sabit portlarla eşleşir (PG → sim 5301/5302/5303). Uçtan-uca test bu ikilinin birlikte
koşmasıyla yapılır.

> Kart şeması notu: banka yalnız kendi **üye-işyerini** (ShopCode = PG/platform) görür — e-ticaret
> store'larını değil (PG = merchant-of-record). Simülatörün `Merchant` tablosu = bu üye-işyeri kaydı
> (credentials + komisyon/MDR), e-ticaret merchant'ı değil.
