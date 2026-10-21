# Feature Specification: 3D Secure (sahte ACS "MaviSecure")

**Feature Branch**: `003-three-d-secure` · **Created**: 2026-10-21 · **Status**: Draft

**Input**: BankSimulator Faz 4 — InterVpos 3D Secure akışı: istek hash doğrulama → sahte ACS sayfası (SMS
kod ekranı) → sonuç callback'i (OkUrl/FailUrl'e auto-POST) + çift-yönlü hash. Gereksinim §1.5, Ders 10.

## User Scenarios

### US1 - 3D doğrulama başarılı → çekim onaylanır (P1) 🎯
Üye-işyeri 3D form POST eder (kart + OkUrl/FailUrl/Rnd/Hash). Sim isteği hash'le doğrular, sahte ACS (kod
ekranı) gösterir; doğru kod + onay kartı → 3DStatus=1, çekim yapılır, OkUrl'e imzalı callback auto-POST.

**Independent Test**: Geçerli Hash + onay kartı + kod 123456 → ACS → OkUrl'e 3DStatus=1 + HASH/HASHPARAMS;
Transaction Authorized. Merchant callback HASH'i SecretKey'le doğrulayabilir.

### US2 - 3D doğrulama başarısız → çekim yok (P2)
3D-fail sentinel kartı (ya da yanlış kod) → 3DStatus=0, çekim YAPILMAZ, FailUrl'e callback.

### US3 - İstek hash geçersiz → reddedilir (P2)
Gelen Hash sim'in hesabıyla eşleşmezse ACS gösterilmez, hata döner (fail-closed, İlke V/VII).

### Edge Cases
- 3DModel'de ayrı tamamlama (completion) adımı — bu dilimde 3DPay/3DHost semantiği (callback nihai + çekim);
  3DModel completion seam (sonraki).
- SMS gerçek gönderilmez → `ICodeDeliveryChannel` (FixedCodeChannel 123456 varsayılan; İlke VI determinizm).

## Requirements

- **FR-001**: 3D istek (SecureType=3DModel/3DPay/3DHost) `POST /mpi/Default.aspx`'e gelir; sim gelen `Hash`'i
  `SHA1(ShopCode+OrderId+PurchAmount+OkUrl+FailUrl+TxnType+InstallmentCount+Rnd+SecretKey)` base64 ile
  doğrular (ayraçsız birleştirme). Eşleşmezse reddeder.
- **FR-002**: Geçerli istek → **sahte ACS HTML** (MaviSecure: işlem özeti + SMS kod girişi + onayla/reddet)
  döner; bekleyen 3D oturumu saklanır (OrderId/tutar/OkUrl/FailUrl/maskeli PAN; tam PAN SAKLANMAZ).
- **FR-003**: Kod `ICodeDeliveryChannel` ile doğrulanır (gerçek SMS YOK; FixedCodeChannel 123456). Kart
  senaryosu 3DStatus'u belirleyebilir (onay kartı→1, 3d-fail kartı→0; determinist).
- **FR-004**: 3DStatus=1 (tam 3D) → çekim yapılır (Transaction Authorized + batch); 3DStatus=0 → çekim yok.
- **FR-005**: Sonuç OkUrl (başarı) / FailUrl (başarısızlık)'e **auto-POST HTML form** ile döner; alanlar
  3DStatus/MD/PayerTxnId/Eci/ProcReturnCode/OrderId/maskeli Pan/PurchAmount/Currency/TxnStat/TRXDATE +
  **HASH + HASHPARAMS**.
- **FR-006**: Callback HASH = `base64(SHA1( değerler(':' ile) + SecretKey ))`; HASHPARAMS = alan adları
  (':' ile). Merchant aynı yolla doğrular (bankanın imzası, Ders 2 "gelen imza").
- **FR-007**: Tam PAN/CVV + SecretKey hiçbir kalıcı ortamda; callback maskeli PAN taşır (İlke VI/VIII).

## Key Entities
- **ThreeDSession** (YENİ, doküman): bekleyen 3D oturumu — sid, ShopCode, OrderId, Amount, Currency,
  Installment, MaskedPan, CardType, OkUrl, FailUrl, Rnd, Approve (senaryo), Status (Pending/Completed).
- **Card** (GENİŞLER): `CardScenario` +`ThreeDFail`; seed 4111..0003 (3DStatus=0).
- **Transaction**: 3DStatus=1'de 002 Auth yolundaki gibi Authorized (batch'e bağlanır).

## Success Criteria
- **SC-001**: Geçerli Hash + onay kartı + doğru kod → 3DStatus=1, Transaction Authorized, OkUrl callback
  HASH doğrulanabilir.
- **SC-002**: 3d-fail kartı / yanlış kod → 3DStatus=0, çekim yok, FailUrl callback.
- **SC-003**: Geçersiz istek Hash → ACS gösterilmez (fail-closed).
- **SC-004**: Hash'ler (istek + callback) deterministik + doğrulanabilir; tam PAN/CVV/SecretKey kalıcıda yok.

## Assumptions
- 3DPay/3DHost semantiği (callback nihai + çekim). 3DModel ayrı completion seam.
- ACS HTML gömülü statik + saf JS (Blazor/SPA red — pivot kararı); SMS yok (ICodeDeliveryChannel).
- 001/002 altyapısı (Transaction/Batch/Merchant seed, InterVpos endpoint) mevcut.
