---
description: "Task list — Batch/Settlement + Void/Refund/StatusHistory"
---

# Tasks: Faz 2

**Input**: `/specs/002-batch-settlement-ops/`. Tüm yollar `src/services/MaviBank.Api/`.

**Tests**: Domain-TDD — settlement hesabı, void/refund guard, batch aktif-tekil, Apply test-first.

## Phase 1: Foundational

- [X] T001 [P] `Domains/Batches/Batch.cs` — düz doküman + `enum BatchStatus {Open,Closed}` + davranış
      (`OpenFor(shopCode)` factory, `AddTransaction(key)`, `RemoveTransaction(key)`, `Close(totals, eod)`);
      `TransactionKeys` + totals. ResultDomain döner (İlke II/IV).
- [X] T002 [P] `Domains/Transactions/TransactionEvents.cs` genişlet: `TransactionAuthorized` +`BatchNo`;
      yeni `TransactionSettled`/`TransactionVoided`/`TransactionRefunded`. `Transaction.cs`: yeni `Apply`'lar
      + alanlar (BatchNo/RefundedAmount/DeductionAmount/NetAmount/BlokeTarihi?/VoidDate?) + status
      {Authorized,Declined,Voided,Settled} + guard metotları (`CanVoid`/`CanRefund`).
- [X] T003 [P] `Domains/Merchants/Merchant.cs` + `merchant-seed.json`: `CommissionRates`(taksit→oran) +
      `DefaultCommissionRate` + `BlokeGun`.
- [X] T004 `Program.cs`: `Batch` `(ShopCode)` partial-unique index predicate `Status=Open` (aktif-tekil).

### Domain-TDD testleri (önce)
- [X] T005 [P] `tests/BatchSettlementTests.cs`: gün sonu → Authorized→Settled; Deduction=round(Gross×oran,2)/
      Net/Bloke; batch Close + totals. `tests/VoidTests.cs`: Authorized→Voided; Settled→red.
      `tests/RefundTests.cs`: Settled kısmi birikim; aşım red; Authorized→red. `tests/StatusHistoryTests.cs`.

## Phase 2: US1 — Gün sonu / Settlement (P1)
- [X] T006 [US1] `Domains/Transactions/Features/Commands/Authorize.cs` DEĞİŞİR: onayda açık batch bul/aç
      (`Batch` aktif-tekil) → `batch.AddTransaction(key)` + store → `TransactionAuthorized(BatchNo=...)`.
- [X] T007 [US1] `Domains/Transactions/Features/Commands/EndOfDay.cs` YENİ: açık batch → her TransactionKey:
      Authorized tx → komisyon/net/bloke hesap → `TransactionSettled` append → batch `Close(totals)` → yeni
      `Batch.OpenFor` (bağımlı: T001-T003, T006).
- [X] T008 [US1] `Ops/EndOfDayEndpointExtension.cs` YENİ: `POST /ops/end-of-day` (JSON {shopCode}) → EndOfDay
      → özet JSON (batchNo/settledCount/totals). Dev tetik (operasyon API Faz 3).

## Phase 3: US2 — Void (P2)
- [X] T009 [US2] `Domains/Transactions/Features/Commands/Void.cs` YENİ: kimlik → tx `CanVoid` (Authorized) →
      `TransactionVoided` + açık batch `RemoveTransaction(key)`; değilse red (`05`, gün sonu geçti).

## Phase 4: US3 — Refund (P2)
- [X] T010 [US3] `Domains/Transactions/Features/Commands/Refund.cs` YENİ: kimlik → tx `CanRefund` (Settled &
      RefundedAmount+amount ≤ Amount) → `TransactionRefunded`; değilse/aşım red.

## Phase 5: US4 — StatusHistory (P2)
- [X] T011 [US4] `Domains/Transactions/Features/Queries/StatusHistory.cs` YENİ: tx yükle → durum/RefundedAmount/
      VoidDate; (ShopCode,OrderId) yoksa bulunamadı.

## Phase 6: Endpoint dispatch + Polish
- [X] T012 `InterVpos/InterVposEndpointExtension.cs` DEĞİŞİR: `TxnType` dispatch (Auth/Void/Refund/
      StatusHistory) + TxnType başına `;;` response builder (contracts/ops-and-txn.md).
- [X] T013 `Program.cs`: ops endpoint map + yeni nested `*Handler`'lar için `Discovery.IncludeType`.
- [X] T014 `FLOW.md` güncelle (yeni adımlar: batch/gün-sonu/void/refund/status; anchor `Batch`, `EndOfDay`,
      `Void`, `Refund`, `StatusHistory`).
- [X] T015 İlke VI/VIII grep + `dotnet build` (0) + `dotnet test` (001 + Faz 2 yeşil).
- [ ] T016 Canlı Aspire E2E (quickstart; **tüm geliştirme bitince topluca**).

## Dependencies
- Foundational (T001-T005) → US1 (T006-T008; Authorize batch bağlama foundational-near) → US2/US3/US4 →
  endpoint dispatch (T012) → polish. Kritik: `Transaction.cs` T002 → her şey; `Authorize.cs` 001→T006;
  `InterVposEndpointExtension` 001→T012.
