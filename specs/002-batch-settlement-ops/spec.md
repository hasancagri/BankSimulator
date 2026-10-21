# Feature Specification: Batch / Gün Sonu + Void / Refund / StatusHistory

**Feature Branch**: `002-batch-settlement-ops`

**Created**: 2026-10-21

**Status**: Draft

**Input**: BankSimulator Faz 2 — işlem yaşam döngüsünün geri kalanı: açık/kapalı **batch** + **gün sonu
settlement** (komisyon/bloke/net) + **Void** (iptal, açık batch) + **Refund** (iade, kapalı batch, kısmi) +
**StatusHistory** (durum sorgu / in-doubt). Gereksinim §3.2/§6/§8 + Ders 12-14. 001 Auth üstüne inşa.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Gün sonu: açık batch kapanır, komisyon/net hesaplanır (Priority: P1) 🎯

Onaylı işlemler bir **açık batch**'e yazılır. Gün sonu (manuel tetik) açık batch'i kapatır: her işlem için
komisyon (taksit oranından) düşülür, net tutar + bloke/valör tarihi hesaplanır, yeni açık batch açılır.

**Why this priority**: Void (açık batch) ve Refund (kapalı batch) ayrımı batch yaşam döngüsüne dayanır;
settlement onu kuran motordur. Para akışının "brüt − komisyon = net" dersi burada kodlanır.

**Independent Test**: Onaylı 2 işlem → açık batch'te; gün sonu tetiği → batch Closed, her işlem Settled
(DeductionAmount = Gross × oran, NetAmount, BlokeTarihi = kapanış + bloke gün), yeni açık batch doğar.

**Acceptance Scenarios**:
1. **Given** açık batch'te onaylı işlem, **When** gün sonu tetiklenir, **Then** işlem `Settled`
   (Deduction/Net/BlokeTarihi dolu), batch `Closed`, yeni batch `Open`.
2. **Given** batch'te hiç işlem yok, **When** gün sonu, **Then** batch kapanır (boş), yeni açılır (no-op güvenli).

---

### User Story 2 - Void: açık batch işlemini iptal (Priority: P2)

Henüz gün sonu görmemiş (açık batch) onaylı işlem iz bırakmadan ters çevrilir — para hareketi yoktu,
komisyon doğmaz, limit anında açılır.

**Independent Test**: Onaylı (açık batch) işleme `TxnType=Void` (orgOrderId) → `Voided`; kapalı batch
işlemine Void → red (gerçekçi hata).

