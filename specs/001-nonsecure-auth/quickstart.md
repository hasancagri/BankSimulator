# Quickstart / Doğrulama: NonSecure Auth

Canlı adımlar tüm dilim bitince topluca koşulur.

## Birim test (domain-TDD, önce)

```bash
dotnet test
```
Beklenen (yeni): `TransactionTests` (Authorized/Declined `Apply` + `(ShopCode,OrderId)` idempotency),
`SemicolonEncoderTests` (`;;` encode birebir), `ChargeScenarioTests` (sentinel PAN → ChargeOutcome:
00/51/timeout), `MaskingTests` (PAN/CVV maskeleme). Saf domain, host yok.

## Build

```bash
dotnet build
```
Beklenen: 0 hata.

## Canlı Aspire

```bash
dotnet run --project src/aspire/AppHost/AppHost.csproj
```

1. **Onay (US1):** `POST /mpi/Default.aspx` (form-urlencoded, ShopCode=MAVI0001 + onay kartı 4111..0001,
   TxnType=Auth, NonSecure) → cevap `ProcReturnCode=00;;TransId=..;;AuthCode=..`; Marten `mavibank` şemasında
   `mt_events`'te `TransactionAuthorized`; ham log'da PAN maskeli.
2. **İssuer red (US2):** kart 4111..0002 → `ProcReturnCode=51`; `TransactionDeclined`.
3. **Kimlik hatası (US3):** yanlış UserPass → `ProcReturnCode=99` (Error); yeni stream YOK.
4. **İdempotency:** aynı (ShopCode, OrderId) ikinci kez → aynı cevap, ikinci olay yok.

## İlke denetimi

- Tam PAN/CVV: DB/log/cevapta görünmemeli (grep + inceleme; yalnız maskeli).
- Cevap `;;` ayraçlı, JSON değil (İlke V).
- Aynı girdi → aynı cevap (determinizm, İlke VI).

## Nihai kabul (Faz 5, ileride)

mews/pos InterPos gateway'i config URL'i bu uca çevirip NonSecure satışı geçebilmeli (tel uyumluluğu).
