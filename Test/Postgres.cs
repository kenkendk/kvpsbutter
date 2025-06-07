namespace Test;

/// <summary>
/// This tests will require an enviroment variable named "KVPS_TEST_POSTGRES" to be set with the connection string,
/// and assumes the database and table are already created.
/// </summary>
[TestClass]
public class PostgresTests
{
    private byte[] data1 = [];
    private byte[] data2 = [];
    private byte[] data3 = [];

    private static string GetConnectionString()
    {

        // Example: postgres://?username=testuser&password=testpassword&host=localhost&port=5432&database=testdb&tablename=kvps_test

        var connectionString = Environment.GetEnvironmentVariable("KVPS_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Fail("Environment variable 'KVPS_TEST_POSTGRES' is not set. Please set it with a valid PostgreSQL connection string.");
        }
        return connectionString;
    }

    [TestInitialize]
    public void Setup()
    {
        var rnd = new Random();
        data1 = new byte[8 * 1024];
        data2 = new byte[4 * 1024];
        data3 = new byte[256]; // Small buffer for large enumeration tests

        rnd.NextBytes(data1);
        rnd.NextBytes(data2);
        rnd.NextBytes(data3);

        //CleanUp().GetAwaiter().GetResult();
    }
    public async Task CleanUp()
    {
        var kvps = KVPSLoader.CreateIKVPS(GetConnectionString());

        foreach (var entry in kvps.EnumerateAsync().ToBlockingEnumerable())
            await kvps.DeleteAsync(entry.Key).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task WriteAndReadShouldReturnSameContent()
    {
        var kvps = KVPSLoader.CreateIKVPS(GetConnectionString());
        await kvps.WriteAsync("k1", new MemoryStream(data1));
        await kvps.WriteAsync("k2", new MemoryStream(data1));
        await kvps.WriteAsync("k3", new MemoryStream(data2));

        var k1 = ReadAllBytes(await kvps.ReadAsync("k1"));
        var k2 = ReadAllBytes(await kvps.ReadAsync("k2"));
        var k3 = ReadAllBytes(await kvps.ReadAsync("k3"));

        k1.Should().NotBeNull();
        k2.Should().NotBeNull();
        k3.Should().NotBeNull();

        k1.Should().BeEquivalentTo(data1);
        k2.Should().BeEquivalentTo(data1);
        k3.Should().BeEquivalentTo(data2);
    }

    [TestMethod]
    public async Task WriteDeleteAndRead()
    {
        var kvps = KVPSLoader.CreateIKVPS(GetConnectionString());

        await kvps.WriteAsync("k1", new MemoryStream(data1));
        await kvps.WriteAsync("k2", new MemoryStream(data1));
        await kvps.WriteAsync("k3", new MemoryStream(data2));

        var k1 = ReadAllBytes(await kvps.ReadAsync("k1"));
        var k2 = ReadAllBytes(await kvps.ReadAsync("k2"));
        var k3 = ReadAllBytes(await kvps.ReadAsync("k3"));

        k1.Should().NotBeNull();
        k2.Should().NotBeNull();
        k3.Should().NotBeNull();

        k1.Should().BeEquivalentTo(data1);
        k2.Should().BeEquivalentTo(data1);
        k3.Should().BeEquivalentTo(data2);

        await kvps.DeleteAsync("k1");
        k1 = ReadAllBytes(await kvps.ReadAsync("k1"));

        k1.Should().BeNull();

        KVPSBatchExtender batchExtender = new KVPSBatchExtender(kvps);

        await batchExtender.DeleteAsync(["k2", "k3"], CancellationToken.None);

        k2 = ReadAllBytes(await kvps.ReadAsync("k2"));
        k3 = ReadAllBytes(await kvps.ReadAsync("k3"));

        k2.Should().BeNull();
        k3.Should().BeNull();
    }

    [TestMethod]
    public async Task WriteAndList()
    {
        var kvps = KVPSLoader.CreateIKVPS(GetConnectionString());

        await kvps.WriteAsync("k1", new MemoryStream(data1));
        await kvps.WriteAsync("k2", new MemoryStream(data1));
        await kvps.WriteAsync("k3", new MemoryStream(data2));

        var list = kvps.EnumerateAsync();
        var items = list.ToBlockingEnumerable().ToList();

        items.Should().Contain(x => x.Key == "k1");
        items.Should().Contain(x => x.Key == "k2");
        items.Should().Contain(x => x.Key == "k3");
    }

    [TestMethod]
    public async Task WriteAndListHugeList()
    {
        var targetSize = 2500; // 1000 is the page size, so just 2.5 pages

        var kvps = KVPSLoader.CreateIKVPS(GetConnectionString());

        using var sample = new MemoryStream(data3);
        for (int i = 0; i < targetSize; i++)
        {
            await kvps.WriteAsync($"k{i}", sample);
        }

        var list = kvps.EnumerateAsync();
        var items = list.ToBlockingEnumerable().ToList();

        items.Should().HaveCountGreaterThanOrEqualTo(targetSize);
    }

    private static byte[]? ReadAllBytes(Stream? instream)
    {
        if (instream == null)
            return null;

        if (instream is MemoryStream ms)
            return ms.ToArray();

        using var memoryStream = new MemoryStream();
        instream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }
}


