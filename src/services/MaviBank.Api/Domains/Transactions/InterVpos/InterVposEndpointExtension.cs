using System.Globalization;
using MaviBank.Api.Domains.Transactions.Features.Commands;
using MaviBank.Api.Domains.Transactions.Features.Queries;

namespace MaviBank.Api.Domains.Transactions.InterVpos;

/// <summary>
/// İlke V: InterVpos ödeme yüzü. `POST /mpi/Default.aspx` — form-urlencoded; `TxnType`'a göre yönlendirir
/// (Auth / Void / Refund / StatusHistory). Cevap `;;` ayraçlı (JSON DEĞİL, text/plain). Ham istek loglanır
/// ama Pan/Cvv2 maskeli (İlke VI/VIII). Kimlik protokol credential'ında (handler fail-closed doğrular).
/// </summary>
public static class InterVposEndpointExtension
{
    public static void AddInterVposEndpoints(this WebApplication app)
    {
        app.MapPost("/mpi/Default.aspx", async (HttpRequest req, IMessageBus bus, ILoggerFactory lf,
            IDocumentSession session, CancellationToken ct) =>
            {
                var logger = lf.CreateLogger("InterVpos");
                var form = await req.ReadFormAsync(ct);
                logger.LogInformation("InterVpos istek: {Raw}", MaskRaw(form));

                var txnType = form["TxnType"].ToString();
                var secureType = form["SecureType"].ToString();

                // Faz 7 IP whitelist: merchant'ın izinli IP listesi varsa, liste dışı istek reddedilir.
                var shopCode = form["ShopCode"].ToString();
                if (!string.IsNullOrEmpty(shopCode))
                {
                    var m = await session.LoadAsync<Merchant>(shopCode, ct);
                    if (m is not null && !m.IsIpAllowed(req.HttpContext.Connection.RemoteIpAddress?.ToString()))
                        return Results.Text(SemicolonEncoder.Encode(
                            ErrorFields(InterVposConstants.ProcAuthError, "IP", "Izinli olmayan IP")),
                            "text/plain; charset=utf-8");
                }

                // 3D Secure (SecureType=3DModel/3DPay/3DHost) → sahte ACS HTML döner (`;;` değil).
                if (txnType == InterVposConstants.TxnTypeAuth && IsThreeD(secureType))
                {
                    var init = await bus.InvokeAsync<ThreeDInitiate.Result>(BuildThreeDCommand(form));
                    if (!init.Valid)
                        return Results.Text(SemicolonEncoder.Encode(
                            ErrorFields(InterVposConstants.ProcAuthError, "3D", init.ErrorMessage ?? "3D hata")),
                            "text/plain; charset=utf-8");
                    return Results.Content(ThreeDPages.AcsPage(init), "text/html; charset=utf-8");
                }

                // 3DModel completion: NonSecure + MD → ACS sonrası asıl çekim (callback nihai değildi).
                if (txnType == InterVposConstants.TxnTypeAuth
                    && secureType == InterVposConstants.SecureTypeNonSecure
                    && !string.IsNullOrEmpty(form["MD"].ToString()))
                {
                    var done = await HandleThreeDModelComplete(form, bus);
                    return Results.Text(SemicolonEncoder.Encode(done), "text/plain; charset=utf-8");
                }

                var fields = txnType switch
                {
                    InterVposConstants.TxnTypeAuth => await HandleAuth(form, bus),
                    InterVposConstants.TxnTypeVoid => await HandleVoid(form, bus),
                    InterVposConstants.TxnTypeRefund => await HandleRefund(form, bus),
                    InterVposConstants.TxnTypeStatusHistory => await HandleStatus(form, bus),
                    _ => ErrorFields(InterVposConstants.ProcAuthError, "TXN", "Desteklenmeyen TxnType")
                };

                var body = SemicolonEncoder.Encode(fields);
                logger.LogInformation("InterVpos cevap: {Body}", body); // cevap PAN/sır içermez
                return Results.Text(body, "text/plain; charset=utf-8");
            })
            .WithTags("intervpos")
            .AllowAnonymous();

        // Sahte ACS kod girişi sonrası — 3D tamamla → sonucu OkUrl/FailUrl'e auto-POST eden HTML.
        app.MapPost("/mpi/3DAcs", async (HttpRequest req, IMessageBus bus) =>
            {
                var form = await req.ReadFormAsync();
                var r = await bus.InvokeAsync<ThreeDComplete.Result>(new ThreeDComplete.Command(
                    form["sid"].ToString(), form["code"].ToString()));
                return Results.Content(ThreeDPages.CallbackAutoPost(r.TargetUrl, r.CallbackFields), "text/html; charset=utf-8");
            })
            .WithTags("intervpos")
            .AllowAnonymous();
    }

