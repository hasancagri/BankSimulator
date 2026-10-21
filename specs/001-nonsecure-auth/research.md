# Phase 0 Research: NonSecure Auth

Teknik bağlam açık (NEEDS CLARIFICATION yok); kararlar gereksinim dokümanı (`Banka Simülatörü - Gereksinim
Dokümanı.md` §1,§3), Ders 4 mimari ve constitution'dan türetildi.

## D1 — InterVpos tel: form-urlencoded istek + `;;` ayraçlı cevap (İlke V)

- **Decision**: İstek `application/x-www-form-urlencoded`; cevap **`;;` ayraçlı `key=value`** (JSON DEĞİL).
  Alan adları + kod değerleri InterVpos'la birebir.
- **İstek alanları (Auth NonSecure)**: `ShopCode`, `UserCode`, `UserPass`, `TxnType=Auth`,
  `SecureType=NonSecure`, `Pan`, `Expiry` ("MMYY", ör. `1225`), `Cvv2`, `PurchAmount`, `Currency` (949=TRY),
  `OrderId`, `InstallmentCount` (0/1 → boş string, 2+ → sayı), `CardType` (0=VISA,1=MC,2=AMEX,3=TROY),
  `MOTO=0`, `Lang`.
- **Cevap alanları**: `ProcReturnCode` (`00`=onay), `OrderId`, `TransId`, `AuthCode` (onayda), `Response`
  (Approved/Declined), `ErrorCode`/`ErrorMessage` (redde), `TRXDATE`.
- **Rationale**: pos-master InterPos + gerçek InterVpos; uyumluluk ölçütü (Faz 5) bu teli bekler.
- **Alternatives**: JSON cevap → İlke V reddi (gerçeğe sadık değil, mews/pos bağlanamaz).

## D2 — `;;` encoder: handler-yakını küçük helper

- **Decision**: `key=value;;key=value` üreten küçük saf encoder (sıralı dictionary → string). Handler/slice
  içinde helper; ayrı middleware/formatter YAZILMAZ (tek içerik tipi, YAGNI).
- **Rationale**: Küçük + saf → test-first kolay (domain-TDD). Encoder saf fonksiyon.
- **Alternatives**: Özel `IOutputFormatter` → fazla altyapı (tek uç, tek biçim).

## D3 — Transaction = Marten event stream (`[AggregateHandler]`)

- **Decision**: İşlem defteri olay akışı; `TransactionAuthorized`/`TransactionDeclined` olayları; aggregate
  `Apply(event)` ile türetilir. Wolverine `[AggregateHandler]` deseni (Ders 4 bölüm 3).
- **Rationale**: İlke II + audit/mutabakat izi bedava; para alanı event sourcing'in doğal yeri. Faz 2 Void/
  Refund/Batch olayları aynı stream'e eklenir (append-only).
- **Alternatives**: Düz doküman Transaction → audit/replay kaybı, Void/Refund tarihçesi ezilir.

## D4 — Merchant + Card = seed'li düz doküman

- **Decision**: `Merchant` (ShopCode kimliği) + `Card` (test kartı + senaryo bayrağı) düz Marten dokümanı;
  startup'ta embedded JSON'dan seed (008/090 BinCardSeeder deseni). Onboarding (credential doğuşu) Faz 7.
- **Rationale**: Referans veri, davranış yok → aggregate değil. Dev: reseed; defansif migration yok.
- **Alternatives**: Merchant aggregate → dormant davranış, YAGNI.

## D5 — Sonuç = sentinel test-kartı (determinizm; bakiye yok)

- **Decision**: Kart `Pan` → senaryo bayrağı (onay/limit-yetersiz/timeout) → canned `ProcReturnCode`.
  Gerçek bakiye/limit YOK (storm kararı). Sentinel'ler Card seed'inde.
  - 4111..0001 → onay (`00`) · 4111..0002 → limit yetersiz (`51`) · 4111..0005 → timeout.
- **Rationale**: İlke VI determinizm; iyzico sandbox bin+senaryo deseni. Gerçek bakiye Faz 6 (hesaba düşüş).
- **Alternatives**: Account+balance → sonraki faz; rastgele → determinizm bozulur.

## D6 — Kimlik: ShopCode/UserCode/UserPass, fail-closed, işlem doğmadan

- **Decision**: Her istek seed'li Merchant'a karşı doğrulanır; eşleşmezse kart'a dokunulmadan kimlik hatası
  (`ErrorCode`/`ErrorMessage`), Transaction AÇILMAZ. NonSecure'de hash YOK (3DS hash Faz 4).
- **Rationale**: İlke VII fail-closed; gerçek POS kimlik hatasını işlem akışından ayırır.

## D7 — İdempotency: `(ShopCode, OrderId)` tekil

- **Decision**: `(ShopCode, OrderId)` partial/unique; tekrar eden Auth ikinci işlem doğurmaz, ilk sonucu
  döner. (Marten unique index; çakışmada mevcut stream'den cevap.)
- **Rationale**: İlke — çift çekim önleme; PG retry'ı (aynı OrderId) güvenli. Ders 5 idempotency.

## D8 — PAN maskeleme (İlke VI × ham log gerilimi)

- **Decision**: Ham istek/cevap loglanır ama `Pan`→`4111********0001`, `Cvv2`→`***` maskelenir. Transaction
  yalnız maskeli PAN + referans tutar; tam PAN hiçbir kalıcı ortamda yok.
- **Rationale**: Tel şekli eğitim için görünür; kart sırrı görünmez (İlke VI/VIII). Tek uzlaşı noktası.
