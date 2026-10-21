# Phase 1 Data Model: NonSecure Auth

Yeni BC (MaviBank facade). Bir event-stream aggregate (`Transaction`) + iki seed'li düz doküman
(`Merchant`, `Card`) + transient wire VO'ları. Kart verisi kalıcı ortama girmez (İlke VIII).

## Transaction (aggregate — Marten event stream)

Append-only olay akışı; `[AggregateHandler]` ile türetilir. Faz 1'de yalnız Auth olayları; Void/Refund/
Capture Faz 2'de aynı stream'e eklenir.

**Olaylar (Faz 1):**
- `TransactionAuthorized(Guid Id, string ShopCode, string OrderId, string TransId, string AuthCode, decimal Amount, int Currency, int Installment, string MaskedPan, string CardType, DateTime At)`
- `TransactionDeclined(Guid Id, string ShopCode, string OrderId, string ProcReturnCode, string ErrorCode, string ErrorMessage, decimal Amount, string MaskedPan, DateTime At)`

**Türetilen durum (Apply sonrası):** `Status ∈ {Authorized, Declined}` (Faz 2: Captured/Voided/Refunded),
`ShopCode/OrderId/TransId/AuthCode/Amount/Currency/MaskedPan`.

**Değişmezler:**
- PAN/CVV İÇERMEZ (yalnız `MaskedPan`). `(ShopCode, OrderId)` tekil (idempotency).
- Red bir olaydır (silinmez); denetim/mutabakat için kalır. Onay/Red outcome event'tir, exception değil.

**Stream kimliği:** deterministik — `(ShopCode, OrderId)` → stream key (idempotent tekrar aynı stream'i bulur).

## Merchant (düz doküman — seed)

| Alan | Tip | Not |
|---|---|---|
| `ShopCode` | string | kimlik (id) |
| `UserCode` | string | POS kullanıcısı |
| `UserPass` | string | POS parolası (NonSecure kimlik) |
| `SecretKey` | string | hash anahtarı (3DS Faz 4; seed'de durur) — sır, log/dönüşte görünmez |
| `SubscriptionKey` | string | operasyon API (Faz 3) |

Seed: embedded JSON; startup `MerchantSeeder`. Store başına farklı credential.

## Card (düz doküman — seed test kartları)

| Alan | Tip | Not |
|---|---|---|
| `Pan` | string | test kartı numarası (id) |
| `Expiry` | string | "MMYY" |
| `Cvv` | string | test |
| `CardType` | int | 0=VISA,1=MC,2=AMEX,3=TROY |
| `Scenario` | enum | `Approve`/`InsufficientFunds`/`Timeout` (sentinel bayrağı) |

Seed: 4111..0001 Approve · 4111..0002 InsufficientFunds · 4111..0005 Timeout. Gerçek bakiye YOK.

## AuthRequestWire / AuthResponseWire (transient — facade nested, İlke V)

`Domains/.../InterVpos/` facade içinde nested; domain sınırını GEÇMEZ. Saklanmaz (İlke VIII).

- **AuthRequestWire** (form-urlencoded parse): `ShopCode/UserCode/UserPass/TxnType/SecureType/Pan/Expiry/
  Cvv2/PurchAmount/Currency/OrderId/InstallmentCount/CardType/MOTO/Lang`.
- **AuthResponseWire** (→ `;;` encode): `ProcReturnCode/OrderId/TransId/AuthCode/Response/ErrorCode/
  ErrorMessage/TRXDATE`.

## Çekim sonucu (normalize, iç)

`ChargeOutcome` VO (record + private ctor + factory): `IsApproved`, `ProcReturnCode`, `AuthCode?`,
`ErrorCode?`, `ErrorMessage?`. Sentinel motoru üretir; facade `;;` cevaba + Transaction olayına çevirir.

## İlişkiler

```
AuthRequestWire → (kimlik: Merchant doğrula) → (sentinel: Card.Scenario → ChargeOutcome)
   → Transaction olayı (Authorized/Declined, event stream, (ShopCode,OrderId) tekil)
   → AuthResponseWire → `;;` encode → istemci
PAN: wire'da var → log'da maskeli → Transaction'da maskeli (tam PAN hiçbir kalıcıda yok)
```

## Marten notu

`Transaction` event stream (`mt_events`, şema `mavibank`). `Merchant`/`Card` düz doküman (Pan/ShopCode = id);
startup seed (`ApplyAllDatabaseChangesOnStartup` açık). `(ShopCode,OrderId)` tekillik: stream key deterministik
türetme + idempotency kaydı. Defansif migration yok (dev).
