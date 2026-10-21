namespace Simulator.Core;

// Core domain'in Marten şema yapılandırması (identity + event stream + Batch aktif-tekil). Her host kendi
// DatabaseSchemaName'iyle (banka başına ayrı şema) AddMarten içinde `opts.ConfigureCoreSchema()` çağırır.
public static class MartenRegistration
{
    public static void ConfigureCoreSchema(this StoreOptions opts)
    {
        // İşlem defteri event stream kimliği string: `ShopCode:OrderId`.
        opts.Events.StreamIdentity = JasperFx.Events.StreamIdentity.AsString;

        opts.Schema.For<Merchant>().Identity(x => x.ShopCode);
        opts.Schema.For<Card>().Identity(x => x.Pan);
        opts.Schema.For<ThreeDSession>().Identity(x => x.Sid);
        opts.Schema.For<Account>().Identity(x => x.ShopCode);
        opts.Schema.For<MerchantApplication>().Identity(x => x.Id);

        // Batch kimliği + merchant başına tek AÇIK batch (aktif-tekil, partial unique; Status=0 Open).
        opts.Schema.For<Batch>().Identity(x => x.BatchNo)
            .Index(x => x.ShopCode, idx =>
            {
                idx.IsUnique = true;
                idx.Predicate = "(data ->> 'Status') = '0'";
            });
    }
}
