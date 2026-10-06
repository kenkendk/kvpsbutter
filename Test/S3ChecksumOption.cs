namespace Test;
using System.Reflection;
using Amazon.S3;
using KVPSButter.S3;

/// <summary>
/// The S3 destination's ChecksumAlgorithm option, which buckets with Object Lock need
/// </summary>
[TestClass]
public class S3ChecksumOption
{
    private const string BaseConnectionString = "s3://bucket/prefix?username=key&password=secret";

    /// <summary>
    /// Every checksum algorithm the AWS SDK defines, so the cases follow the SDK
    /// </summary>
    public static IEnumerable<object[]> SdkAlgorithms
        => typeof(ChecksumAlgorithm)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(x => x.FieldType == typeof(ChecksumAlgorithm))
            .Select(x => new object[] { ((ChecksumAlgorithm)x.GetValue(null)!).Value });

    [TestMethod]
    public void ShouldListTheOption()
    {
        var option = new Loader().SupportedOptions.FirstOrDefault(x => x.Name == "ChecksumAlgorithm");

        option.Should().NotBeNull();
        option!.Optional.Should().BeTrue();
        option.Default.Should().BeNull();
    }

    [TestMethod]
    public void ShouldConnectWithoutTheOption()
    {
        using var kvps = new Loader().Create(BaseConnectionString);

        kvps.Should().BeOfType<KVPS>();
    }

    [TestMethod]
    [DynamicData(nameof(SdkAlgorithms))]
    public void ShouldAcceptEveryAlgorithmTheSdkDefines(string algorithm)
    {
        using var kvps = new Loader().Create($"{BaseConnectionString}&ChecksumAlgorithm={algorithm}");

        kvps.Should().BeOfType<KVPS>();
    }

    [TestMethod]
    [DataRow("sha256")]
    [DataRow("Sha256")]
    [DataRow("crc32c")]
    public void ShouldAcceptAlgorithmsRegardlessOfCase(string algorithm)
    {
        using var kvps = new Loader().Create($"{BaseConnectionString}&ChecksumAlgorithm={algorithm}");

        kvps.Should().BeOfType<KVPS>();
    }

    [TestMethod]
    [DataRow("md5")]
    [DataRow("SHA-256")]
    [DataRow("none")]
    public void ShouldRejectAlgorithmsTheSdkDoesNotDefine(string algorithm)
    {
        var act = () => new Loader().Create($"{BaseConnectionString}&ChecksumAlgorithm={algorithm}");

        act.Should().Throw<InvalidOptionException>()
            .WithMessage($"*{algorithm}*")
            .WithMessage("*SHA256*");
    }
}
