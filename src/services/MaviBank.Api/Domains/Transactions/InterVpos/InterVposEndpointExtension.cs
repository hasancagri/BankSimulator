using System.Globalization;
using MaviBank.Api.Domains.Transactions.Features.Commands;

namespace MaviBank.Api.Domains.Transactions.InterVpos;

/// <summary>
/// İlke V: InterVpos ödeme yüzü. `POST /mpi/Default.aspx` — form-urlencoded istek → `Authorize` →
/// `;;` ayraçlı cevap (JSON DEĞİL, text/plain). Ham istek loglanır ama Pan/Cvv2 maskeli (İlke VI/VIII).
/// Anonim: kimlik protokol credential'ında (ShopCode/UserCode/UserPass), handler fail-closed doğrular.
/// </summary>
public static class InterVposEndpointExtension
{
    public static void AddInterVposEndpoints(this WebApplication app)
    {
        app.MapPost("/mpi/Default.aspx", async (HttpRequest req, IMessageBus bus, ILoggerFactory lf) =>
            {
                var logger = lf.CreateLogger("InterVpos");
                var form = await req.ReadFormAsync();
                var wire = ParseAuth(form);

                logger.LogInformation("InterVpos istek: {Raw}", MaskRaw(form));

                var result = await bus.InvokeAsync<Authorize.Result>(new Authorize.Command(
                    wire.ShopCode, wire.UserCode, wire.UserPass, wire.Pan, wire.Expiry, wire.Cvv2,
                    wire.PurchAmount, wire.Currency, wire.OrderId, wire.InstallmentCount, wire.CardType));

                var body = SemicolonEncoder.Encode(BuildResponseFields(result));
                logger.LogInformation("InterVpos cevap: {Body}", body); // cevap PAN/sır içermez
                return Results.Text(body, "text/plain; charset=utf-8");
            })
            .WithTags("intervpos")
            .AllowAnonymous();
    }

    private static AuthRequestWire ParseAuth(IFormCollection f)
    {
        decimal.TryParse(f["PurchAmount"].ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount);
        int.TryParse(f["Currency"].ToString(), out var currency);
        int.TryParse(f["CardType"].ToString(), out var cardType);

        // InstallmentCount: InterVpos'ta 0/1 → tek çekim (boş), 2+ → taksit.
        var instRaw = f["InstallmentCount"].ToString();
        var installment = int.TryParse(instRaw, out var inst) && inst > 1 ? inst : 0;

        return new AuthRequestWire(
            f["ShopCode"].ToString(), f["UserCode"].ToString(), f["UserPass"].ToString(),
            f["TxnType"].ToString(), f["SecureType"].ToString(),
            f["Pan"].ToString(), f["Expiry"].ToString(), f["Cvv2"].ToString(),
            amount, currency, f["OrderId"].ToString(), installment, cardType);
    }

    // Cevap alanları — sıralı (İlke V tel sırası). Response tipine göre alan seti değişir.
    private static List<KeyValuePair<string, string>> BuildResponseFields(Authorize.Result r)
    {
        var o = r.Outcome;
        var trxDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        var f = new List<KeyValuePair<string, string>>
        {
            new("ProcReturnCode", o.ProcReturnCode),
            new("Response", o.Response)
        };

        if (o.IsApproved)
        {
            f.Add(new("OrderId", r.OrderId));
            f.Add(new("TransId", r.TransId ?? string.Empty));
            f.Add(new("AuthCode", o.AuthCode ?? string.Empty));
        }
        else if (o.Response == InterVposConstants.ResponseDeclined)
        {
            f.Add(new("OrderId", r.OrderId));
            f.Add(new("ErrorCode", o.ErrorCode ?? string.Empty));
            f.Add(new("ErrorMessage", o.ErrorMessage ?? string.Empty));
        }
        else // Error — kimlik/biçim; işlem doğmadı (OrderId taşınmaz, contract)
        {
            f.Add(new("ErrorCode", o.ErrorCode ?? string.Empty));
            f.Add(new("ErrorMessage", o.ErrorMessage ?? string.Empty));
        }

        f.Add(new("TRXDATE", trxDate));
        return f;
    }

    // Ham log — Pan/Cvv2 maskeli, kalanı olduğu gibi (İlke VI/VIII: tel şekli görünür, sır görünmez).
    private static string MaskRaw(IFormCollection f)
        => string.Join(";;", f.Select(kv => kv.Key switch
        {
            "Pan" => $"Pan={SemicolonEncoder.MaskPan(kv.Value.ToString())}",
            "Cvv2" => $"Cvv2={SemicolonEncoder.MaskCvv(kv.Value.ToString())}",
            _ => $"{kv.Key}={kv.Value}"
        }));
}
