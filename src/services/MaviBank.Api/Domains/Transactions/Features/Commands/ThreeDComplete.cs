using System.Globalization;

namespace MaviBank.Api.Domains.Transactions.Features.Commands;

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

            s3d.Complete();
            session.Store(s3d);

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

                // Çekim (3D doğrulandı) — senaryo kararı (gerçek bakiye yok).
                var outcome = engine.Evaluate(new Card { Scenario = s3d.Scenario });
                var streamKey = $"{s3d.ShopCode}:{s3d.OrderId}";
                var existing = await session.Events.AggregateStreamAsync<Transaction>(streamKey, token: ct);

                if (existing is null && outcome.IsApproved)
                {
                    var batch = await session.Query<Batch>()
                        .Where(b => b.ShopCode == s3d.ShopCode && b.Status == BatchStatus.Open)
                        .FirstOrDefaultAsync(ct) ?? Batch.OpenFor(s3d.ShopCode, now);
                    batch.AddTransaction(streamKey);
                    session.Store(batch);

                    session.Events.StartStream<Transaction>(streamKey, new TransactionAuthorized(
                        s3d.ShopCode, s3d.OrderId, batch.BatchNo, Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
                        outcome.AuthCode!, s3d.Amount, s3d.Currency, s3d.Installment, s3d.MaskedPan, s3d.CardType, now));
                    procReturnCode = InterVposConstants.ProcApproved;
                    txnStat = nameof(TransactionStatus.Authorized);
                }
                else if (existing is null)
                {
                    session.Events.StartStream<Transaction>(streamKey, new TransactionDeclined(
                        s3d.ShopCode, s3d.OrderId, outcome.ProcReturnCode, outcome.ErrorCode ?? string.Empty,
                        outcome.ErrorMessage ?? string.Empty, s3d.Amount, s3d.MaskedPan, now));
                    procReturnCode = outcome.ProcReturnCode;
                    txnStat = nameof(TransactionStatus.Declined);
                }
                else
                {
                    // İdempotent tekrar — mevcut durum.
                    procReturnCode = existing.ProcReturnCode ?? InterVposConstants.ProcApproved;
                    txnStat = existing.Status.ToString();
                }
            }
            else
            {
                threeDStatus = "0"; // 3D başarısız → çekim yok
                procReturnCode = InterVposConstants.ProcDoNotHonour;
                txnStat = "3D-Fail";
            }

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
