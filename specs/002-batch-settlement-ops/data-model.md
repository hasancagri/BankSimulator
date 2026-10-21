# Phase 1 Data Model: Batch / Settlement + Void / Refund / Status

001 `Transaction` event stream genişler + yeni `Batch` düz doküman + `Merchant` komisyon alanları.

## Batch (YENİ — düz Marten doküman)

Merchant başına tek AÇIK batch (aktif-tekil). Settlement onu kapatır, yenisini açar.

| Alan | Tip | Not |
|---|---|---|
| `BatchNo` | string | kimlik (id); `{ShopCode}-{guid8}` |
| `ShopCode` | string | sahip merchant |
| `Status` | `BatchStatus` | Open / Closed |
| `OpenedAt` | DateTime | |
| `ClosedAt` | DateTime? | settlement'ta |
| `EndOfDayDate` | DateTime? | kapanış günü |
| `TransactionKeys` | List&lt;string&gt; | batch'teki işlem stream anahtarları (`ShopCode:OrderId`); Auth ekler, Void çıkarır |
| `GrossTotal`/`DeductionTotal`/`NetTotal` | decimal | kapanışta hesaplanır |

Aktif-tekil: `(ShopCode)` üzerinde partial unique, predicate `Status=Open` (089 PaymentLink deseni).
`enum BatchStatus { Open, Closed }` (Batch.cs içinde).

## Transaction (GENİŞLER — event stream)

**Yeni olaylar:**
- `TransactionSettled(BatchNo, DeductionAmount, NetAmount, DateTime BlokeTarihi, DateTime At)`
- `TransactionVoided(DateTime At)`
- `TransactionRefunded(decimal Amount, decimal RefundedTotal, DateTime At)`

**TransactionAuthorized GENİŞLER:** `BatchNo` eklenir (işlem hangi açık batch'e bağlı).

**Yeni durum + alanlar:** `enum TransactionStatus { Authorized, Declined, Voided, Settled }`;
`BatchNo`, `RefundedAmount`, `DeductionAmount`, `NetAmount`, `BlokeTarihi?`, `VoidDate?`.

**Durum geçişi:**
```
Authorized ─Void→ Voided (terminal; açık batch, komisyon yok)
Authorized ─[gün sonu]→ Settled (Deduction/Net/Bloke)
Settled ─Refund(kısmi)→ Settled (RefundedAmount += ; ≤ Amount)
```

**Değişmezler:** Void yalnız Authorized; Refund yalnız Settled + RefundedAmount ≤ Amount; her geçiş olaydır
(silinmez). PAN taşımaz (MaskedPan).

## Merchant (GENİŞLER — seed)

| Yeni alan | Tip | Not |
|---|---|---|
| `CommissionRates` | Dictionary&lt;int,decimal&gt; | taksit → oran (ör. {1:0.0179, 3:0.0219}) |
| `DefaultCommissionRate` | decimal | tabloda yoksa (ör. 0.0179) |
| `BlokeGun` | int | valör gün sayısı (ör. 1) |

Settlement: `rate = CommissionRates.GetValueOrDefault(installment, DefaultCommissionRate)`;
`Deduction = round(Gross × rate, 2)`; `Net = Gross − Deduction`; `BlokeTarihi = ClosedAt + BlokeGun gün`.

## Akış

```
Auth(onay)  → açık batch bul/aç → batch.TransactionKeys += key → TransactionAuthorized(BatchNo)
Void        → tx Authorized? → TransactionVoided + batch.TransactionKeys −= key   (değilse red)
EndOfDay    → açık batch → her key: tx Authorized → hesap → TransactionSettled; batch Closed+totals; yeni batch Open
Refund      → tx Settled? & RefundedAmount+amount ≤ Amount → TransactionRefunded  (değilse red)
StatusHistory → tx yükle → durum/RefundedAmount/VoidDate `;;`   (yoksa bulunamadı)
```

## Marten notu

`Batch` düz doküman (BatchNo id), `(ShopCode)` partial-unique predicate `Status=Open`. `Transaction` event
stream (001, StreamIdentity.AsString). Void/Refund/Settle aynı stream'e append. Settlement, Batch.TransactionKeys
üzerinden işlem stream'lerini gezer (projeksiyon yerine — dev basitliği). Defansif migration yok.
