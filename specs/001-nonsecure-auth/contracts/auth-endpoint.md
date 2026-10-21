# Contract: InterVpos NonSecure Auth (`POST /mpi/Default.aspx`)

Üye-işyeri (PG adapter / mews/pos InterPos) NonSecure satışı bu uca gönderir. Gerçeğe sadakat (İlke V):
istek form-urlencoded, cevap `;;` ayraçlı — JSON DEĞİL.

## Request

`POST /mpi/Default.aspx`
`Content-Type: application/x-www-form-urlencoded`

| Alan | Örnek | Not |
|---|---|---|
| `ShopCode` | `MAVI0001` | merchant kimliği |
| `UserCode` | `APIUSER` | POS kullanıcı |
| `UserPass` | `****` | POS parola |
| `TxnType` | `Auth` | satış |
| `SecureType` | `NonSecure` | 3DS'siz |
| `Pan` | `4111111111110001` | kart no (saklanmaz) |
| `Expiry` | `1230` | "MMYY" |
| `Cvv2` | `123` | (saklanmaz) |
| `PurchAmount` | `125.50` | tutar |
| `Currency` | `949` | ISO numerik (949=TRY; v1 yalnız TL) |
| `OrderId` | `ORD-7731` | merchant referansı — `(ShopCode,OrderId)` tekil |
| `InstallmentCount` | `` / `3` | 0-1 → boş, 2+ → sayı |
| `CardType` | `0` | 0=VISA,1=MC,2=AMEX,3=TROY |

## Response (`;;` ayraçlı `key=value` — JSON değil)

**Onay (ProcReturnCode=00):**
```
ProcReturnCode=00;;Response=Approved;;OrderId=ORD-7731;;TransId=A1B2C3D4;;AuthCode=P00123;;TRXDATE=2026-10-21 14:03:11
```

**Red (issuer, limit yetersiz):**
```
ProcReturnCode=51;;Response=Declined;;OrderId=ORD-7731;;ErrorCode=51;;ErrorMessage=Yetersiz bakiye;;TRXDATE=...
```

**Kimlik hatası (işlem doğmaz):**
```
ProcReturnCode=99;;Response=Error;;ErrorCode=AUTH;;ErrorMessage=Gecersiz uye isyeri bilgileri
```

Durum kodları: `ProcReturnCode="00"` = onay, diğer her şey red/hata. Alan adları + biçim InterVpos birebir.

## Davranış

```
kimlik geçersiz           → ProcReturnCode=99 (Error); Transaction DOĞMAZ (fail-closed, İlke VII)
kart Approve sentinel      → ProcReturnCode=00; TransactionAuthorized; TransId+AuthCode üretilir
kart InsufficientFunds     → ProcReturnCode=51; TransactionDeclined (Opened/terminal değil — red denemesi)
kart Timeout sentinel      → belgeli timeout davranışı (determinizm; PG in-doubt sınaması)
para birimi ≠ 949          → red (desteklenmeyen para birimi)
aynı (ShopCode,OrderId)    → idempotent: ikinci işlem doğmaz, ilk sonuç döner
```

## İnvariant

- Cevap asla kart verisi (tam PAN/CVV) / banka sırrı (SecretKey) İÇERMEZ.
- Ham log `Pan`/`Cvv2` maskeler; Transaction yalnız maskeli PAN tutar.
- `TransId`/`AuthCode` benzersiz; aynı girdi → aynı cevap (determinizm).