**Acceptance Scenarios**:
1. **Given** açık batch'te onaylı işlem, **When** Void, **Then** `Voided` (terminal), komisyon yok.
2. **Given** settled (kapalı batch) işlem, **When** Void, **Then** red `ProcReturnCode≠00` ("iptal edilemez,
   gün sonu geçti").

---

### User Story 3 - Refund: kapalı batch işlemine iade (kısmi dahil) (Priority: P2)

Gün sonu görmüş (settled) işleme ters yönde iade — kısmi olabilir, `RefundedAmount` birikir, toplam tutarı
aşamaz; komisyon geri gelmez.

**Independent Test**: Settled işleme `TxnType=Refund` (orgOrderId + PurchAmount kısmi) → `RefundedAmount`
artar; açık batch işleme Refund → red; aşan iade → red.

**Acceptance Scenarios**:
1. **Given** settled işlem (100), **When** Refund 30, **Then** RefundedAmount=30 (kısmi); tekrar Refund 30 →
   60; Refund 50 → red (aşım).
2. **Given** açık batch (Authorized) işlem, **When** Refund, **Then** red ("gün sonu görmemiş, iade edilemez").

---

### User Story 4 - StatusHistory: durum sorgu (in-doubt) (Priority: P2)

Üye-işyeri bir OrderId'nin güncel durumunu sorgular (cevap kaybı / mutabakat). Banka işlemin halini döner.

**Why this priority**: PG'nin çift-çekim emniyetini (in-doubt çözümü) AÇAR — ödeme ağ geçidi kayıp cevabı
bununla netleştirir (fault tolerance, Ders 16).

**Independent Test**: Var olan işleme `TxnType=StatusHistory` (orgOrderId) → `;;` cevap (ProcReturnCode,
TxnStat, VoidDate, RefundedAmount); bilinmeyen OrderId → bulunamadı.

**Acceptance Scenarios**:
1. **Given** onaylı işlem, **When** StatusHistory, **Then** durumu + RefundedAmount/VoidDate döner.
2. **Given** bilinmeyen (ShopCode,OrderId), **When** StatusHistory, **Then** "işlem bulunamadı" red.

---

### Edge Cases

- Gün sonu kesim: zamanlanmış tetik Faz-2'de **seam** (manuel tetik yeterli; Process/timer sonraki).
- Void/Refund kimlik doğrulaması (ShopCode/UserCode/UserPass) — Auth ile aynı fail-closed.
- Zaten Voided/tam-Refunded işleme tekrar işlem → red.
- Komisyon modeli basit (taksit→oran + bloke gün); tam matris (kart tipi/MCC/ciro — Ders 9) sonraki faz.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Onaylı her işlem bir **açık batch**'e bağlanmalı; merchant başına tek açık batch (yoksa açılır).
- **FR-002**: Gün sonu (manuel tetik) açık batch'i kapatmalı: her işlem için `DeductionAmount =
  GrossAmount × komisyonOran(taksit)`, `NetAmount = Gross − Deduction`, `BlokeTarihi = kapanış + blokeGün`
  hesaplanıp işlem `Settled`'a geçmeli; ardından yeni açık batch açılmalı.
- **FR-003**: `Void` yalnız **açık batch**'teki (Authorized) işleme izin verilmeli → `Voided` (komisyon yok);
  kapalı batch (Settled) işleme Void reddedilmeli.
- **FR-004**: `Refund` yalnız **kapalı batch**'teki (Settled) işleme izin verilmeli; kısmi olabilir,
  `RefundedAmount` birikmeli ve toplam tutarı AŞAMAMALI; açık batch işleme Refund reddedilmeli.
- **FR-005**: `StatusHistory` (orgOrderId) işlemin güncel halini (durum, `RefundedAmount`, `VoidDate`,
  `ProcReturnCode`, `TxnStat`) döndürmeli; bilinmeyen (ShopCode,OrderId) → bulunamadı.
- **FR-006**: Void/Refund/StatusHistory istekleri aynı `/mpi/Default.aspx` ucuna `TxnType` ile gelir; cevap
  `;;` ayraçlı (İlke V). Endpoint TxnType'a göre yönlendirir (Auth/Void/Refund/StatusHistory).
- **FR-007**: Void/Refund kimlik (ShopCode/UserCode/UserPass) doğrulamalı (Auth ile aynı fail-closed).
- **FR-008**: İşlem defteri olay akışı (append-only); Void/Refund/Settle birer olaydır (silinmez; denetim).
- **FR-009**: Komisyon oranı + bloke gün `Merchant` üstünde yapılandırılır (taksit→oran tablosu + blokeGün);
  settlement bunu okur. (Tam komisyon matrisi — kart tipi/MCC/ciro — sonraki faz.)

### Key Entities

- **Batch** (YENİ): BatchNo, ShopCode, Status (Open/Closed), OpenedAt, ClosedAt, EndOfDayDate, Gross/
  Deduction/Net toplamları. Merchant başına tek açık batch.
- **Transaction** (GENİŞLER): yeni olaylar `TransactionVoided`/`TransactionRefunded`/`TransactionSettled`;
  yeni alanlar BatchNo, RefundedAmount, DeductionAmount, NetAmount, BlokeTarihi, VoidDate; durum
  {Authorized, Declined, Voided, Settled}.
- **Merchant** (GENİŞLER): `CommissionRates` (taksit→oran), `BlokeGun`.

## Success Criteria *(mandatory)*

- **SC-001**: Gün sonu sonrası açık batch Closed + her işlem Settled (Deduction/Net/Bloke doğru hesap); yeni batch Open.
- **SC-002**: Void yalnız açık-batch işlemde çalışır; settled işlemde reddedilir.
- **SC-003**: Refund yalnız settled işlemde; kısmi birikir; aşan iade reddedilir; komisyon iade edilmez.
- **SC-004**: StatusHistory işlemin güncel halini doğru döner; bilinmeyen OrderId "bulunamadı".
- **SC-005**: Tüm cevaplar `;;` (İlke V); kart verisi/sır dönmez; PAN/CVV kalıcıda maskeli (İlke VI/VIII).

## Assumptions

- Gün sonu **manuel tetik** (dev uç); zamanlanmış kesim Process/timer seam (sonraki).
- Komisyon modeli basit (taksit→oran + bloke gün); tam matris Ders 9 sonraki faz.
- Operasyon API (10 JSON uç — rapor/batch görünümü) Faz 3; bu dilim yalnız InterVpos TxnType'ları + manuel
  gün-sonu tetiği. Hesaba düşüş (MoneyOrder) Faz 6.
- 001 Auth akışı + Merchant/Card seed + event stream altyapısı mevcut.
