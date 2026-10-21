# Quickstart / Doğrulama: Faz 2

Canlı adımlar tüm geliştirme bitince topluca.

## Birim test (domain-TDD)
```bash
dotnet test
```
Beklenen (yeni): `BatchSettlementTests` (gün sonu: Authorized→Settled, Deduction/Net/Bloke hesabı, batch
Close+yeni Open), `VoidTests` (Authorized→Voided; Settled→red), `RefundTests` (Settled kısmi birikim; aşım
red; Authorized→red), `StatusHistoryTests` (durum/RefundedAmount/VoidDate; bulunamadı). 001 testleri bozulmaz.

## Build
```bash
dotnet build
```

## Canlı Aspire (ileride, topluca)
1. Auth (001) → işlem açık batch'te.
2. `POST /ops/end-of-day {shopCode}` → batch Closed, işlem Settled (net/komisyon), yeni batch Open.
3. `POST /mpi/Default.aspx TxnType=Refund OrderId PurchAmount` → RefundedAmount artar; aşan → red.
4. Settlement ÖNCESİ `TxnType=Void OrderId` → Voided; settlement SONRASI Void → red.
5. `TxnType=StatusHistory OrderId` → güncel durum + RefundedAmount/VoidDate.

## İlke denetimi
- Cevaplar `;;` (İlke V); kart verisi/sır dönmez; PAN maskeli (İlke VI/VIII).
- Void/Refund kimlik doğrular (fail-closed).
