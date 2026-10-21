# MaviBank.Api — Domain Süreci

**BC ne yapar:** MaviBank facade'i DenizBank InterVpos ödeme yüzünü taklit eder — üye-işyerinin (PG adapter /
mews/pos) gönderdiği **NonSecure satış (Auth)** isteğini karşılar, kimliği doğrular, kart senaryosuna göre
çekimi onaylar/reddeder, sonucu bir işlem defteri olayına yazar ve `;;` ayraçlı InterVpos cevabı döner.

> **003 Faz-4 durumu:** NonSecure Auth + batch/gün-sonu + Void/Refund/StatusHistory + **3D Secure (sahte ACS
> "MaviSecure" + çift-yönlü hash + OkUrl/FailUrl callback)** KURULDU. Operasyon API (10 JSON uç + MCP),
> zamanlanmış kesim, hesaba düşüş, onboarding ve 3DModel ayrı-completion SONRAKİ fazlar. Gerçek bakiye/limit
> YOK (sentinel kart); komisyon basit; SMS yok (ICodeDeliveryChannel). (İlke V/VI)

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.

## Süreç

1. **Üye-işyeri satış ister.** `POST /mpi/Default.aspx` — form-urlencoded istek (ShopCode/UserCode/UserPass
   + Pan/Expiry/Cvv2/PurchAmount/Currency/OrderId/InstallmentCount/CardType, TxnType=Auth, SecureType=
   NonSecure) gelir; facade parse eder, ham istek Pan/Cvv2 maskeli loglanır `(InterVposEndpointExtension)`.
2. **Para birimi + kimlik kapısı (fail-closed).** Yalnız TL (949) kabul; ShopCode/UserCode/UserPass seed'li
   üye-işyerine karşı doğrulanır — eşleşmezse işlem DOĞMADAN kimlik hatası (`ProcReturnCode=99`, Error)
   `(Authorize.AuthorizeHandler)`.
3. **İdempotency.** `(ShopCode, OrderId)` için işlem defteri stream'i (anahtar `ShopCode:OrderId`) zaten
   varsa ikinci işlem doğmaz; mevcut sonuç aynen döner (çift çekim önleme) `(Authorize.AuthorizeHandler)`.
4. **Çekim kararı (sentinel).** Kart senaryosu çekim sonucunu belirler — onay / limit-yetersiz(51) /
   timeout(91) / bilinmeyen-kart(05); gerçek bakiye yok, deterministik `(ChargeScenarioEngine)`.
5. **İşlem defterine yazılır + açık batch'e bağlanır.** Onayda işlem açık `(Batch)`'e eklenir (yoksa yeni
   açık batch — merchant başına tek) ve `TransactionAuthorized` (BatchNo + TransId + AuthCode); issuer-
   reddinde `TransactionDeclined` olayı event stream'e eklenir; yalnız maskeli PAN `(Transaction)`.
6. **`;;` cevap üretilir.** Sonuç sıralı InterVpos alanlarına `key=value;;...` (JSON değil) text/plain
   `(SemicolonEncoder)`. İstek `TxnType`'a göre yönlendirilir `(InterVposEndpointExtension)`.
7. **İptal (Void) — açık batch.** `TxnType=Void` (orgOrderId): işlem hâlâ açık batch'te (Authorized) ise iz
   bırakmadan ters çevrilir `(VoidTransaction)` → `TransactionVoided`, açık batch'ten çıkar; komisyon doğmaz.
   Gün sonu görmüş (Settled) işleme Void reddedilir.
8. **Gün sonu (settlement) — manuel tetik.** `POST /ops/end-of-day` açık batch'i kapatır: her onaylı işlem
   için komisyon (taksit oranı) düşülür, net + bloke/valör hesaplanır `(EndOfDay)` → `TransactionSettled`;
   batch Closed + toplamlar; yeni açık batch açılır.
9. **İade (Refund) — kapalı batch.** `TxnType=Refund` (orgOrderId + tutar): işlem Settled ise kısmi iade
   `(Refund)` → `TransactionRefunded` (RefundedAmount birikir, tutarı aşamaz); komisyon geri gelmez. Açık
   batch işleme Refund reddedilir.
10. **Durum sorgu (StatusHistory).** `TxnType=StatusHistory` (orgOrderId): işlemin güncel hali (durum,
    RefundedAmount, VoidDate) döner `(StatusHistory)` — PG in-doubt/çift-çekim emniyeti; yoksa bulunamadı.
