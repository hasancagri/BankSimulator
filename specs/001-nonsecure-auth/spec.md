# Feature Specification: NonSecure Auth (Satış) — InterVpos Ödeme Yüzü

**Feature Branch**: `001-nonsecure-auth`

**Created**: 2026-10-21

**Status**: Draft

**Input**: BankSimulator ilk dikey dilim — MaviBank facade'i InterVpos protokolüyle NonSecure (3DS'siz)
satış (`Auth`) çekimini karşılar; PaymentGateway adapter'ının ve mews/pos InterPos gateway'inin bağlanacağı
ilk uç. Kapsam onaylı plan + EventStorming Storm 2 (banka-içi) ile kilitlendi.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Geçerli kartla NonSecure satış onaylanır (Priority: P1) 🎯 MVP

Üye-işyeri (PG adapter'ı aracılığıyla) müşterinin kart bilgisini ve tutarı InterVpos ödeme ucuna gönderir;
kimlik geçerli ve kart onay senaryosundaysa banka çekimi onaylar, bir işlem kaydı doğar ve `;;` ayraçlı
onay cevabı (onay kodu + işlem referansıyla) döner.

**Why this priority**: Bu, simülatörün tüm varlık sebebidir — bir ağ geçidinin karşısında gerçek bir
NonSecure satışı taklit etmek. Onay akışı olmadan hiçbir şey test edilemez; en ince uçtan-uca değer budur.

**Independent Test**: Seed'li geçerli merchant + onay test-kartıyla `POST /mpi/Default.aspx` (TxnType=Auth,
SecureType=NonSecure) → cevap `ProcReturnCode=00;;OrderId=..;;TransId=..;;AuthCode=..`; işlem defterinde
bir `TransactionAuthorized` olayı; cevap JSON değil `;;` ayraçlı.

**Acceptance Scenarios**:

1. **Given** seed'li geçerli merchant + onay kartı, **When** TxnType=Auth NonSecure istek gelir,
   **Then** `ProcReturnCode=00`, benzersiz `TransId` + `AuthCode` döner, defterde `TransactionAuthorized`.
2. **Given** aynı (ShopCode, OrderId) ile ikinci bir Auth, **When** istek tekrar gelir, **Then** ikinci
   işlem DOĞMAZ; ilk sonucun aynısı döner (idempotent — çift çekim önlenir).
3. **Given** cevap üretilir, **When** istemci okur, **Then** gövde `application/x-www-form-urlencoded`
   değil, `key=value;;...` ayraçlı; alan adları ve `00` kodu InterVpos'la birebir.

---

### User Story 2 - İssuer reddi (limit yetersiz) (Priority: P2)

Red senaryosundaki test-kartıyla Auth gelir; banka çekimi reddeder, işlem onaylanmaz ama red bir deneme
kaydı olarak defterde kalır ve red kodu `;;` cevabında döner.

**Why this priority**: Red yolu olmadan ağ geçidinin hata taksonomisi + decline-retry davranışı test
edilemez. Onaydan sonraki ilk zorunlu davranış.

**Independent Test**: "Limit yetersiz" sentinel kartıyla Auth → `ProcReturnCode=51` (+ ErrorCode/
ErrorMessage); onay değil; defterde `TransactionDeclined`; link/işlem terminale onayla GEÇMEZ.

**Acceptance Scenarios**:

1. **Given** limit-yetersiz sentinel kartı, **When** Auth gelir, **Then** `ProcReturnCode=51`, onay YOK,
   defterde `TransactionDeclined` (red sınıfı taşır).
2. **Given** red döndü, **When** istemci okur, **Then** cevap kart verisi / banka sırrı İÇERMEZ.

---

### User Story 3 - Geçersiz kimlik işlem doğurmadan reddedilir (Priority: P2)

ShopCode/UserCode/UserPass seed'li merchant'la eşleşmezse istek kart'a dokunulmadan reddedilir; hiçbir
işlem kaydı doğmaz.

**Why this priority**: Fail-closed yetki (İlke VII) — kimlik hatası bir "çekim denemesi" değildir; işlem
akışına hiç girmemelidir. Gerçek POS davranışı budur.

**Independent Test**: Yanlış UserPass ile Auth → kimlik hatası cevabı (ErrorCode/ErrorMessage); defterde
HİÇBİR işlem doğmaz.

**Acceptance Scenarios**:

1. **Given** yanlış ShopCode/UserCode/UserPass, **When** Auth gelir, **Then** kimlik hatası döner ve
   işlem defteri DEĞİŞMEZ (Transaction açılmaz).

---

### Edge Cases

- **Timeout sentinel kartı:** determinizm için "zaman aşımı" senaryolu bir test kartı tanımlanır; bankanın
  cevap üretmediği/geciktiği durumu taklit eder (PG tarafındaki in-doubt davranışını sınamak için). İlk
  dilimde davranışı belgeli bir sentinel sonucu olarak sağlanır; gerçek ağ gecikmesi simülasyonu opsiyonel.
