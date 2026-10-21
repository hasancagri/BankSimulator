using System.Globalization;
using System.Xml.Linq;

namespace TuruncuBank.Api.TuruncuPos;

/// <summary>
/// TuruncuBank facade — İlke VII: MaviBank'tan (InterVpos form+`;;`+SHA1) FARKLI protokol: **XML istek/cevap
/// + SHA512 hash**, `POST /turuncubank/pos`. Ama Core AYNI — XML parse + hash doğrula → paylaşılan
/// `Authorize` komutu (Transaction/Batch/Merchant/ChargeScenarioEngine değişmez) → XML cevap. Facade yalnız
/// protokol çevirmeni (anti-corruption); domain bank-agnostik. Kart verisi transient (saklanmaz, İlke VI).
/// </summary>
public static class TuruncuPosEndpointExtension
{
    public static void AddTuruncuPosEndpoints(this WebApplication app)
    {
        app.MapPost("/turuncubank/pos", async (HttpRequest req, IMessageBus bus, IDocumentSession session, CancellationToken ct) =>
            {
                using var reader = new StreamReader(req.Body);
                var xml = await reader.ReadToEndAsync(ct);

                XDocument doc;
                try { doc = XDocument.Parse(xml); }
                catch { return Xml(ErrorResponse("99", "Gecersiz XML")); }

                var r = doc.Root;
                string Get(string n) => r?.Element(n)?.Value ?? string.Empty;
                var shopCode = Get("ShopCode");
                var orderId = Get("OrderId");
                var amountRaw = Get("Amount");

                var merchant = await session.LoadAsync<Merchant>(shopCode, ct);
                if (merchant is null)
                    return Xml(ErrorResponse("99", "Gecersiz uye isyeri"));

                // SHA512 hash doğrulama (rotasyon overlap'i dahil — Faz 7).
                var hashOk = merchant.ValidSecretKeys(DateTime.UtcNow)
                    .Any(secret => TuruncuHash.Compute(shopCode, orderId, amountRaw, secret) == Get("Hash"));
                if (!hashOk)
                    return Xml(ErrorResponse("99", "Gecersiz hash"));

                decimal.TryParse(amountRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount);
                int.TryParse(Get("Currency"), out var currency);

                // Paylaşılan Core — aynı Authorize komutu (protokol-agnostik).
                var result = await bus.InvokeAsync<Authorize.Result>(new Authorize.Command(
                    shopCode, Get("UserCode"), Get("UserPass"), Get("Pan"), Get("Expiry"), Get("Cvv"),
                    amount, currency, orderId, 0, 0));

                var o = result.Outcome;
                if (o.IsApproved)
                    return Xml(new XElement("PaymentResponse",
                        new XElement("ResultCode", "00"),
                        new XElement("ResultMessage", "Approved"),
                        new XElement("OrderId", result.OrderId),
                        new XElement("TransId", result.TransId ?? string.Empty),
                        new XElement("AuthCode", o.AuthCode ?? string.Empty)));

                return Xml(new XElement("PaymentResponse",
                    new XElement("ResultCode", o.ProcReturnCode),
                    new XElement("ResultMessage", o.ErrorMessage ?? "Declined"),
                    new XElement("OrderId", result.OrderId),
                    new XElement("ErrorCode", o.ErrorCode ?? string.Empty)));
            })
            .WithTags("turuncubank")
            .AllowAnonymous();
    }

    private static XElement ErrorResponse(string code, string message)
        => new("PaymentResponse", new XElement("ResultCode", code), new XElement("ResultMessage", message));

    private static IResult Xml(XElement el) => Results.Content(el.ToString(SaveOptions.DisableFormatting), "application/xml; charset=utf-8");
}
