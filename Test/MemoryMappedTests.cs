namespace Test;

[TestClass]
public class MemoryMappedTests
{
    private byte[] data1 = new byte[0];
    private byte[] data2 = new byte[0];
    private string folder1 = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        var rnd = new Random();
        var data1 = new byte[8 * 1024];
        var data2 = new byte[4 * 1024];

        rnd.NextBytes(data1);
        rnd.NextBytes(data2);

        folder1 = Path.GetFullPath("./test1");
        if (Directory.Exists(folder1))
        {
            Directory.Delete(folder1, true);

            // Windows has a quirk here
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                Thread.Sleep(500);
        }

        Directory.CreateDirectory(folder1);
    }

    [TestMethod]
    [DataRow("memory://")]
    [DataRow("file://test1?pathmapped=false")]
    [DataRow("file://test1?pathmapped=true")]
    public async Task WriteAndReadShouldReturnSameContent(string connectionString)
    {
        var kvps = KVPSLoader.CreateIKVPS(connectionString);
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
    [DataRow("memory://")]
    [DataRow("file://test1?pathmapped=false")]
    [DataRow("file://test1?pathmapped=true")]
    public async Task ReadingNonExistingShouldReturnNull(string connectionString)
    {
        var kvps = KVPSLoader.CreateIKVPS(connectionString);
        var info = await kvps.GetInfoAsync("x1");
        var stream = await kvps.ReadAsync("x1");

        info.Should().BeNull();
        stream.Should().BeNull();
    }

    [TestMethod]
    [DataRow("memory://")]
    [DataRow("file://test1?pathmapped=false")]
    [DataRow("file://test1?pathmapped=true")]
    public async Task EnumerateShouldReturnFilteredItems(string connectionString)
    {
        var kvps = KVPSLoader.CreateIKVPS(connectionString);

        await kvps.WriteAsync("k1", new MemoryStream(data1));
        await kvps.WriteAsync("k2", new MemoryStream(data1));
        await kvps.WriteAsync("a3", new MemoryStream(data2));

        var prefixk = kvps.EnumerateAsync(KVPSQuery.Empty.WithPrefix("k")).ToBlockingEnumerable().ToList();
        var prefixa = kvps.EnumerateAsync(KVPSQuery.Empty.WithPrefix("a")).ToBlockingEnumerable().ToList();

        prefixk.Should().HaveCount(2);
        prefixa.Should().HaveCount(1);
    }

    public static byte[]? ReadAllBytes(Stream? instream)
    {
        if (instream == null)
            return null;

        if (instream is MemoryStream ms)
            return ms.ToArray();

        var memoryStream = new MemoryStream();
        instream!.CopyTo(memoryStream);

        return memoryStream.ToArray();
    }
}
