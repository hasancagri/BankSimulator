---
description: "Task list — NonSecure Auth (MaviBank InterVpos)"
---

# Tasks: NonSecure Auth

**Input**: `/specs/001-nonsecure-auth/` (plan.md, spec.md, research.md, data-model.md, contracts/)

**Tests**: Domain-TDD (İlke IX) — saf domain (`;;` encoder+maskeleme, `ChargeOutcome`, `ChargeScenarioEngine`,
`Transaction` Apply+idempotency) test-first. Handler/endpoint/seeder test-sonra/canlı.

**Organization**: US bazlı. Öncelik: US1 (P1) → US2 (P2) → US3 (P2). Tüm yollar `src/services/MaviBank.Api/`.

---

## Phase 1: Setup

- [X] T001 [P] `Options/InterVposOptions.cs` (Lang vars. `tr`, MOTO `0`, Currency `949`; handler literal yasak)
- [X] T002 [P] `Domains/Transactions/InterVpos/InterVposConstants.cs` (ProcReturnCode `00`/`51`/`99`,
      TxnType `Auth`, SecureType `NonSecure`, Response Approved/Declined/Error)
- [X] T003 [P] `Domains/Merchants/merchant-seed.json` (MAVI0001 + UserCode/UserPass/SecretKey=TEST_*/SubscriptionKey)
- [X] T004 [P] `Domains/Cards/card-seed.json` (4111..0001 Approve · 4111..0002 InsufficientFunds · 4111..0005 Timeout)
- [X] T005 `*.csproj`: seed JSON'ları EmbeddedResource (090 deseni; Content Remove + EmbeddedResource Include)

---

## Phase 2: Foundational (TÜM story'leri bloklar)

### Domain-TDD testleri (önce, FAIL etmeli)

- [X] T006 [P] `tests/SemicolonEncoderTests.cs`: sıralı `key=value;;...` birebir; `Pan`/`Cvv2` maskeleme
- [X] T007 [P] `tests/ChargeScenarioTests.cs`: sentinel Pan → `ChargeOutcome` (Approve→00+authCode /
      InsufficientFunds→51 / Timeout); bilinmeyen kart → ? (varsayılan red belgeli)
- [X] T008 [P] `tests/TransactionTests.cs`: `Apply(Authorized)`→Status=Authorized+alanlar; `Apply(Declined)`→
      Declined; MaskedPan (tam PAN yok); stream key `(ShopCode,OrderId)` deterministik

### Implementasyon

- [X] T009 [P] `Domains/Transactions/InterVpos/SemicolonEncoder.cs` (saf `;;` encode + `Mask(pan)`/`MaskCvv` helper)
- [X] T010 [P] `Domains/Transactions/ValueObjects/ChargeOutcome.cs` (private ctor + `Approved`/`Declined` factory)
- [X] T011 [P] `Domains/Transactions/TransactionEvents.cs` (`TransactionAuthorized`/`TransactionDeclined` record) +
      `Domains/Transactions/Transaction.cs` (`Apply` metotları + `enum TransactionStatus`; PAN taşımaz)
- [X] T012 `Domains/Transactions/ChargeScenarioEngine.cs` (`IScopedDependency`/saf; `Card.Scenario` → `ChargeOutcome`;
      TransId/AuthCode üretimi — deterministik-ish ama benzersiz; bağımlı: T010)
- [X] T013 [P] `Domains/Merchants/Merchant.cs` + `MerchantSeeder.cs`; `Domains/Cards/Card.cs` (+`enum CardScenario`) +
      `CardSeeder.cs` (embedded JSON → upsert, 090 BinCardSeeder deseni)
- [X] T014 `Program.cs`: Marten `Events.StreamIdentity = StreamIdentity.AsString` (stream key `ShopCode:OrderId`) +
      Merchant/Card `Identity` + startup seeder çağrıları (bağımlı: T011, T013)

**Checkpoint**: `;;` encoder + outcome + sentinel + Transaction olayları + seed hazır.

---

