using System.Globalization;

namespace Simulator.InterVpos;

/// <summary>
/// 3D Secure tamamla (sahte ACS kod girişi sonrası). Kod + kart senaryosu 3DStatus'u belirler: geçerli kod
/// & senaryo 3D-fail değil → 3DStatus=1 (tam 3D) → çekim yapılır (Transaction Authorized/Declined + batch);
/// aksi → 3DStatus=0, çekim YOK. Sonuç callback alanları + çift-yönlü HASH/HASHPARAMS üretilir (bankanın
/// imzası, İlke V). 3DPay/3DHost semantiği (callback nihai). SecretKey hash girdisi — çıktıda görünmez.
/// </summary>
public static class ThreeDComplete
{
    public record Command(string Sid, string Code);

    public record Result(bool ThreeDSuccess, string TargetUrl,
        List<KeyValuePair<string, string>> CallbackFields);

    [Transactional]
    public class ThreeDCompleteHandler
    {
        public async Task<Result> Handle(Command cmd, IDocumentSession session, ICodeDeliveryChannel codeChannel,
            ChargeScenarioEngine engine, CancellationToken ct)
        {
            var s3d = await session.LoadAsync<ThreeDSession>(cmd.Sid, ct);
            if (s3d is null || s3d.Status == ThreeDSessionStatus.Completed)
                return new Result(false, string.Empty, new List<KeyValuePair<string, string>>());

            var merchant = await session.LoadAsync<Merchant>(s3d.ShopCode, ct);
            var now = DateTime.UtcNow;
            var codeValid = codeChannel.Verify(cmd.Code);
            var threeDPass = codeValid && s3d.Scenario != CardScenario.ThreeDFail;

            string threeDStatus, procReturnCode, txnStat, md = string.Empty, payerTxnId = string.Empty, eci = string.Empty;

            if (threeDPass)
            {
                threeDStatus = "1"; // tam 3D
                md = Guid.NewGuid().ToString("N")[..16];
                payerTxnId = Guid.NewGuid().ToString("N")[..12];
                eci = "05"; // full authentication

                if (s3d.SecureType == "3DModel")
                {
                    // Callback NİHAİ DEĞİL — çekim ayrı completion adımında (SecureType=NonSecure + MD).
                    procReturnCode = ResponseCodes.Approved; // yalnız 3D doğrulama OK
                    txnStat = "3D-Auth";
                }
                else
                {
                    // 3DPay/3DHost: callback nihai → şimdi çek (paylaşılan helper).
                    (procReturnCode, txnStat) = await ThreeDCharge.ChargeAsync(s3d, session, engine, now, ct);
                }
            }
            else
            {
                threeDStatus = "0"; // 3D başarısız → çekim yok
                procReturnCode = ResponseCodes.DoNotHonour;
                txnStat = "3D-Fail";
            }

            // Oturumu tamamla — 3DModel completion MD'yi doğrulayabilsin (3D sonucu + MD saklanır).
            s3d.Complete(threeDPass, threeDPass ? md : null);
            session.Store(s3d);

            // Callback alanları (sıralı) — HASH imzalanacak değerler.
            var amountStr = s3d.Amount.ToString(CultureInfo.InvariantCulture);
            var trxDate = now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            var signed = new (string Name, string Value)[]
            {
                ("3DStatus", threeDStatus), ("MD", md), ("PayerTxnId", payerTxnId), ("Eci", eci),
                ("ProcReturnCode", procReturnCode), ("OrderId", s3d.OrderId), ("Pan", s3d.MaskedPan),
                ("PurchAmount", amountStr), ("Currency", s3d.Currency.ToString()),
                ("TxnStat", txnStat), ("TRXDATE", trxDate)
            };
            var hashParams = InterVposHash.BuildHashParams(signed.Select(x => x.Name));
            var hash = InterVposHash.ComputeCallbackHash(signed.Select(x => x.Value), merchant?.SecretKey ?? string.Empty);

            var fields = signed.Select(x => new KeyValuePair<string, string>(x.Name, x.Value)).ToList();
            fields.Add(new("HASHPARAMS", hashParams));
            fields.Add(new("HASH", hash));

            var targetUrl = threeDPass ? s3d.OkUrl : s3d.FailUrl;
            return new Result(threeDPass, targetUrl, fields);
        }
    }
}
