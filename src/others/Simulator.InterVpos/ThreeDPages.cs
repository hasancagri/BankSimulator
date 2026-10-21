using System.Net;
using System.Globalization;

namespace Simulator.InterVpos;

/// <summary>
/// Sahte ACS "MaviSecure" + callback auto-POST HTML (gömülü statik + saf JS — Blazor/SPA red, pivot kararı).
/// Gerçek bir 3D Host sayfasını taklit eder: işlem özeti + SMS kod ekranı; sonuç OkUrl/FailUrl'e auto-POST.
/// Store-dışı metin HtmlEncode'lanır. Kod ekranda gösterilir (ICodeDeliveryChannel demo; gerçek SMS yok).
/// </summary>
public static class ThreeDPages
{
    public static string AcsPage(ThreeDInitiate.Result init)
    {
        var tr = CultureInfo.GetCultureInfo("tr-TR");
        var sid = WebUtility.HtmlEncode(init.Sid);
        var order = WebUtility.HtmlEncode(init.OrderId);
        var amount = WebUtility.HtmlEncode(init.Amount.ToString("N2", tr) + " TL");
        var pan = WebUtility.HtmlEncode(init.MaskedPan);
        var code = WebUtility.HtmlEncode(init.DeliveredCode);

        return $$"""
        <!doctype html>
        <html lang="tr"><head><meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1"><title>MaviSecure</title>
        <style>
          body{margin:0;min-height:100vh;display:flex;align-items:center;justify-content:center;
            background:#eef2ff;font-family:-apple-system,Segoe UI,Roboto,Arial,sans-serif;color:#1e293b;padding:24px}
          .card{background:#fff;border-radius:16px;box-shadow:0 10px 30px rgba(0,0,0,.1);padding:28px;max-width:400px;width:100%}
          h1{margin:0 0 4px;font-size:18px;color:#3730a3}.sub{margin:0 0 18px;font-size:13px;color:#64748b}
          .row{display:flex;justify-content:space-between;padding:8px 0;border-top:1px solid #f1f5f9;font-size:14px}
          .row .v{font-weight:600}
          .hint{margin:14px 0 6px;font-size:13px;color:#475569}
          input{width:100%;padding:11px;border:1px solid #cbd5e1;border-radius:8px;font-size:16px;letter-spacing:3px;text-align:center}
          .btns{display:flex;gap:10px;margin-top:18px}
          button{flex:1;border:0;cursor:pointer;font-size:15px;font-weight:600;padding:13px;border-radius:10px}
          .ok{background:#3730a3;color:#fff}.no{background:#e2e8f0;color:#334155}
        </style></head><body>
          <div class="card">
            <h1>MaviSecure — 3D Doğrulama</h1>
            <p class="sub">Bu bir SİMÜLATÖR ACS ekranıdır (gerçek SMS gönderilmez).</p>
            <div class="row"><span>Sipariş</span><span class="v">{{order}}</span></div>
            <div class="row"><span>Kart</span><span class="v">{{pan}}</span></div>
            <div class="row"><span>Tutar</span><span class="v">{{amount}}</span></div>
            <p class="hint">Telefonunuza gelen kod: <b>{{code}}</b></p>
            <form id="f" method="post" action="/mpi/3DAcs">
              <input type="hidden" name="sid" value="{{sid}}">
              <input id="code" name="code" inputmode="numeric" placeholder="SMS kodu" autocomplete="one-time-code">
              <div class="btns">
                <button type="button" class="no" onclick="reject()">Reddet</button>
                <button type="submit" class="ok">Onayla</button>
              </div>
            </form>
          </div>
          <script>
            // Reddet → boş/yanlış kod gönder (doğrulama başarısız → 3DStatus=0 → FailUrl).
            function reject(){ document.getElementById('code').value=''; document.getElementById('f').submit(); }
          </script>
        </body></html>
        """;
    }

    // Banka cevabını OkUrl/FailUrl'e auto-POST eden form (tarayıcı merchant'a döner). HASH/HASHPARAMS dahil.
    public static string CallbackAutoPost(string targetUrl, IReadOnlyList<KeyValuePair<string, string>> fields)
    {
        var action = WebUtility.HtmlEncode(targetUrl);
        var inputs = string.Concat(fields.Select(f =>
            $"<input type=\"hidden\" name=\"{WebUtility.HtmlEncode(f.Key)}\" value=\"{WebUtility.HtmlEncode(f.Value)}\">"));

        return $$"""
        <!doctype html>
        <html lang="tr"><head><meta charset="utf-8"><title>Yönlendiriliyor</title></head>
        <body onload="document.getElementById('cb').submit()">
          <form id="cb" method="post" action="{{action}}">{{inputs}}</form>
          <noscript><button type="submit" form="cb">Devam</button></noscript>
          <p>İşlem sonucu mağazaya iletiliyor…</p>
        </body></html>
        """;
    }
}
