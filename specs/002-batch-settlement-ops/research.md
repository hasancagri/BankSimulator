# Phase 0 Research: Batch / Settlement + Void / Refund / Status

Kararlar gereksinim §3.2/§6/§8 + Ders 12-14 + 001 altyapısından türetildi.

## D1 — Batch = düz doküman, aktif-tekil açık batch
Batch bir koordinasyon kaydıdır (işlem defteri değil) → düz Marten doküman. Merchant başına tek AÇIK batch:
`(ShopCode)` partial-unique predicate `Status=Open` (089 PaymentLink aktif-tekil deseni). Alternatif event-
stream batch → YAGNI (basit open/close + totals).

## D2 — Void/Refund/Settle = Transaction stream olayları
001 Transaction event stream'e append edilir (append-only, audit). Void/Refund/Settle silmez, olay ekler.
Durum olaylardan türetilir (Apply). İlke II/VIII.

## D3 — Settlement, Batch.TransactionKeys üzerinden gezer (projeksiyon yok)
"Açık batch'teki Authorized işlemler" sorgusu için event-stream'e projeksiyon gerekir; dev basitliği için
Batch dokümanı işlem stream anahtarlarını (`ShopCode:OrderId`) tutar (Auth ekler, Void çıkarır). Settlement
bu listeyi gezip her stream'e `TransactionSettled` append eder. Projeksiyon/read-model sonraki faz (raporlar).

## D4 — Void açık-batch / Refund kapalı-batch ayrımı = durum guard'ı
§8 kuralı Transaction durumuyla kodlanır: Void yalnız `Authorized` (açık batch), Refund yalnız `Settled`
(kapalı batch). Batch Status'u işlem durumuna yansır → ayrı batch-sorgusu gerekmez (guard aggregate'te).

## D5 — Komisyon basit (taksit→oran + bloke gün); tam matris sonra
Merchant `CommissionRates` (taksit→oran) + `DefaultCommissionRate` + `BlokeGun`. `Deduction = round(Gross ×
oran, 2)`. Tam matris (kart tipi/MCC/ciro/on-us — Ders 9) sonraki faz. İlke VI determinizm (oran config).

## D6 — Gün sonu manuel tetik (dev uç); zamanlanmış seam
`POST /ops/end-of-day` dev tetiği (determinist test + demo; §4 "manuel uç/CLI"). Zamanlanmış kesim (23:50)
Process/timer seam — sonraki. Operasyon API (JSON zarf + SubscriptionKey, rapor uçları) Faz 3.

## D7 — Endpoint TxnType dispatch
`/mpi/Default.aspx` artık TxnType'a göre yönlendirir (Auth/Void/Refund/StatusHistory) — gerçek InterVpos tek
uç + TxnType deseni. Her TxnType kendi slice'ını çağırır (`InvokeAsync`), cevabı `;;` sıralı kurulur.