- **Desteklenmeyen para birimi:** v1 yalnız TL (949); başka para birimi reddedilir (red kodu).
- **Eksik/bozuk zorunlu alan** (Pan/PurchAmount/OrderId yok): işlem doğmadan biçim hatası.
- **Ham log + PAN:** ham istek/cevap loglanır ama PAN ve CVV **maskelenir** (İlke VI/VIII ile İlke "ham log"
  gerilimi böyle çözülür — tel şekli görünür, kart sırrı görünmez).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem `POST /mpi/Default.aspx` ucunda `TxnType=Auth` + `SecureType=NonSecure` isteğini
  `application/x-www-form-urlencoded` gövdeyle kabul etmelidir.
- **FR-002**: Sistem her isteği seed'li `Merchant` kaydına karşı `ShopCode`/`UserCode`/`UserPass` ile
  doğrulamalı; eşleşmezse işlem DOĞURMADAN kimlik hatası döndürmelidir (fail-closed).
- **FR-003**: Sistem çekim sonucunu **sentinel test-kartı** ile belirlemelidir (onay / limit-yetersiz /
  timeout); sonuç deterministiktir, gerçek bakiye/limit hesabı YOKTUR.
- **FR-004**: Onayda sistem benzersiz `TransId` + `AuthCode` üretmeli ve `TransactionAuthorized` olayını
  defterine yazmalı; redde `TransactionDeclined` (red kodu/sınıfıyla) yazmalıdır.
- **FR-005**: Sistem işlem defterini **olay akışı** (append-only) olarak tutmalı; `OrderId`, `(ShopCode,
  OrderId)` kapsamında tekil olmalıdır.
- **FR-006**: Sistem cevabı **`;;` ayraçlı `key=value`** biçiminde üretmelidir (JSON DEĞİL); alan adları,
  `ProcReturnCode` değerleri (`00`=onay, `51`=red ...) ve biçim InterVpos protokolüyle BİREBİR olmalıdır.
- **FR-007**: Aynı `(ShopCode, OrderId)` ile tekrarlanan Auth, ikinci bir işlem DOĞURMAMALI; ilk sonucun
  aynısını döndürmelidir (idempotency — çift çekim önleme).
- **FR-008**: Sistem her istek ve cevabı ham haliyle loglamalı; ANCAK `Pan` ve `Cvv2` alanları
  maskelenmelidir (tam PAN/CVV hiçbir kalıcı ortamda görünmez).
- **FR-009**: Sistem yalnız TL (ISO numerik `949`) işlemi desteklemelidir; başka para birimi reddedilir.
- **FR-010**: İşlem kaydı tam kart numarası (PAN) / CVV İÇERMEMELİ; yalnız maskeli gösterim + referans tutar.

### Key Entities

- **Merchant**: Üye-işyeri protokol kimliği — `ShopCode`, `UserCode`, `UserPass`, `SecretKey`,
  `SubscriptionKey` + (ileride) komisyon/bloke. Seed'li düz kayıt; store başına farklı credential.
- **Card**: Test kartı — `Pan`, `Expiry`, `Cvv`, `CardType` + **senaryo bayrağı** (onay/limit-yetersiz/
  timeout). Seed'li; gerçek bakiye taşımaz.
- **Transaction**: İşlem defteri kaydı — olay akışı (`TransactionAuthorized`/`TransactionDeclined`);
  `OrderId`/`TransId`/`AuthCode`/tutar/para birimi/taksit/durum/zaman; PAN SAKLANMAZ (maskeli).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Onay test-kartıyla NonSecure Auth → `ProcReturnCode=00` + benzersiz `TransId`/`AuthCode`;
  defterde tam 1 `TransactionAuthorized` olayı.
- **SC-002**: Cevap biçimi InterVpos'la birebir (`;;` ayraçlı, JSON değil; alan adları + kod değerleri
  gerçek protokolle aynı) — nihai kabul (Faz 5) mews/pos InterPos bağlanabilirliğinin ön koşulu.
- **SC-003**: Determinizm — aynı (merchant, kart, tutar, OrderId) girdisi her çağrıda aynı cevabı üretir.
- **SC-004**: Tam PAN ve CVV hiçbir kalıcı ortamda (işlem defteri, DB, log) görünmez (yalnız maskeli).
- **SC-005**: Geçersiz kimlikli istek işlem defterini hiç değiştirmez (fail-closed).

## Assumptions

- Merchant ve test kartları **seed'lidir** (onboarding modülü Faz 7 — bu dilimde yok; kayıtlar hazır varsayılır).
- NonSecure'de **hash doğrulama opsiyoneldir** (3DS hash + çift-yönlü imza Faz 4); bu dilim kimliği
  ShopCode/UserCode/UserPass ile doğrular.
- Gerçek bakiye/limit, batch/gün-sonu, Void/Refund/StatusHistory, operasyon API, 3DS ve onboarding
  **kapsam DIŞIDIR** (sonraki fazlar).
- Depolama Marten/Postgres + Aspire (constitution Teknoloji bölümü); `Transaction` olay akışı.
- PaymentGateway adapter'ı ve mews/pos InterPos gateway'i bu ucun nihai tüketicileridir (tel uyumluluğu ölçüt).
