using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Collectify.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Collectify.PostgresTests;

public sealed class PostgresMigrationLineageTests
{
    private static readonly string[] Ids =
    [
        "20260528000000_InitialPostgres", "20260817000000_AddStoreImportAndDlc",
        "20260818000000_DropLookupCache", "20260820000000_MultiDigitalStores",
        "20260821000000_AddRichDetailFields"
    ];

    public static TheoryData<string, string, string> Prefixes => new()
    {
        { "P0", Ids[0], "0828c4caff5cc6290eff701a7005a7de58902a0f" },
        { "P1", Ids[1], "60455f79dbd041f1712ba4ecae289bebdfe7cc36" },
        { "P2", Ids[2], "b2579b407162cebead3a2925d041c39daec6cc15" },
        { "P3", Ids[3], "5881b72a08e3f5a080720bf05c168d29347faf60" },
        { "P4", Ids[4], "86500ebd2648793e4be1a80c028bcbffcc6b51ce" }
    };

    [Fact]
    public void MigrationAssembly_ContainsExactOrderedFiveIdLineage()
    {
        using var services = new ServiceCollection().AddDbContext<CollectifyDbContext>(options =>
            options.UseNpgsql("Host=127.0.0.1;Database=collectify;Username=collectify",
                x => x.MigrationsAssembly("Collectify.PostgresMigrations"))).BuildServiceProvider();
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CollectifyDbContext>();
        var assembly = context.GetService<IMigrationsAssembly>();
        Assert.Equal("Collectify.PostgresMigrations", assembly.Assembly.GetName().Name);
        Assert.Equal(Ids, assembly.Migrations.Keys);
        Assert.Equal(Ids.Select(x => x[(x.IndexOf('_') + 1)..]), assembly.Migrations.Values.Select(x => x.Name));
        Assert.All(assembly.Migrations.Values, type => Assert.Equal("Collectify.PostgresMigrations.Migrations", type.Namespace));
        Assert.All(assembly.Migrations.Keys, id => Assert.Equal("10.0.11", assembly.CreateMigration(assembly.Migrations[id], "10.0.11").TargetModel.GetProductVersion()));
    }

