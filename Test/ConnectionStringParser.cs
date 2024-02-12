namespace Test;
using System.ComponentModel;

[TestClass]
public class ConnectionStringParser
{
    [TestMethod]
    [DataRow("ftp://example.com?port=1234&username=user1&pass=pass2")]
    [DataRow("ftp://example.com/home/xyz?port=1234&username=user1&pass=pass2")]
    [DataRow("file://c:\\test?smb=false")]
    [DataRow("file://c:\\test")]
    [DataRow("file:///unixstyle")]
    [DataRow("file://unixstyle")]
    [DataRow("mem://")]
    public void ShouldParseValid(string connectionString)
    {
        var parsed = KVPSLoader.ParseConnectionString(connectionString);
        connectionString.Should().StartWith(parsed.Scheme);
    }

    public record BasicOptions(
        [Description("The username")]
        string Username,
        string Password,
        bool Option = true,
        int Port = 3333);

    [TestMethod]
    public void ShouldReturnAllBasicOptions()
    {
        var opts = KVPSLoader.GetSupportedOptions<BasicOptions>().ToList();
        opts.Count.Should().Be(4);
        var usernameOption = opts.FirstOrDefault(x => x.Name == "Username")!;
        var passwordOption = opts.FirstOrDefault(x => x.Name == "Password")!;
        var optionOption = opts.FirstOrDefault(x => x.Name == "Option")!;
        var portOption = opts.FirstOrDefault(x => x.Name == "Port")!;

        usernameOption.Should().NotBeNull();
        passwordOption.Should().NotBeNull();
        optionOption.Should().NotBeNull();
        portOption.Should().NotBeNull();

        usernameOption.Optional.Should().BeFalse();
        usernameOption.Description.Should().Be("The username");
        usernameOption.Default.Should().BeNull();

        passwordOption.Optional.Should().BeFalse();
        passwordOption.Description.Should().BeNullOrEmpty();
        passwordOption.Default.Should().BeNull();

        optionOption.Optional.Should().BeTrue();
        optionOption.Default.Should().Be(true);

        portOption.Optional.Should().BeTrue();
        portOption.Default.Should().Be(3333);
    }

    [TestMethod]
    [DataRow("ftp://example.com?username=user1&password=pass2", true, 3333)]
    [DataRow("ftp://example.com?username=user1&password=pass2&xyz=abc", true, 3333)]
    [DataRow("ftp://example.com?username=user1&password=pass2&option=no", false, 3333)]
    [DataRow("ftp://example.com?username=user1&password=pass2&port=1234", true, 1234)]
    [DataRow("ftp://example.com?username=user1&password=pass2&port=1234&port=3456", true, 3456)]
    public void ShouldParseValidBasicConfig(string connectionString, bool optionResult, int portResult)
    {
        var (p, config) = KVPSLoader.ParseConnectionString<BasicOptions>(connectionString);
        var c = p.RequirePath().GetRequiredCredentials();
        c.Username.Should().Be("user1");
        c.Password.Should().Be("pass2");

        config.Username.Should().Be(c.Username);
        config.Password.Should().Be(c.Password);
        config.Option.Should().Be(optionResult);
        config.Port.Should().Be(portResult);
    }

    [TestMethod]
    [DataRow("ftp://example.com?port=1234&username=user1&pass_word=pass2")]
    [DataRow("ftp://example.com?port=1234&usern?ame=user1&password=pass2")]
    [DataRow("ftp://example.com/path?port=abc&username=user1&password=pass2")]
    [DataRow("ftp://example.com?username=user1&password=pass2&option=")]
    [DataRow("ftp://example.com?username=user1&password=pass2&port=")]
    [DataRow("ftp://example.com?username=user1&password=pass2&option=f")]
    public void ShouldNotParseInvalidBasicConfig(string connectionString)
    {
        Action act = () => KVPSLoader.ParseConnectionString<BasicOptions>(connectionString);
        act.Should().Throw<InvalidOptionException>();
    }

    public record SimpleWithDefaultOptions(bool Option = false);

    [TestMethod]
    [DataRow("ftp://example.com", false)]
    [DataRow("ftp://example.com?optioN=yes", true)]
    [DataRow("ftp://example.com?optioN=true", true)]
    [DataRow("ftp://example.com?optioN=1", true)]
    [DataRow("ftp://example.com?optioN=ON", true)]
    [DataRow("ftp://example.com?optioN=no", false)]
    [DataRow("ftp://example.com?optioN=False", false)]
    [DataRow("ftp://example.com?optioN=0", false)]
    [DataRow("ftp://example.com?optioN=OfF", false)]
    public void ShouldParseValidSimpleConfig(string connectionString, bool optionResult)
    {
        var (_, config) = KVPSLoader.ParseConnectionString<SimpleWithDefaultOptions>(connectionString);
        config.Option.Should().Be(optionResult);
    }

    [TestMethod]
    [DataRow("ftp://?opt=1")]
    [DataRow("ftp://")]
    [DataRow("ftp://   \n")]
    public void ShouldThrowOnMissingPath(string connectionString)
    {
        var cfg = KVPSLoader.ParseConnectionString(connectionString);
        Action act = () => cfg.RequirePath();
        act.Should().Throw<InvalidOptionException>();
    }
}