    private static bool IsThreeD(string secureType)
        => secureType is "3DModel" or "3DPay" or "3DHost";

    private static ThreeDInitiate.Command BuildThreeDCommand(IFormCollection f)
    {
        decimal.TryParse(f["PurchAmount"].ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount);
        int.TryParse(f["Currency"].ToString(), out var currency);
        int.TryParse(f["CardType"].ToString(), out var cardType);
        var installment = int.TryParse(f["InstallmentCount"].ToString(), out var inst) && inst > 1 ? inst : 0;

        return new ThreeDInitiate.Command(
            f["ShopCode"].ToString(), f["UserCode"].ToString(), f["UserPass"].ToString(),
            f["Pan"].ToString(), f["Expiry"].ToString(), f["Cvv2"].ToString(),
            f["PurchAmount"].ToString(), f["InstallmentCount"].ToString(), f["TxnType"].ToString(),
            f["SecureType"].ToString(), amount, currency, f["OrderId"].ToString(), installment, cardType,
            f["OkUrl"].ToString(), f["FailUrl"].ToString(), f["Rnd"].ToString(), f["Hash"].ToString());
    }

    // 3DModel completion (SecureType=NonSecure + MD): asıl çekim burada.
    private static async Task<List<KeyValuePair<string, string>>> HandleThreeDModelComplete(IFormCollection f, IMessageBus bus)
    {
        var r = await bus.InvokeAsync<ThreeDModelComplete.Result>(new ThreeDModelComplete.Command(
            f["ShopCode"].ToString(), f["UserCode"].ToString(), f["UserPass"].ToString(),
            f["OrderId"].ToString(), f["MD"].ToString()));

        var fields = Base(r.ProcReturnCode, r.Success ? InterVposConstants.ResponseApproved : InterVposConstants.ResponseError);
        fields.Add(new("OrderId", r.OrderId));
        if (r.Success)
        {
            fields.Add(new("TransId", r.TransId ?? string.Empty));
            fields.Add(new("AuthCode", r.AuthCode ?? string.Empty));
        }
        else
        {
            fields.Add(new("ErrorCode", r.ErrorCode ?? string.Empty));
            fields.Add(new("ErrorMessage", r.ErrorMessage ?? string.Empty));
        }
        return WithDate(fields);
    }

    // --- TxnType=Auth ---
    private static async Task<List<KeyValuePair<string, string>>> HandleAuth(IFormCollection f, IMessageBus bus)
    {
        decimal.TryParse(f["PurchAmount"].ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount);
        int.TryParse(f["Currency"].ToString(), out var currency);
        int.TryParse(f["CardType"].ToString(), out var cardType);
        var installment = int.TryParse(f["InstallmentCount"].ToString(), out var inst) && inst > 1 ? inst : 0;

        var r = await bus.InvokeAsync<Authorize.Result>(new Authorize.Command(
            f["ShopCode"].ToString(), f["UserCode"].ToString(), f["UserPass"].ToString(),
            f["Pan"].ToString(), f["Expiry"].ToString(), f["Cvv2"].ToString(),
            amount, currency, f["OrderId"].ToString(), installment, cardType));

        var o = r.Outcome;
        var fields = Base(o.ProcReturnCode, o.Response);
        if (o.IsApproved)
        {
            fields.Add(new("OrderId", r.OrderId));
            fields.Add(new("TransId", r.TransId ?? string.Empty));
            fields.Add(new("AuthCode", o.AuthCode ?? string.Empty));
        }
        else if (o.Response == InterVposConstants.ResponseDeclined)
        {
            fields.Add(new("OrderId", r.OrderId));
            fields.Add(new("ErrorCode", o.ErrorCode ?? string.Empty));
            fields.Add(new("ErrorMessage", o.ErrorMessage ?? string.Empty));
        }
        else
        {
            fields.Add(new("ErrorCode", o.ErrorCode ?? string.Empty));
            fields.Add(new("ErrorMessage", o.ErrorMessage ?? string.Empty));
        }
        return WithDate(fields);
    }