## Phase 3: US1 — Geçerli kartla onay (P1) 🎯 MVP

- [X] T015 [US1] `Domains/Transactions/Features/Commands/Authorize.cs`: `Authorize` komut +
      `AuthorizeHandler` (`[Transactional]`): Merchant doğrula → `(ShopCode,OrderId)` idempotency (mevcut
      stream varsa aynı sonucu döndür) → `ChargeScenarioEngine` → onayda `StartStream<Transaction>(key,
      TransactionAuthorized)` → `ChargeOutcome` döner (bağımlı: T010-T014)
- [X] T016 [US1] `Domains/Transactions/InterVpos/InterVposWire.cs`: `AuthRequestWire`/`AuthResponseWire`
      (nested, transient; form alanları ↔ record)
- [X] T017 [US1] `Domains/Transactions/InterVpos/InterVposEndpointExtension.cs`: `POST /mpi/Default.aspx` —
      form-urlencoded parse → `AuthRequestWire` → `Authorize` (`InvokeAsync`) → `ChargeOutcome` →
      `AuthResponseWire` → `SemicolonEncoder` → `text/plain` `;;` cevap; ham log (PAN/CVV maskeli)
- [X] T018 [US1] `Program.cs`: endpoint map + gerekirse `opts.Discovery.IncludeType(typeof(Authorize.AuthorizeHandler))`

**Checkpoint**: Onay yolu uçtan uca (`00` + TransId + AuthCode + stream + maskeli log).

---

## Phase 4: US2 — İssuer reddi (P2)

- [X] T019 [US2] `Authorize.cs`: declined dalı — `StartStream(TransactionDeclined, 51)`; endpoint decline
      `;;` cevabı (`ProcReturnCode=51`+ErrorCode/ErrorMessage); kart verisi/sır dönmez (bağımlı: T015)

**Checkpoint**: Red akışı; defterde Declined; link terminale geçmez.

---

## Phase 5: US3 — Geçersiz kimlik (P2)

- [X] T020 [US3] `Authorize.cs` + endpoint: kimlik eşleşmezse işlem DOĞMADAN `ProcReturnCode=99` (Error);
      Transaction açılmaz (fail-closed, İlke VII) (bağımlı: T015)

**Checkpoint**: Kimlik hatası işlem defterini değiştirmez.

---

## Phase 6: Polish & Çapraz Kesit

- [X] T021 `src/services/MaviBank.Api/FLOW.md` oluştur (MaviBank domain süreci: istek→kimlik→sentinel→
      Transaction olayı→`;;` cevap; kenar-anchor `Transaction`, `ChargeScenarioEngine`, `SemicolonEncoder`,
      `Authorize`); guard script (varsa) yeşil
- [X] T022 İlke VI/VIII denetimi: tam PAN/CVV + SecretKey hiçbir kalıcı ortamda (DB/log/cevap/trace) —
      grep + inceleme (ham log maskeli doğrula)
- [X] T023 `dotnet build` (0 hata) + `dotnet test` (domain testleri yeşil)
- [ ] T024 Canlı Aspire E2E (quickstart.md; **tüm geliştirme bitince topluca**)

---

## Dependencies & Execution Order

- Setup → Foundational → US1 (MVP) → US2/US3 (Authorize genişletir, T019/T020 bağımlı T015) → Polish.
- **Kritik dosya (sıralı):** `Authorize.cs`: T015 → T019 → T020. `Program.cs`: T014 → T018. `Transaction.cs`: T011 → (kullanım).
- **[P] fırsatları:** Setup T001-T004; Foundational testleri T006-T008; impl T009-T011, T013.

## Notes

- Kart verisi: command `InvokeAsync`-only; PAN/CVV saklanmaz/outbox'a girmez; ham log maskeli (İlke VI/VIII).
- Cevap `text/plain` `;;` (JSON serializer DEVREDE DEĞİL — İlke V). Alan sırası + kodlar InterVpos birebir.
- Timeout sentinel davranışı belgeli canned sonuç (gerçek gecikme opsiyonel); PG in-doubt sınaması için.