    [Theory]
    [MemberData(nameof(Prefixes))]
    public async Task MigrationPrefix_MatchesHistoricalCatalogManifest(string family, string target, string fixtureCommit)
    {
        var provenance = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "generated", "provenance.json")));
        var variants = provenance.RootElement.GetProperty("states").EnumerateArray().Where(x => x.GetProperty("family").GetString() == family)
            .Select(x => x.GetProperty("variant").GetString()).Distinct().ToArray();
        Assert.Single(variants);
        await using var container = new PostgreSqlBuilder("postgres:17-alpine@sha256:d4bb0a8c1b7bb2e29f976d099e7bfb9a5d8858cffe9e46b35cd302cd1f1f8168").Build();
        await container.StartAsync();
        var options = new DbContextOptionsBuilder<CollectifyDbContext>().UseNpgsql(container.GetConnectionString(),
            x => x.MigrationsAssembly("Collectify.PostgresMigrations")).Options;
        await using (var context = new CollectifyDbContext(options)) await context.Database.MigrateAsync(target);
        await using var connection = new NpgsqlConnection(container.GetConnectionString()); await connection.OpenAsync();
        await using (var history = new NpgsqlCommand("SELECT \"MigrationId\",\"ProductVersion\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\"", connection))
        await using (var reader = await history.ExecuteReaderAsync())
        {
            var rows = new List<(string, string)>(); while (await reader.ReadAsync()) rows.Add((reader.GetString(0), reader.GetString(1)));
            var count = Array.IndexOf(Ids, target) + 1;
            Assert.Equal(Ids.Take(count).Select(x => (x, "10.0.11")), rows);
        }
        var actual = await ExtractCatalogAsync(connection);
        var expected = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "generated", fixtureCommit, "catalog-manifest.json"));
        if (!expected.SequenceEqual(actual))
        {
            var expectedText = Encoding.UTF8.GetString(expected);
            var actualText = Encoding.UTF8.GetString(actual);
            var index = Enumerable.Range(0, Math.Min(expectedText.Length, actualText.Length)).First(i => expectedText[i] != actualText[i]);
            var start = Math.Max(0, index - 200);
            Assert.Fail($"catalog differs at {index}\nexpected: {expectedText.Substring(start, Math.Min(500, expectedText.Length - start))}\nactual: {actualText.Substring(start, Math.Min(500, actualText.Length - start))}");
        }
    }

    private static async Task<byte[]> ExtractCatalogAsync(NpgsqlConnection connection)
    {
        const string objects = "SELECT c.oid,c.relname,c.relkind,c.relpersistence,c.relrowsecurity,c.relforcerowsecurity,c.relacl,c.relowner FROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='public' AND c.relname<>'__EFMigrationsHistory' AND c.relname<>'PK___EFMigrationsHistory'";
        var queries = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["databaseSchema"] = "SELECT json_build_object('databaseOwnerIsCurrentUser',(SELECT pg_get_userbyid(datdba)=current_user FROM pg_database WHERE datname=current_database()),'schemaOwnerIsDatabaseOwner',(SELECT nspowner=(SELECT datdba FROM pg_database WHERE datname=current_database()) OR pg_get_userbyid(nspowner)='pg_database_owner' FROM pg_namespace WHERE nspname='public'),'currentUserHasUsage',has_schema_privilege(current_user,'public','USAGE'),'currentUserHasCreate',has_schema_privilege(current_user,'public','CREATE'),'publicHasCreate',has_schema_privilege('public','public','CREATE'))::text",
            ["relations"] = $"SELECT json_build_object('name',relname,'kind',relkind,'persistence',relpersistence,'ownerIsCurrentUser',pg_has_role(current_user,relowner,'MEMBER'),'acl',coalesce(array_to_json(relacl)::json,'[]'::json))::text FROM ({objects}) o ORDER BY relname",
            ["columns"] = $"SELECT json_build_object('relation',c.relname,'name',a.attname,'typeOid',a.atttypid,'typeName',format_type(a.atttypid,a.atttypmod),'typmod',a.atttypmod,'length',a.attlen,'notNull',a.attnotnull,'collation',CASE WHEN a.attcollation=0 THEN NULL ELSE co.collname END,'default',pg_get_expr(d.adbin,d.adrelid),'identity',a.attidentity,'generated',a.attgenerated,'ownedSequence',(SELECT s.relname FROM pg_depend dep JOIN pg_class s ON s.oid=dep.objid WHERE dep.refobjid=a.attrelid AND dep.refobjsubid=a.attnum AND dep.classid='pg_class'::regclass AND s.relkind='S' LIMIT 1))::text FROM ({objects}) c JOIN pg_attribute a ON a.attrelid=c.oid LEFT JOIN pg_attrdef d ON d.adrelid=a.attrelid AND d.adnum=a.attnum LEFT JOIN pg_collation co ON co.oid=a.attcollation WHERE c.relkind IN ('r','p') AND a.attnum>0 AND NOT a.attisdropped ORDER BY c.relname,a.attname",
            ["constraints"] = $"SELECT json_build_object('relation',c.relname,'name',con.conname,'type',con.contype,'columns',(SELECT coalesce(json_agg(a.attname ORDER BY u.ord),'[]') FROM unnest(con.conkey) WITH ORDINALITY u(attnum,ord) JOIN pg_attribute a ON a.attrelid=con.conrelid AND a.attnum=u.attnum),'referencedRelation',rc.relname,'referencedColumns',(SELECT coalesce(json_agg(a.attname ORDER BY u.ord),'[]') FROM unnest(con.confkey) WITH ORDINALITY u(attnum,ord) JOIN pg_attribute a ON a.attrelid=con.confrelid AND a.attnum=u.attnum),'matchType',con.confmatchtype,'updateAction',con.confupdtype,'deleteAction',con.confdeltype,'validated',con.convalidated,'deferrable',con.condeferrable,'initiallyDeferred',con.condeferred,'definition',pg_get_constraintdef(con.oid,true))::text FROM pg_constraint con JOIN ({objects}) c ON c.oid=con.conrelid LEFT JOIN pg_class rc ON rc.oid=con.confrelid ORDER BY c.relname,con.conname",
            ["indexes"] = $"SELECT json_build_object('relation',t.relname,'name',i.relname,'unique',x.indisunique,'valid',x.indisvalid,'ready',x.indisready,'method',am.amname,'keyCount',x.indnkeyatts,'columns',(SELECT coalesce(json_agg(pg_get_indexdef(x.indexrelid,k,TRUE) ORDER BY k),'[]') FROM generate_series(1,x.indnatts) k),'operatorClasses',(SELECT coalesce(json_agg(opc.opcname ORDER BY u.ord),'[]') FROM unnest(x.indclass::oid[]) WITH ORDINALITY u(oid,ord) JOIN pg_opclass opc ON opc.oid=u.oid),'collations',(SELECT coalesce(json_agg(CASE WHEN u.oid=0 THEN NULL ELSE col.collname END ORDER BY u.ord),'[]') FROM unnest(x.indcollation::oid[]) WITH ORDINALITY u(oid,ord) LEFT JOIN pg_collation col ON col.oid=u.oid),'options',x.indoption::text,'expressions',pg_get_expr(x.indexprs,x.indrelid),'predicate',pg_get_expr(x.indpred,x.indrelid))::text FROM pg_index x JOIN ({objects}) t ON t.oid=x.indrelid JOIN pg_class i ON i.oid=x.indexrelid JOIN pg_am am ON am.oid=i.relam ORDER BY t.relname,i.relname",
            ["sequences"] = $"SELECT json_build_object('name',c.relname,'dataType',format_type(s.seqtypid,NULL),'start',s.seqstart,'increment',s.seqincrement,'minimum',s.seqmin,'maximum',s.seqmax,'cache',s.seqcache,'cycle',s.seqcycle,'ownerIsCurrentUser',pg_has_role(current_user,c.relowner,'MEMBER'),'dependencyType',d.deptype,'ownedRelation',t.relname,'ownedColumn',a.attname)::text FROM pg_sequence s JOIN ({objects}) c ON c.oid=s.seqrelid LEFT JOIN pg_depend d ON d.objid=c.oid AND d.classid='pg_class'::regclass AND d.refclassid='pg_class'::regclass AND d.refobjsubid>0 LEFT JOIN pg_class t ON t.oid=d.refobjid LEFT JOIN pg_attribute a ON a.attrelid=d.refobjid AND a.attnum=d.refobjsubid ORDER BY c.relname",
            ["triggers"] = $"SELECT json_build_object('relation',c.relname,'name',t.tgname,'enabled',t.tgenabled,'definition',pg_get_triggerdef(t.oid,true))::text FROM pg_trigger t JOIN ({objects}) c ON c.oid=t.tgrelid WHERE NOT t.tgisinternal ORDER BY c.relname,t.tgname",
            ["rewriteRules"] = $"SELECT json_build_object('relation',c.relname,'name',r.rulename,'event',r.ev_type,'instead',r.is_instead,'enabled',r.ev_enabled,'definition',pg_get_ruledef(r.oid,true))::text FROM pg_rewrite r JOIN ({objects}) c ON c.oid=r.ev_class WHERE r.rulename<>'_RETURN' ORDER BY c.relname,r.rulename",
            ["rls"] = $"SELECT json_build_object('relation',relname,'enabled',relrowsecurity,'forced',relforcerowsecurity)::text FROM ({objects}) o WHERE relkind IN ('r','p') ORDER BY relname",
            ["policies"] = $"SELECT json_build_object('relation',c.relname,'name',p.polname,'permissive',p.polpermissive,'roles',p.polroles::text,'command',p.polcmd,'using',pg_get_expr(p.polqual,p.polrelid),'check',pg_get_expr(p.polwithcheck,p.polrelid))::text FROM pg_policy p JOIN ({objects}) c ON c.oid=p.polrelid ORDER BY c.relname,p.polname",
            ["inboundDependencies"] = $"SELECT json_build_object('sourceClass',d.classid::regclass::text,'sourceIdentity',pg_describe_object(d.classid,d.objid,d.objsubid),'targetRelation',c.relname,'targetColumn',a.attname,'dependencyType',d.deptype)::text FROM pg_depend d JOIN ({objects}) c ON c.oid=d.refobjid LEFT JOIN pg_attribute a ON a.attrelid=c.oid AND a.attnum=d.refobjsubid AND d.refobjsubid>0 LEFT JOIN ({objects}) own ON own.oid=d.objid LEFT JOIN pg_class source_class ON d.classid='pg_class'::regclass AND source_class.oid=d.objid LEFT JOIN pg_namespace source_namespace ON source_namespace.oid=source_class.relnamespace LEFT JOIN pg_constraint source_constraint ON d.classid='pg_constraint'::regclass AND source_constraint.oid=d.objid LEFT JOIN ({objects}) constraint_owner ON constraint_owner.oid=source_constraint.conrelid LEFT JOIN pg_rewrite source_rule ON d.classid='pg_rewrite'::regclass AND source_rule.oid=d.objid LEFT JOIN ({objects}) rule_owner ON rule_owner.oid=source_rule.ev_class WHERE d.deptype<>'i' AND own.oid IS NULL AND constraint_owner.oid IS NULL AND rule_owner.oid IS NULL AND d.classid<>'pg_type'::regclass AND coalesce(source_namespace.nspname,'') NOT IN ('pg_catalog','pg_toast') AND pg_describe_object(d.classid,d.objid,d.objsubid) !~ '(^| )pg_(catalog|toast)\\.' ORDER BY c.relname,a.attname NULLS FIRST,d.classid::regclass::text,pg_describe_object(d.classid,d.objid,d.objsubid),d.deptype"
        };
        var result = new SortedDictionary<string, List<JsonElement>>(StringComparer.Ordinal);
        foreach (var (category, sql) in queries)
        {
            await using var command = new NpgsqlCommand(sql, connection); await using var reader = await command.ExecuteReaderAsync();
            var rows = new List<JsonElement>(); while (await reader.ReadAsync()) rows.Add(JsonDocument.Parse(reader.GetString(0)).RootElement.Clone());
            rows.Sort((a, b) => StringComparer.Ordinal.Compare(Canonical(a), Canonical(b))); result[category] = rows;
        }
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartObject(); foreach (var (category, rows) in result) { writer.WritePropertyName(category); writer.WriteStartArray(); foreach (var row in rows) WriteCanonical(writer, row); writer.WriteEndArray(); } writer.WriteEndObject();
        }
        stream.WriteByte((byte)'\n'); return stream.ToArray();
    }

    private static string Canonical(JsonElement value) { using var stream = new MemoryStream(); using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping })) WriteCanonical(writer, value); return Encoding.UTF8.GetString(stream.ToArray()); }
    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object) { writer.WriteStartObject(); foreach (var property in value.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal)) { writer.WritePropertyName(property.Name); WriteCanonical(writer, property.Value); } writer.WriteEndObject(); }
        else if (value.ValueKind == JsonValueKind.Array) { writer.WriteStartArray(); foreach (var item in value.EnumerateArray()) WriteCanonical(writer, item); writer.WriteEndArray(); }
        else value.WriteTo(writer);
    }
}