    // --- TxnType=Void ---
    private static async Task<List<KeyValuePair<string, string>>> HandleVoid(IFormCollection f, IMessageBus bus)
    {
        var r = await bus.InvokeAsync<VoidTransaction.Result>(new VoidTransaction.Command(
            f["ShopCode"].ToString(), f["UserCode"].ToString(), f["UserPass"].ToString(), f["OrderId"].ToString()));

        var fields = Base(r.ProcReturnCode, r.Success ? InterVposConstants.ResponseApproved : InterVposConstants.ResponseDeclined);
        fields.Add(new("OrderId", r.OrderId));
        if (!r.Success)
        {
            fields.Add(new("ErrorCode", r.ErrorCode ?? string.Empty));
            fields.Add(new("ErrorMessage", r.ErrorMessage ?? string.Empty));
        }
        return WithDate(fields);
    }

    // --- TxnType=Refund ---
    private static async Task<List<KeyValuePair<string, string>>> HandleRefund(IFormCollection f, IMessageBus bus)
    {
        decimal.TryParse(f["PurchAmount"].ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount);
        var r = await bus.InvokeAsync<Refund.Result>(new Refund.Command(
            f["ShopCode"].ToString(), f["UserCode"].ToString(), f["UserPass"].ToString(), f["OrderId"].ToString(), amount));

        var fields = Base(r.ProcReturnCode, r.Success ? InterVposConstants.ResponseApproved : InterVposConstants.ResponseDeclined);
        fields.Add(new("OrderId", r.OrderId));
        fields.Add(new("RefundedAmount", r.RefundedAmount.ToString("0.00", CultureInfo.InvariantCulture)));
        if (!r.Success)
        {
            fields.Add(new("ErrorCode", r.ErrorCode ?? string.Empty));
            fields.Add(new("ErrorMessage", r.ErrorMessage ?? string.Empty));
        }
        return WithDate(fields);
    }

    // --- TxnType=StatusHistory ---
    private static async Task<List<KeyValuePair<string, string>>> HandleStatus(IFormCollection f, IMessageBus bus)
    {
        var r = await bus.InvokeAsync<StatusHistory.Result>(new StatusHistory.Query(
            f["ShopCode"].ToString(), f["OrderId"].ToString()));

        if (!r.Found)
            return WithDate(Base(r.ProcReturnCode, InterVposConstants.ResponseError)
                .Concat(new[]
                {
                    new KeyValuePair<string, string>("ErrorCode", r.ErrorCode ?? string.Empty),
                    new KeyValuePair<string, string>("ErrorMessage", r.ErrorMessage ?? string.Empty)
                }).ToList());

        var fields = new List<KeyValuePair<string, string>>
        {
            new("ProcReturnCode", r.ProcReturnCode),
            new("OrderId", r.OrderId),
            new("TxnStat", r.TxnStat),
            new("VoidDate", r.VoidDate?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty),
            new("RefundedAmount", r.RefundedAmount.ToString("0.00", CultureInfo.InvariantCulture))
        };
        return WithDate(fields);
    }

    // --- Yardımcılar ---
    private static List<KeyValuePair<string, string>> Base(string procReturnCode, string response)
        => new() { new("ProcReturnCode", procReturnCode), new("Response", response) };

    private static List<KeyValuePair<string, string>> ErrorFields(string proc, string code, string message)
        => WithDate(new List<KeyValuePair<string, string>>
        {
            new("ProcReturnCode", proc), new("Response", InterVposConstants.ResponseError),
            new("ErrorCode", code), new("ErrorMessage", message)
        });

    private static List<KeyValuePair<string, string>> WithDate(List<KeyValuePair<string, string>> fields)
    {
        fields.Add(new("TRXDATE", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)));
        return fields;
    }

    // Ham log — Pan/Cvv2 maskeli (İlke VI/VIII: tel şekli görünür, sır görünmez).
    private static string MaskRaw(IFormCollection f)
        => string.Join(";;", f.Select(kv => kv.Key switch
        {
            "Pan" => $"Pan={SemicolonEncoder.MaskPan(kv.Value.ToString())}",
            "Cvv2" => $"Cvv2={SemicolonEncoder.MaskCvv(kv.Value.ToString())}",
            _ => $"{kv.Key}={kv.Value}"
        }));
}
