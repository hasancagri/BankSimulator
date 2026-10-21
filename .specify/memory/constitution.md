<!--
SYNC IMPACT REPORT
==================
Version change: (yeni) → 1.0.0
Bump rationale: İLK onay (initial ratification). BankSimulator reposu için sıfırdan anayasa;
  PaymentGateway anayasasından devralınan taşınabilir mimari ilkeler (BC izolasyonu, zengin domain,
  VSA+CQRS, Result pattern, spec-driven, domain legibility) + banka-simülatörüne özel ilkeler
  (protokol sadakati, determinizm/dış-bağımlılık-yasağı, protokol kimliği, kart/sır minimizasyonu).

Added principles (hepsi yeni):
  I. Bounded Context İzolasyonu
  II. Zengin Domain + Event Sourcing
  III. Vertical Slice + CQRS
  IV. Result Pattern
  V. Protokol Sadakati + Anti-Corruption (NON-NEGOTIABLE)
  VI. Determinizm ve Dış Bağımlılık Yasağı (NON-NEGOTIABLE)
  VII. Protokol Kimliği ve Fail-Closed Yetki
  VIII. Kart Verisi ve Sır Minimizasyonu
  IX. Spec-Driven Development
  X. Domain Süreci Legibility

Sections: Teknoloji ve Alan Kısıtları · Geliştirme Akışı · Governance.

Deferred TODOs:
  - TODO(BANK_MCP_IDP): operasyon MCP yüzü AgentPlatform gateway'e mount + AP IdP scope kilitlendi
    (memory decisions_unified_mcp_agentplatform); scope adları (bank.apply/bank.operations) + mount
    detayı Faz 3 spec'inde netleşir.
  - TODO(FACADE_GENERALIZATION): Core/facade ayrımı (çok-banka) ilk facade (MaviBank) + 2. banka
    gelince genelleştirilir; v1 tek facade (YAGNI).

