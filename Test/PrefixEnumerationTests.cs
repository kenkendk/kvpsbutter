namespace Test;

/// <summary>
/// Listing the distinct key prefixes at one level, natively or by scanning the keys
/// </summary>
[TestClass]
public class PrefixEnumerationTests
{
    private const string Folder = "./prefixtest";

    [TestInitialize]
    public void Setup()
    {
        var folder = Path.GetFullPath(Folder);
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, true);

            // Windows has a quirk here
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                Thread.Sleep(500);
        }

        Directory.CreateDirectory(folder);
    }

    private static async Task WriteKeys(IKVPS kvps, params string[] keys)
    {
        foreach (var key in keys)
            await kvps.WriteAsync(key, new MemoryStream(new byte[] { 1, 2, 3 }));
    }

    private static List<KVPPrefix> List(IKVPS kvps, KVPSQuery? query, string delimiter)
        => kvps.EnumeratePrefixesAsync(query, delimiter).ToBlockingEnumerable().ToList();

    [TestMethod]
    [DataRow("memory://")]
    [DataRow("file://prefixtest?pathmapped=false")]
    [DataRow("file://prefixtest?pathmapped=true")]
    public async Task ShouldReturnEachTopLevelPrefixOnce(string connectionString)
    {
        using var kvps = KVPSLoader.CreateIKVPS(connectionString);
        await WriteKeys(kvps, "o1/a.json", "o1/b.json", "o2/c.json", "o3/x/y.json", "top.json");

        var prefixes = List(kvps, null, "/").Select(x => x.Prefix);

        prefixes.Should().BeEquivalentTo(new[] { "o1/", "o2/", "o3/" });
    }

    [TestMethod]
    [DataRow("memory://")]
    [DataRow("file://prefixtest?pathmapped=false")]
    [DataRow("file://prefixtest?pathmapped=true")]
    public async Task ShouldReturnPrefixesBelowTheQueryPrefix(string connectionString)
    {
        using var kvps = KVPSLoader.CreateIKVPS(connectionString);
        await WriteKeys(kvps, "o3/x/y.json", "o3/x/z.json", "o3/w/v.json", "o3/file.json", "o4/x/y.json");

        var prefixes = List(kvps, KVPSQuery.Empty.WithPrefix("o3/"), "/").Select(x => x.Prefix);

        prefixes.Should().BeEquivalentTo(new[] { "o3/x/", "o3/w/" });
    }

    [TestMethod]
    [DataRow("memory://")]
    [DataRow("file://prefixtest?pathmapped=false")]
    [DataRow("file://prefixtest?pathmapped=true")]
    public async Task ShouldResumeAfterTheCursorWithoutRepeatingPrefixes(string connectionString)
    {
        using var kvps = KVPSLoader.CreateIKVPS(connectionString);
        await WriteKeys(kvps, "o1/a.json", "o1/b.json", "o2/c.json", "o3/d.json", "o4/e.json");

        var first = List(kvps, new KVPSQuery(null, null, 2, null), "/");
        first.Should().HaveCount(2);

        var rest = List(kvps, new KVPSQuery(null, null, null, first.Last().Cursor), "/");

        first.Concat(rest).Select(x => x.Prefix).Should().OnlyHaveUniqueItems()
            .And.BeEquivalentTo(new[] { "o1/", "o2/", "o3/", "o4/" });
    }

    [TestMethod]
    public void ShouldRejectACursorFromAnotherSource()
    {
        using var kvps = KVPSLoader.CreateIKVPS("memory://");

        var act = () => List(kvps, new KVPSQuery(null, null, null, "cv1:1:token"), "/");

        act.Should().Throw<InvalidCursorException>();
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(null)]
    public void ShouldRejectAnEmptyDelimiter(string? delimiter)
    {
        using var kvps = KVPSLoader.CreateIKVPS("memory://");

        var act = () => List(kvps, null, delimiter!);

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public async Task KeyWrapperShouldListInsideItsOwnKeySpaceAndReturnLocalPrefixes()
    {
        using var inner = KVPSLoader.CreateIKVPS("memory://");
        await WriteKeys(inner, "other/o9/a.json");

        var wrapped = inner.WrapKeys(new PrefixKeyTransformer("tenant/"));
        await WriteKeys(wrapped, "o1/a.json", "o2/b.json");

        var prefixes = List(wrapped, null, "/").Select(x => x.Prefix);

        prefixes.Should().BeEquivalentTo(new[] { "o1/", "o2/" });
    }

    [TestMethod]
    public async Task ExtendersShouldListThroughTheirParent()
    {
        using var inner = KVPSLoader.CreateIKVPS("memory://");
        await WriteKeys(inner, "o1/a.json", "o2/b.json");

        IKVPS[] extenders = [new KVPSBatchExtender(inner), new KVPSJsonExtender(inner)];
        foreach (var extender in extenders)
        {
            extender.Should().BeAssignableTo<IKVPSPrefixEnumeration>();
            List(extender, null, "/").Select(x => x.Prefix).Should().BeEquivalentTo(new[] { "o1/", "o2/" });
        }
    }

    [TestMethod]
    public void S3ShouldListPrefixesNatively()
    {
        using var kvps = new KVPSButter.S3.Loader().Create("s3://bucket/prefix?username=key&password=secret");

        kvps.Should().BeAssignableTo<IKVPSPrefixEnumeration>();
    }

    [TestMethod]
    [DataRow(0, new int[0])]
    [DataRow(1, new[] { 1 })]
    [DataRow(1000, new[] { 1000 })]
    [DataRow(2500, new[] { 1000, 1000, 500 })]
    public void S3ShouldSplitBatchDeletesIntoRequestsOfAtMost1000Keys(int count, int[] expectedSizes)
    {
        var keys = Enumerable.Range(0, count).Select(x => $"k{x}").ToList();

        var chunks = KVPSButter.S3.KVPS.ChunkForDelete(keys).ToList();

        chunks.Select(x => x.Length).Should().Equal(expectedSizes);
        chunks.SelectMany(x => x).Should().Equal(keys);
    }
}
