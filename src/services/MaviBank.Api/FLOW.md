# MaviBank.Api — Domain Süreci

**BC ne yapar:** MaviBank facade'i DenizBank InterVpos ödeme yüzünü taklit eder — üye-işyerinin (PG adapter /
mews/pos) gönderdiği **NonSecure satış (Auth)** isteğini karşılar, kimliği doğrular, kart senaryosuna göre
çekimi onaylar/reddeder, sonucu bir işlem defteri olayına yazar ve `;;` ayraçlı InterVpos cevabı döner.

> **001 Faz-1 durumu:** yalnız **NonSecure Auth**. 3DS, Void/Refund/StatusHistory, batch/gün-sonu/komisyon,
> operasyon API (10 uç) ve onboarding SONRAKİ fazlardır. Gerçek bakiye/limit YOK (sentinel kart; İlke VI).

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
5. **İşlem defterine yazılır.** Onayda `TransactionAuthorized` (benzersiz TransId + AuthCode), issuer-reddinde
   `TransactionDeclined` olayı event stream'e eklenir; kart verisi değil yalnız maskeli PAN tutulur
   `(Transaction)`.
6. **`;;` cevap üretilir.** Sonuç sıralı InterVpos alanlarına çevrilip `key=value;;...` (JSON değil) olarak
   text/plain döner `(SemicolonEncoder)`.

## Domain kuralları (süreci yöneten değişmezler)

- **Protokol sadakati (İlke V).** İstek form-urlencoded, cevap `;;` ayraçlı; alan adları + `ProcReturnCode`
  değerleri InterVpos'la birebir. Kabul ölçütü: mews/pos InterPos bu tele bağlanabilmeli (Faz 5).
- **Determinizm + bakiye yok (İlke VI).** Sonuç sentinel karttan gelir (aynı girdi aynı sonuç); gerçek
  bakiye/limit/dış-servis (SMS/kart-ağı) yok.
- **Fail-closed kimlik (İlke VII).** Geçersiz credential işlem defterini DEĞİŞTİRMEZ (Transaction açılmaz).
- **İdempotency.** `(ShopCode, OrderId)` tekil; tekrar eden Auth yeni işlem doğurmaz, ilk sonucu döner.
- **Kart/sır minimizasyonu (İlke VIII).** Tam PAN/CVV hiçbir kalıcı ortamda (defter/DB/cevap) yok; ham
  log'da Pan/Cvv2 maskeli. SecretKey seed'de sır, cevapta/log'da görünmez.

## Sınır (bu BC'nin dokunmadığı)

**3DS/secure akış + sahte ACS, Void/Refund/StatusHistory, batch/gün-sonu/komisyon/bloke, operasyon API (10
uç) + mutabakat raporları, hesaba düşüş, onboarding ve operasyon MCP yüzü bu dilimin DIŞINDADIR** (sonraki
fazlar). Üye-işyeri/müşteri sipariş durumu çağıranın (PG/store) işidir — MaviBank yalnız otorizasyonu ve
`;;` sonucu döndürür. Para hareketi/ledger simülasyonu Faz 6.