Templates/commands: bağımlı şablonlar anayasayı çalışma anında okur; senkron gerektiren tutarsızlık yok.
CLAUDE.md + docs/conventions.md anayasayla hizalı (bootstrap'ta yazıldı).
-->

# BankSimulator Constitution

BankSimulator, bir bankanın (referans: DenizBank) üye-işyerine gösterdiği iki yüzü taklit eden bir **banka
simülatörüdür**: (1) **ödeme yüzü** — InterVpos protokolü (form-urlencoded istek + `;;` ayraçlı cevap +
hash); (2) **operasyon yüzü** — API Market POS uçları (gün sonu/batch/mutabakat, JSON zarf). Amaç, bir
ödeme ağ geçidinin (PaymentGateway adapter'ı) ve mews/pos InterPos gateway'inin karşısında **gerçeğe sadık**
çalışan, deterministik bir test/öğrenme hedefi olmaktır. Mimari, **tek repo + banka başına facade** (ilk:
MaviBank) deseniyle çok-bankayı taşır. Bu anayasa projenin pazarlık edilemez ilkelerini tanımlar; CLAUDE.md
*nasıl uygulanır* rehberidir, bu belge *ne pazarlık edilemez* sorusunu yanıtlar. Çakışma olursa anayasa kazanır.

## Core Principles

### I. Bounded Context İzolasyonu

Her banka facade'i tek bir Bounded Context'tir ve sınırı fiziksel olarak serttir.

- Her facade kendi Postgres şemasına ve kendi domain modeline sahiptir; facade'ler arasında paylaşılan bir
  domain modeli OLMAZ. Paylaşım yalnız altyapı/çekirdek yardımcılarıdır (Result, DI marker, AggregateRoot),
  domain değil.
- Bir facade başka bir facade'in şemasına/aggregate'ine doğrudan erişemez. Context'ler arası iletişim
  yalnız bilinçli integration event kontratı ya da sanksiyonlu senkron kanal üzerindendir.
- Aynı kavram farklı banka facade'inde farklı modeldir (farklı wire, farklı hash); model sızdırılmaz.

Gerekçe: Çok-bankalı bir simülatörde her bankanın protokolü ve kuralı farklıdır; izolasyon, bir bankanın
değişiminin diğerlerine bulaşmasını engeller.

### II. Zengin Domain Modeli + Event Sourcing

İş kuralları domain modelinin içinde yaşar; para hareketi olaylarla izlenir.

- Anemik aggregate YASAKTIR; private setter + statik `Create` fabrikası + davranış metotları. Invariant
  ihlalleri handler'da değil aggregate metodunda yakalanır; koleksiyonlar private, okuma `IReadOnlyList`.
- **İşlem defteri (`Transaction`) Marten event stream'dir** (Wolverine `[AggregateHandler]`): append-only,
  `TransactionAuthorized`/`TransactionDeclined`/`...Voided`/`...Refunded` olaylarıyla; güncel durum türetilir.
  Audit ve mutabakat izi bedava gelir (muhasebe defteri analojisi).
- Hesap/config/kart/BIN gibi **referans veri düz dokümandır** (event stream değil). Kimliği olmayan kavram
  Value Object (record + private ctor + `Create`); enum ait olduğu aggregate dosyasında.
- Optimistic concurrency bilinçli kurulur (`FetchForWriting`/version); eşzamanlı çift-işlem DB'de ölür.

Gerekçe: Ödeme/para alanı event sourcing'in doğal yeridir — her soru ("iade öncesi durum", "provizyon-capture
arası") sonradan cevaplanabilir; kural tek yerde (aggregate) toplanır.

### III. Vertical Slice + CQRS

Kod teknik katmana göre değil, domain feature'ına göre organize edilir.

- Feature'lar `Domains/<Aggregate>/Features/{Commands,Queries}` altındadır; bir feature = bir static class
  (command/query `record` + `Response` + `Handler` + endpoint-extension aynı dosyada).
- Yazma `Commands/` (`[Transactional]`), okuma `Queries/` ayrı slice'tır. Repository YOK — handler doğrudan
  Marten `IDocumentSession`, slice-arası `IMessageBus`. Endpoint Minimal API.
- Kullanıcı tetiklemeyen süreç-güdümlü handler'lar `Domains/` DIŞINDADIR: `Saga/` (dış orchestrator),
  `Process/` (BC'nin kendi dayanıklı süreci — ör. gün sonu batch kapanışı, mutabakat).

Gerekçe: Bir feature'ın tüm parçalarını tek dosyada tutmak değişimi yerelleştirir.

### IV. Result Pattern

Beklenen hatalar exception ile DEĞİL, Result nesnesiyle taşınır.

- Aggregate metotları `ResultDomain`/`ResultDomain<T>`; handler'lar servis-içi Result tipleri döner; statik
  fabrika (`Ok`/`Error`), `new` değil. Hata `MessageItem.Code` = resource sabiti (serbest metin değil).
- Çok-durumlu banka sonucu (onay / red + red sınıfı) **outcome-enum** ile taşınır; retry-able red teknik
  `Error`'a eşlenmez. (Not: dışa dönen tel yine `;;` ProcReturnCode'dur — Result iç modeldir.)
- Exception yalnız gerçekten beklenmeyen durum içindir (`GlobalExceptionHandler`).

Gerekçe: Beklenen hataları tipli taşımak akışı öngörülebilir kılar; kontrol akışı exception'a bağlanmaz.

### V. Protokol Sadakati + Anti-Corruption (NON-NEGOTIABLE)

Simülatörün değeri gerçeğe sadakattedir; tel birebir taklit edilir.

- Her banka facade'i gerçek protokolünü **birebir** üretir: InterVpos için istek `application/x-www-form-
  urlencoded`, cevap **`;;` ayraçlı `key=value`** (JSON DEĞİL), hash gerçek algoritmayla (SHA1/base64,
  ayraçsız birleştirme). Alan adları, kod değerleri (`ProcReturnCode=00`...), hash girdileri gerçekle aynıdır.
- **Uyumluluk ölçütü (kabul):** mews/pos InterPos gateway'i yalnız config'de URL değiştirerek bu simülatöre
  bağlanıp tüm akışları geçebilmelidir. PG adapter'ı da aynı tele konuşur.
- Wire tipleri kullanan facade'in İÇİNDE nested kalır; domain/BC sınırını GEÇMEZ. Facade, wire ↔ normalize
  domain modeli çevirisini yapar (anti-corruption). Paylaşılan banka SDK'sı ÜRETİLMEZ.

Gerekçe: Uydurma bir tel öğretmez ve test hedefi olamaz; sadakat bu projenin tüm varlık sebebidir.

### VI. Determinizm ve Dış Bağımlılık Yasağı (NON-NEGOTIABLE)

Aynı girdi her zaman aynı sonucu üretir; gerçek dış servise bağımlılık yoktur.

- Sonuç **sentinel test-kartı** ile belirlenir (kart/tutar → canned sonuç: onay/limit-yetersiz/timeout/
  fraud). Rastgelelik yasaktır (test determinizmini bozar). Gerçek bakiye/limit modeli v1'de YOKTUR —
  `ProcReturnCode=51` bir sentinel bayrağıdır, hesaplanan bakiye değil.
- **Gerçek dış bağımlılık yoktur:** gerçek SMS gönderilmez (3DS kod teslimi `ICodeDeliveryChannel` arkasında;
  varsayılan sabit kod / ekran / log — asla dış servis), gerçek kart ağı çağrılmaz, gerçek para hareketi yok.
- Her istek ve cevap ham haliyle loglanır (eğitim + debug + mutabakat izi).

Gerekçe: Bir simülatör, deterministik ve offline çalışabildiği ölçüde test hedefi ve öğrenme aracıdır;
dış bağımlılık (iyzico dersi) hem determinizmi hem tekrarlanabilirliği bozar.

### VII. Protokol Kimliği ve Fail-Closed Yetki

Banka kimliği protokol kimliğidir (SSO değil); hiçbir korunan uç açıkta kalmaz.

- Üye-işyeri her istekte **protokol credential'ı** taşır: `ShopCode`/`UserCode`/`UserPass` (+ hash'lerde
  `SecretKey`, operasyon yüzünde `SubscriptionKey`). Credential **store (ShopCode) başına farklıdır** ve
  seed'li `Merchant` kaydına karşı doğrulanır; eşleşmezse istek reddedilir (fail-closed). Banka ayrı kurum
  olduğu için SSO/OIDC YOKTUR — kimlik protokoldedir (ileride IP whitelist dahil).
- Durum değiştiren veya hassas veri döndüren her uç gereken yetkiyi açıkça beyan eder; "varsayılan açık" uç yok.
- **MCP sınırı:** bankanın **operasyon/admin yüzü** (gün sonu/başvuru/batch/parametre) MCP'den sürülebilir
  (AgentPlatform gateway'e mount + AP IdP scope; Faz 3). **Ödeme İCRASI (Auth/çekim) hiçbir koşulda MCP'ye
  GİRMEZ** — protokol rayında kalır. MCP yalnız operatör düzlemi (gör/tetikle/onayla), para icrası değil.

Gerekçe: Banka gerçek dünyada bir bankadır; kimliği kurumsal SSO değil protokol credential'ıdır. Ödeme
akışını agent/LLM'e açmak fraud + belirsizlik yüzeyi doğurur; operatör işlemleri ise MCP'ye uygundur.

### VIII. Kart Verisi ve Sır Minimizasyonu

Simülatör dahi taşımak zorunda olmadığı kart verisini ve sırrı tutmaz.

- **PAN kalıcı olmaz:** tam kart numarası işlem defterine YAZILMAZ, ham log dışında kalıcı ortama DÜŞMEZ;
  Transaction yalnız maskeli gösterim + referans/token tutar. Kart saklama (vault) kapsam DIŞIDIR.
- **Sırlar yalnız sır-store'da:** merchant `SecretKey`, callback imza sırları secret-store'da (user-secrets/
  env), non-secret Options POCO'sundan ayrı. Sır ASLA query DTO'su / tool-dönüşü / trace'te görünmez.
- **Kontrol düzlemi sır döndürmez:** onboarding/operasyon dönüşleri credential İÇERMEZ; sır gösterimi yalnız
  TTL'li reveal-link ile (insan kopyalar), makine-handoff S2S kayıt + imzalı callback'le yapılır.

Gerekçe: Gerçeğe sadakat, PCI disiplinini de taklit etmeyi gerektirir; tutmadığın veriyi sızdıramazsın.

### IX. Spec-Driven Development

Önemsiz olmayan her değişiklik spec-kit akışından geçer.

- Akış: `/speckit-constitution` → `/speckit-specify` → (`/speckit-clarify`) → `/speckit-plan` →
  `/speckit-tasks` → `/speckit-implement`. Koda atlamadan önce en azından spec üretilir.
- Anayasa her şeyin üstündedir; spec/plan/kod anayasayla çelişemez. Artefakt feature büyüklüğüne göre
  ölçeklenir (trivial → spec'siz; küçük → spec+tasks; tam → tam akış).
- **Domain-TDD:** saf domain (aggregate davranışı, `Apply` event uygulaması, VO, hash hesabı, test-kart
  senaryo motoru) test-first; test task'ı implementasyondan önce. Handler/endpoint/altyapı test-sonra/canlı.

Gerekçe: Kararların kod yazılmadan önce yazılı ve gözden geçirilmiş olmasını sağlar.

### X. Domain Süreci Legibility

Her Bounded Context'in domain süreci okunabilir olmak zorundadır.

- BC'nin iş akışı (hangi adım, hangi sırayla, hangi komut/olay/policy) **EventStorming altitude'unda**,
  ubiquitous dille belgelenir (teknoloji değil iş anlatısı); class-by-class döküm DEĞİL.
- Güncelleme tetiği dardır: yeni/silinen command-event-policy ya da adım sırası değişimi; mekanik rename
  tetiklemez. Feature domain sürecini değiştiriyorsa belge aynı PR'da güncellenir.
- Domain süreci belgesi olmayan ya da bayat bir BC kabul edilemez. Dosya adı/format/guard "nasıl" olarak
  `conventions.md`/`CLAUDE.md`'de tanımlanır.

Gerekçe: DDD'nin çekirdeği domain legibility'dir; süreç teknolojinin ardında kaybolmamalıdır.

## Teknoloji ve Alan Kısıtları

- **Platform:** .NET 10; her projede `Nullable` ve `ImplicitUsings` açık.
- **Orkestrasyon:** .NET Aspire. Sistem her zaman AppHost'tan ayağa kalkar; conn-string'ler (bankDb,
  rabbitmq) Aspire'dan enjekte edilir. Tek servisi izole çalıştırmak desteklenen yol değildir.
- **Kalıcılık (karma):** Marten/Postgres. `Transaction` = event stream (`[AggregateHandler]`); Merchant/Card/
  Batch/BIN = düz doküman. Şema otomatik kurulur (`ApplyAllDatabaseChangesOnStartup`), migration komutu yok.
- **Mesajlaşma:** Wolverine — süreç-içi bus + RabbitMQ fanout; deterministik mesaj `[Transactional]` outbox.
  Handler keşfi ad-son-eki bazlı (`*Handler`/`*Consumer`); bulunamazsa `opts.Discovery.IncludeType(...)` ŞART.
- **Paket yönetimi:** Central Package Management — sürümler yalnız `Directory.Packages.props`'ta; `.csproj`
  sürümsüz listeler.
- **DI:** `ITransientDependency`/`IScopedDependency`/`ISingletonDependency` marker + Scrutor otomatik kaydı.
- **Config:** Options pattern (tip'li POCO). `IConfiguration`'dan doğrudan okuma YASAK; her section Options
  POCO'suna bağlanır (`BindConfiguration`+`Validate`), tüketici düz `T` enjekte eder. Handler'da literal yasak.
- **Alan kısıtı:** v1 yalnız TL (para birimi ISO numerik 949); çoklu para birimi protokolde taşınır ama
  işlem mantığı TL varsayar.

## Geliştirme Akışı

- **Dil:** Kod yorumları, XML doküman, commit mesajı ve kullanıcıya dönük mesajlar Türkçe.
- **Using'ler:** Her projede tek `GlobalUsings.cs`; paylaşılan namespace oraya.
- **Test:** Saf domain birim testleri (host/entegrasyon harness'ı yok). Davranışlı aggregate, `Apply` event
  uygulaması, hash hesabı, `;;` encoder ve test-kart senaryo motoru test için önceliklidir.
- **Çalışma modu:** Bu bir öğrenme/mülakat-hazırlığı projesidir — her mimari karar gerekçesiyle oturtulur;
  hız değil anlayış önceliklidir (gerekçeler Obsidian ders notlarına yazılır).
- **Runtime rehberi:** CLAUDE.md günlük *nasıl uygulanır* rehberidir ve anayasaya tabidir; çakışırsa anayasa kazanır.

## Governance

- Bu anayasa diğer tüm pratiklerin üstündedir; bir uygulama kararı anayasayla çelişemez.
- **Amendment prosedürü:** Değişiklik bu dosyada gerekçesiyle önerilir, sürüm aşağıdaki kurala göre güncellenir
  ve Sync Impact Report'a işlenir.
- **Sürümleme (semantic):** MAJOR = geriye dönük uyumsuz ilke kaldırma/yeniden tanımlama; MINOR = yeni ilke/
  bölüm ekleme veya rehberliğin belirgin genişlemesi; PATCH = açıklama/ifade/yazım düzeltmesi.
- **Uyum denetimi:** Her spec, plan ve kod incelemesi anayasaya uygunluğu doğrulamak ZORUNDADIR; ek karmaşıklık
  ancak gerekçelendirilerek eklenir (YAGNI).
- Ertelenen kararlar (TODO) Sync Impact Report'ta takip edilir, karar netleştiğinde amendment ile kapatılır.

**Version**: 1.0.0 | **Ratified**: 2026-10-21 | **Last Amended**: 2026-10-21
