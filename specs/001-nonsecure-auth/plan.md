# Implementation Plan: NonSecure Auth

**Branch**: `001-nonsecure-auth` | **Date**: 2026-10-21 | **Spec**: [spec.md](./spec.md)

## Summary

MaviBank facade'i InterVpos NonSecure satışını karşılar: `POST /mpi/Default.aspx` form-urlencoded isteği
parse edilir → seed'li `Merchant`'a karşı kimlik doğrulanır (fail-closed) → `(ShopCode,OrderId)` idempotency
→ `Card.Scenario` sentinel'i çekim sonucunu belirler (onay/`51`/timeout; gerçek bakiye yok) → sonuç
`Transaction` olay akışına yazılır (`TransactionAuthorized`/`Declined`, `[AggregateHandler]`) → `;;` ayraçlı
cevap üretilir. Kart verisi saklanmaz (log'da maskeli, defterde maskeli). Tel InterVpos'la birebir (İlke V).

## Technical Context

**Language/Version**: C# / .NET 10 (Nullable+ImplicitUsings).
**Dependencies**: Critter Stack (Marten event store + Wolverine `[AggregateHandler]`/bus/outbox), Minimal
API, Scrutor. **Storage**: Marten/Postgres şema `mavibank` — `Transaction` event stream, `Merchant`/`Card`
düz doküman seed. **Testing**: xUnit saf domain. **Platform**: Aspire (Postgres 5434 + RabbitMQ).
**Constraints**: yalnız TL (949); PAN/CVV kalıcı ortamda yok (maskeli); cevap `;;` (JSON değil); determinizm.

## Constitution Check

- **I. BC İzolasyonu** ✅ MaviBank facade = tek BC, şema `mavibank`; paylaşım yalnız altyapı (Common). GEÇER.
- **II. Zengin Domain + ES** ✅ `Transaction` event stream (`[AggregateHandler]`, Apply); Merchant/Card düz
  doküman; `ChargeOutcome` VO; enum aggregate dosyasında. GEÇER.
- **III. VSA+CQRS** ✅ `Features/Commands/Authorize`; endpoint Minimal API; Repository yok (IDocumentSession).
  GEÇER.
- **IV. Result** ✅ domain `ResultDomain`/outcome-enum (`ChargeOutcome`); red = outcome, exception değil.
  (Tel cevap `;;` ProcReturnCode — Result iç model.) GEÇER.
- **V. Protokol Sadakati (NON-NEG)** ✅ istek form-urlencoded, cevap `;;`; alan/kod birebir; wire facade içi
  nested; `;;` encoder saf. GEÇER.
- **VI. Determinizm + Dış Bağımlılık Yasağı (NON-NEG)** ✅ sentinel kart → canned; rastgele yok; gerçek
  bakiye/SMS/kart-ağı yok. GEÇER.
- **VII. Protokol Kimliği + Fail-Closed** ✅ ShopCode/UserCode/UserPass seed'e karşı; eşleşmezse işlem
  doğmaz; MCP yok (Faz 3). GEÇER.
- **VIII. Kart/Sır Minimizasyonu** ✅ PAN/CVV log'da maskeli, defterde maskeli; SecretKey seed'de sır, cevapta
  yok. GEÇER.
- **IX. Spec-Driven + Domain-TDD** ✅ tam akış; encoder/Apply/sentinel/maskeleme test-first. GEÇER.
- **X. Domain Legibility** ✅ `FLOW.md` bu dilimde kurulur (ilk BC süreci). GEÇER.

**Sonuç: tüm kapılar geçer; Complexity Tracking boş.**

## Project Structure

```text
src/services/MaviBank.Api/
├ Domains/
│  ├ Transactions/
│  │  ├ Transaction.cs                    # event-stream aggregate (Apply + enum TransactionStatus)
│  │  ├ TransactionEvents.cs              # TransactionAuthorized / TransactionDeclined (record)
│  │  ├ ValueObjects/ChargeOutcome.cs     # normalize sonuç VO (private ctor + factory)
│  │  ├ ChargeScenarioEngine.cs           # sentinel Card.Scenario → ChargeOutcome (saf domain-service)
│  │  ├ Features/Commands/Authorize.cs    # kimlik + idempotency + sentinel + StartStream + sonuç
│  │  └ InterVpos/
│  │     ├ InterVposEndpointExtension.cs  # POST /mpi/Default.aspx: form parse → Authorize → `;;` encode
│  │     ├ InterVposWire.cs               # AuthRequestWire / AuthResponseWire (nested, transient)
│  │     └ SemicolonEncoder.cs            # saf `;;` encoder + PAN/CVV maskeleme helper
│  ├ Merchants/ { Merchant.cs, merchant-seed.json, MerchantSeeder.cs }
│  └ Cards/     { Card.cs (Scenario enum), card-seed.json, CardSeeder.cs }
├ Options/InterVposOptions.cs             # Lang/MOTO/Currency(949) sabitleri (handler literal yasak)
├ Constants/SchemaConstants.cs            # (var) mavibank
├ Program.cs                              # endpoint map + seeder çağrıları + Discovery.IncludeType gerekirse
└ FLOW.md                                 # MaviBank domain süreci (ilk)

tests/MaviBank.Api.Tests/
├ TransactionTests.cs · SemicolonEncoderTests.cs · ChargeScenarioTests.cs · MaskingTests.cs
```

**Structure Decision**: Tek facade düz yapı. `Transaction` event-stream aggregate; InterVpos wire +
endpoint + encoder `Domains/Transactions/InterVpos/` facade klasöründe nested (İlke V/VII). Merchant/Card
ayrı referans doküman klasörleri (davranışsız seed). Süreç-güdümlü Process/Saga YOK (Auth senkron).

## Phases (özet — tasks.md detaylandırır)

1. Setup: Options + Constants + seed JSON iskeletleri.
2. Foundational (domain-TDD): `;;` encoder + maskeleme, `ChargeOutcome` VO, `ChargeScenarioEngine`,
   `Transaction` olayları + Apply + idempotency — testler önce.
3. US1 (onay): Authorize handler + InterVpos endpoint + Merchant/Card seed → onay yolu uçtan-uca.
4. US2 (issuer red): decline dalı + `51` kodu.
5. US3 (kimlik hatası): fail-closed, işlem doğmaz.
6. Polish: FLOW.md + İlke VI/VIII grep denetimi + build/test + (topluca) canlı Aspire.

## Complexity Tracking

> İhlalsiz geçti — boş.