11. **3D başlat.** SecureType=3DModel/3DPay/3DHost: gelen istek Hash'i doğrulanır (SHA1, ayraçsız) ve bekleyen
    3D oturumu saklanır `(ThreeDInitiate)` `(InterVposHash)` `(ThreeDSession)`; sahte ACS "MaviSecure" (SMS kod
    ekranı) HTML döner `(ThreeDPages)`. Kod gerçek SMS değil `(ICodeDeliveryChannel)`.
12. **3D doğrula + çekim.** ACS kod girişi: doğru kod & senaryo 3D-fail değil → 3DStatus=1 → çekim (Transaction
    Authorized/Declined + batch); aksi → 3DStatus=0, çekim yok `(ThreeDComplete)`.
13. **Callback.** Sonuç OkUrl (başarı) / FailUrl'e auto-POST HTML ile döner; alanlar + `HASH`/`HASHPARAMS`
    (bankanın imzası) `(InterVposHash)` `(ThreeDPages)`; merchant HASH'i SecretKey'le doğrular.

## Domain kuralları (süreci yöneten değişmezler)

- **Protokol sadakati (İlke V).** İstek form-urlencoded, cevap `;;` ayraçlı; alan adları + `ProcReturnCode`
  değerleri InterVpos'la birebir. Kabul ölçütü: mews/pos InterPos bu tele bağlanabilmeli (Faz 5).
- **Determinizm + bakiye yok (İlke VI).** Sonuç sentinel karttan gelir (aynı girdi aynı sonuç); gerçek
  bakiye/limit/dış-servis (SMS/kart-ağı) yok.
- **Fail-closed kimlik (İlke VII).** Geçersiz credential işlem defterini DEĞİŞTİRMEZ (Transaction açılmaz).
- **İdempotency.** `(ShopCode, OrderId)` tekil; tekrar eden Auth yeni işlem doğurmaz, ilk sonucu döner.
- **Batch yaşam döngüsü (açık/kapalı).** Onaylı işlem açık batch'e yazılır (merchant başına tek açık).
  **Void yalnız açık batch** (iz bırakmaz, komisyon yok); **Refund yalnız kapalı batch** (settled; kısmi
  birikir, tutarı aşamaz, komisyon geri gelmez). Ayrım işlem durumuyla kodlanır (Authorized=açık, Settled=kapalı).
- **Settlement (gün sonu).** Açık batch kapanır: `Deduction = round(Gross × oran(taksit), 2)`,
  `Net = Gross − Deduction`, `Bloke = kapanış + BlokeGun`; her işlem Settled, yeni batch açılır. Net tutar
  POS hesabına BLOKELİ eklenir `(Account)`.
- **Hesaba düşüş (Faz 6).** Valör (bloke) süresi dolunca blokeli tutar kullanılabilire geçer `(ReleaseBloke
  → Account.ReleaseMatured)` ("POS alacağı hesaba düştü"); dev tetik `/ops/release-bloke`, zamanlanmış
  Process seam. Operasyon: `GetPosAccountDetail`/`GetAccountTransactionList`.
- **3D Secure (İlke V/VI).** İstek Hash'i (SHA1, ayraçsız) doğrulanmazsa ACS gösterilmez (fail-closed).
  3DStatus=1 (tam 3D) çekim yapar; 0 yapmaz. Callback `HASH`/`HASHPARAMS` bankanın imzasıdır (değerler ':'
  + SecretKey → SHA1 → base64); merchant doğrular. SMS gerçek değil (`ICodeDeliveryChannel`, 123456).
- **Kart/sır minimizasyonu (İlke VIII).** Tam PAN/CVV hiçbir kalıcı ortamda (defter/DB/cevap/3D oturumu) yok;
  ham log'da Pan/Cvv2 maskeli; 3D oturumu yalnız maskeli PAN. SecretKey seed'de + hash girdisinde sır,
  cevapta/log'da/oturumda görünmez.

## Sınır (bu BC'nin dokunmadığı)

**Operasyon API mutabakat çifti KURULDU** (`POST /api/GetPosBatchSummary` + `/GetPosBatchDetail`, JSON zarf +
`SubscriptionKey`; pull modeli — merchant sorar). Kalan operasyon uçları (TransactionList/AccountDetail/
EndOfDayList...) + **MCP yüzü (AgentPlatform gateway mount + AP IdP scope)**, zamanlanmış gün-sonu kesimi
(Process/timer), tam komisyon matrisi (kart tipi/MCC/ciro), onboarding ve 3DModel
ayrı-completion (şu an 3DPay/3DHost semantiği) bu dilimin DIŞINDADIR (sonraki fazlar). Üye-işyeri/müşteri
sipariş durumu çağıranın (PG/store) işidir. Gün sonu şimdilik manuel tetik.
