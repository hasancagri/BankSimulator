# Implementation Plan: Batch / Settlement + Void / Refund / StatusHistory

**Branch**: `002-batch-settlement-ops` | **Date**: 2026-10-21 | **Spec**: [spec.md](./spec.md)

## Summary

001 Auth üstüne işlem yaşam döngüsünü tamamlar: onaylı işlem açık **Batch**'e bağlanır; **gün sonu** (manuel
tetik) batch'i kapatır (komisyon/bloke/net → her işlem `Settled`), yeni batch açar; **Void** açık-batch
işlemini iptal eder (`Voided`, komisyon yok), **Refund** kapalı-batch işleme kısmi iade yapar (`RefundedAmount`
birikir), **StatusHistory** işlemin halini döner (in-doubt → PG çift-çekim emniyeti). Void/Refund/Status aynı
`/mpi/Default.aspx` ucuna TxnType ile gelir, `;;` cevap. Gün sonu manuel tetik ucu (dev; operasyon API Faz 3).

## Constitution Check

- **I/II/III/IV** ✅ Batch düz doküman + Transaction event stream genişler (Void/Refund/Settle olay); VSA
  slice (Void/Refund/StatusHistory/EndOfDay); Result/outcome-enum.
- **V Protokol Sadakati** ✅ Void/Refund/Status InterVpos TxnType'ları, `;;` cevap (orgOrderId, VoidDate,
  RefundedAmount — gerçek alanlar). GEÇER.
- **VI Determinizm + VIII Kart/Sır** ✅ komisyon deterministik (merchant oranı); PAN maskeli; gün sonu manuel
  (determinist test). GEÇER.
- **VII Protokol Kimliği** ✅ Void/Refund kimlik doğrular (fail-closed). GEÇER.
- **IX/X** ✅ domain-TDD (settlement hesabı, void/refund guard, batch aktif-tekil); FLOW.md güncellenir.

## Project Structure

```text
src/services/MaviBank.Api/
├ Domains/Transactions/
│  ├ Transaction.cs                      # DEĞİŞİR: yeni Apply (Settled/Voided/Refunded) + alanlar + status
│  ├ TransactionEvents.cs                # DEĞİŞİR: +Settled/Voided/Refunded; Authorized +BatchNo
│  ├ Features/Commands/
│  │  ├ Authorize.cs                      # DEĞİŞİR: onayda açık batch bul/aç + BatchNo + batch.Keys += 
│  │  ├ Void.cs                           # YENİ: Authorized → Voided + batch.Keys −=
│  │  ├ Refund.cs                         # YENİ: Settled → RefundedAmount (kısmi, aşım guard)
│  │  └ EndOfDay.cs                       # YENİ: açık batch kapat (komisyon/net/bloke) + yeni aç
│  ├ Features/Queries/StatusHistory.cs    # YENİ: tx durumu → VoidDate/RefundedAmount
│  └ InterVpos/
│     ├ InterVposEndpointExtension.cs     # DEĞİŞİR: TxnType dispatch (Auth/Void/Refund/StatusHistory)
│     └ (yeni response builder'lar)
├ Domains/Batches/
│  ├ Batch.cs                             # YENİ: düz doküman + enum BatchStatus + davranış (Open/Close/Add/Remove)
│  └ (aktif-tekil index Program.cs)
├ Domains/Merchants/{Merchant.cs,merchant-seed.json}  # DEĞİŞİR: CommissionRates/BlokeGun
├ Ops/EndOfDayEndpointExtension.cs        # YENİ: POST /ops/end-of-day manuel tetik (dev; operasyon API Faz 3)
└ Program.cs                              # DEĞİŞİR: Batch partial-unique index + endpoint map + Discovery

tests/MaviBank.Api.Tests/
├ BatchSettlementTests.cs · VoidTests.cs · RefundTests.cs · StatusHistoryTests.cs (domain-TDD)
```

**Structure Decision**: Batch = `Domains/Batches/` düz doküman (aktif-tekil açık batch; davranış metotlu —
Open/Close/Add/Remove, İlke II). Transaction event stream genişler. Void/Refund = Commands, StatusHistory =
Query, EndOfDay = Command (kullanıcı/operatör tetikli → `Features/`; gün-sonu manuel tetik). Gün-sonu
InterVpos değil ayrı ops ucu (operasyon yüzü Faz 3 formalize eder).

## Phases (tasks.md detaylandırır)

1. Foundational: Batch aggregate + Merchant komisyon alanları + Transaction yeni olaylar/Apply + Authorize'a
   batch bağlama (domain-TDD önce).
2. US1 Settlement (EndOfDay) + manuel tetik ucu.
3. US2 Void. 4. US3 Refund. 5. US4 StatusHistory. 6. Endpoint TxnType dispatch + Polish (FLOW.md/grep/build/test).

## Complexity Tracking

> İhlalsiz — boş.
