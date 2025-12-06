using Moq;
using NetSdrClientApp;
using NetSdrClientApp.Networking;

namespace NetSdrClientAppTests;

public class NetSdrClientTests
{
    NetSdrClient _client;
    Mock<ITcpClient> _tcpMock;
    Mock<IUdpClient> _updMock;

    public NetSdrClientTests() { }

    [SetUp]
    public void Setup()
    {
        _tcpMock = new Mock<ITcpClient>();
        _tcpMock.Setup(tcp => tcp.Connect()).Callback(() =>
        {
            _tcpMock.Setup(tcp => tcp.Connected).Returns(true);
        });

        _tcpMock.Setup(tcp => tcp.Disconnect()).Callback(() =>
        {
            _tcpMock.Setup(tcp => tcp.Connected).Returns(false);
        });

        _tcpMock.Setup(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>())).Callback<byte[]>((bytes) =>
        {
            _tcpMock.Raise(tcp => tcp.MessageReceived += null, _tcpMock.Object, bytes);
        });

        _updMock = new Mock<IUdpClient>();

        _client = new NetSdrClient(_tcpMock.Object, _updMock.Object);
    }

    [Test]
    public async Task ConnectAsyncTest()
    {
        //act
        await _client.ConnectAsync();

        //assert
        _tcpMock.Verify(tcp => tcp.Connect(), Times.Once);
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(3));
    }

    [Test]
    public async Task DisconnectWithNoConnectionTest()
    {
        //act
        _client.Disconect();

        //assert
        //No exception thrown
        _tcpMock.Verify(tcp => tcp.Disconnect(), Times.Once);
    }

    [Test]
    public async Task DisconnectTest()
    {
        //Arrange 
        await ConnectAsyncTest();

        //act
        _client.Disconect();

        //assert
        //No exception thrown
        _tcpMock.Verify(tcp => tcp.Disconnect(), Times.Once);
    }

    [Test]
    public async Task StartIQNoConnectionTest()
    {

        //act
        await _client.StartIQAsync();

        //assert
        //No exception thrown
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
        _tcpMock.VerifyGet(tcp => tcp.Connected, Times.AtLeastOnce);
    }

    [Test]
    public async Task StartIQTest()
    {
        //Arrange 
        await ConnectAsyncTest();

        //act
        await _client.StartIQAsync();

        //assert
        //No exception thrown
        _updMock.Verify(udp => udp.StartListeningAsync(), Times.Once);
        Assert.That(_client.IQStarted, Is.True);
    }

    [Test]
    public async Task StopIQTest()
    {
        //Arrange 
        await ConnectAsyncTest();

        //act
        await _client.StopIQAsync();

        //assert
        //No exception thrown
        _updMock.Verify(tcp => tcp.StopListening(), Times.Once);
        Assert.That(_client.IQStarted, Is.False);
    }

    //TODO: cover the rest of the NetSdrClient code here

    [Test]
    public async Task StopIQNoConnectionTest()
    {
        //act
        await _client.StopIQAsync();

        //assert
        //No exception thrown
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
        _tcpMock.VerifyGet(tcp => tcp.Connected, Times.AtLeastOnce);
        Assert.That(_client.IQStarted, Is.False);
    }

    [Test]
    public async Task ChangeFrequencyAsyncTest()
    {
        //Arrange 
        await ConnectAsyncTest();
        long frequency = 14000000; // 14 MHz
        int channel = 0;

        //act
        await _client.ChangeFrequencyAsync(frequency, channel);

        //assert
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(4)); // 3 from Connect + 1 from ChangeFrequency
    }

    [Test]
    public async Task ChangeFrequencyNoConnectionTest()
    {
        //Arrange
        long frequency = 14000000;
        int channel = 0;

        //act
        await _client.ChangeFrequencyAsync(frequency, channel);

        //assert
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
        _tcpMock.VerifyGet(tcp => tcp.Connected, Times.AtLeastOnce);
    }

    [Test]
    public async Task ConnectAsyncWhenAlreadyConnectedTest()
    {
        //Arrange
        _tcpMock.Setup(tcp => tcp.Connected).Returns(true);

        //act
        await _client.ConnectAsync();

        //assert
        _tcpMock.Verify(tcp => tcp.Connect(), Times.Never);
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
    }

    [Test]
    public async Task StartIQWhenAlreadyStartedTest()
    {
        //Arrange 
        await ConnectAsyncTest();
        await _client.StartIQAsync();

        //act
        await _client.StartIQAsync();

        //assert
        _updMock.Verify(udp => udp.StartListeningAsync(), Times.Once); // Should only start once
        Assert.That(_client.IQStarted, Is.True);
    }

    [Test]
    public async Task StopIQWhenNotStartedTest()
    {
        //Arrange 
        await ConnectAsyncTest();

        //act
        await _client.StopIQAsync();

        //assert
        _updMock.Verify(udp => udp.StopListening(), Times.Once);
        Assert.That(_client.IQStarted, Is.False);
    }

    [Test]
    public async Task ChangeFrequencyWithDifferentChannelTest()
    {
        //Arrange 
        await ConnectAsyncTest();
        long frequency = 7000000; // 7 MHz
        int channel = 1;

        //act
        await _client.ChangeFrequencyAsync(frequency, channel);

        //assert
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(4));
    }

    [Test]
    public async Task ChangeFrequencyMultipleTimesTest()
    {
        //Arrange 
        await ConnectAsyncTest();

        //act
        await _client.ChangeFrequencyAsync(14000000, 0);
        await _client.ChangeFrequencyAsync(7000000, 0);
        await _client.ChangeFrequencyAsync(21000000, 0);

        //assert
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(6)); // 3 from Connect + 3 frequency changes
    }

    [Test]
    public async Task ChangeFrequencyWithZeroFrequencyTest()
    {
        //Arrange 
        await ConnectAsyncTest();
        long frequency = 0;
        int channel = 0;

        //act
        await _client.ChangeFrequencyAsync(frequency, channel);

        //assert
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(4));
    }

    [Test]
    public async Task ChangeFrequencyWithMaxFrequencyTest()
    {
        //Arrange 
        await ConnectAsyncTest();
        long frequency = long.MaxValue;
        int channel = 0;

        //act
        await _client.ChangeFrequencyAsync(frequency, channel);

        //assert
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(4));
    }

    [Test]
    public async Task DisconnectClearsConnectionStateTest()
    {
        //Arrange 
        await ConnectAsyncTest();

        //act
        _client.Disconect();

        //assert
        _tcpMock.Verify(tcp => tcp.Disconnect(), Times.Once);
        _tcpMock.VerifyGet(tcp => tcp.Connected, Times.AtLeastOnce);
    }

    [Test]
    public async Task MultipleConnectDisconnectCyclesTest()
    {
        //act
        await _client.ConnectAsync();
        _client.Disconect();
        await _client.ConnectAsync();
        _client.Disconect();

        //assert
        _tcpMock.Verify(tcp => tcp.Connect(), Times.Exactly(2));
        _tcpMock.Verify(tcp => tcp.Disconnect(), Times.Exactly(2));
    }

    [Test]
    public async Task StartStopIQMultipleCyclesTest()
    {
        //Arrange 
        await ConnectAsyncTest();

        //act
        await _client.StartIQAsync();
        await _client.StopIQAsync();
        await _client.StartIQAsync();
        await _client.StopIQAsync();

        //assert
        _updMock.Verify(udp => udp.StartListeningAsync(), Times.Exactly(2));
        _updMock.Verify(udp => udp.StopListening(), Times.Exactly(2));
        Assert.That(_client.IQStarted, Is.False);
    }

    [Test]
    public void DisconnectMultipleTimesTest()
    {
        //act
        _client.Disconect();
        _client.Disconect();
        _client.Disconect();

        //assert
        _tcpMock.Verify(tcp => tcp.Disconnect(), Times.Exactly(3));
    }

    [Test]
    public async Task IQStartedPropertyInitialStateTest()
    {
        //assert
        Assert.That(_client.IQStarted, Is.False);
    }

    [Test]
    public async Task MessageReceivedEventTriggeredTest()
    {
        //Arrange
        bool eventTriggered = false;
        byte[]? receivedData = null;

        _tcpMock.Setup(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()))
            .Callback<byte[]>((bytes) =>
            {
                eventTriggered = true;
                receivedData = bytes;
                _tcpMock.Raise(tcp => tcp.MessageReceived += null, _tcpMock.Object, bytes);
            });

        await _client.ConnectAsync();

        //assert
        Assert.That(eventTriggered, Is.True);
        Assert.That(receivedData, Is.Not.Null);
    }

    [Test]
    public async Task OperationsWorkInCorrectSequenceTest()
    {
        //act - Full workflow
        await _client.ConnectAsync();
        await _client.ChangeFrequencyAsync(14000000, 0);
        await _client.StartIQAsync();
        await _client.StopIQAsync();
        _client.Disconect();

        //assert
        _tcpMock.Verify(tcp => tcp.Connect(), Times.Once);
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(4));
        _updMock.Verify(udp => udp.StartListeningAsync(), Times.Once);
        _updMock.Verify(udp => udp.StopListening(), Times.Once);
        _tcpMock.Verify(tcp => tcp.Disconnect(), Times.Once);
    }
}
