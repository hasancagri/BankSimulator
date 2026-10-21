# Contract: Void / Refund / StatusHistory (InterVpos) + Gün Sonu

## InterVpos — `POST /mpi/Default.aspx` (TxnType ile yönlendirme)

İstek form-urlencoded; cevap `;;` ayraçlı (İlke V). Kimlik ShopCode/UserCode/UserPass (Void/Refund).

### TxnType=Void
İstek: `ShopCode/UserCode/UserPass` + `TxnType=Void` + `OrderId` (orgOrderId). (Pan yok — orgOrderId referans.)
```
Onay : ProcReturnCode=00;;Response=Approved;;OrderId=ORD-1;;TRXDATE=...
Red  : ProcReturnCode=05;;Response=Declined;;OrderId=ORD-1;;ErrorCode=05;;ErrorMessage=Gun sonu gecti, iptal edilemez;;TRXDATE=...
```

### TxnType=Refund
İstek: `...` + `TxnType=Refund` + `OrderId` + `PurchAmount` (kısmi tutar).
```
Onay : ProcReturnCode=00;;Response=Approved;;OrderId=ORD-1;;RefundedAmount=30.00;;TRXDATE=...
Red  : ProcReturnCode=05;;Response=Declined;;OrderId=ORD-1;;ErrorCode=05;;ErrorMessage=Iade edilemez;;TRXDATE=...
```

### TxnType=StatusHistory
İstek: `...` + `TxnType=StatusHistory` + `OrderId`.
```
Bulundu     : ProcReturnCode=00;;OrderId=ORD-1;;TxnStat=Settled;;VoidDate=;;RefundedAmount=30.00;;TRXDATE=...
Bulunamadı  : ProcReturnCode=99;;Response=Error;;ErrorCode=NOTFOUND;;ErrorMessage=Islem bulunamadi;;TRXDATE=...
```
`TxnStat` = Transaction durumu (Authorized/Settled/Voided). Cevap kart verisi/sır İÇERMEZ.

## Gün Sonu — `POST /ops/end-of-day` (dev manuel tetik)

İstek: `{ "shopCode": "MAVI0001" }` (JSON). Açık batch'i kapatır, settle eder, yeni açar.
```
{ "batchNo": "MAVI0001-ab12cd34", "settledCount": 2, "grossTotal": 250.00, "deductionTotal": 4.48, "netTotal": 245.52 }
```
Not: InterVpos değil — dev operatör tetiği. Formal operasyon API (GetPosBatchSummary vb. JSON zarf +
SubscriptionKey) Faz 3. Zamanlanmış kesim (Process/timer) seam.

## İnvariant

- Void yalnız açık-batch (Authorized); Refund yalnız kapalı-batch (Settled), RefundedAmount ≤ Amount.
- Settlement: `Deduction = round(Gross × oran(taksit), 2)`, `Net = Gross − Deduction`, `Bloke = kapanış + BlokeGun`.
- Tüm InterVpos cevapları `;;`; sır/kart verisi dönmez.
