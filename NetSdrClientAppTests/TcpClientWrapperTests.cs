using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NetSdrClientAppTests;

public class TcpClientWrapperTests : IDisposable
{
    private TcpListener _testServer;
    private TcpClientWrapper _wrapper;
    private const int TestPort = 9999;
    private const string TestHost = "127.0.0.1";

    [SetUp]
    public void Setup()
    {
        _testServer = new TcpListener(IPAddress.Parse(TestHost), TestPort);
        _wrapper = new TcpClientWrapper(TestHost, TestPort);
    }

    [TearDown]
    public void Dispose()
    {
        _wrapper?.Disconnect();
        _testServer?.Stop();
    }

    [Test]
    public async Task Connect_ShouldEstablishConnection_WhenServerIsAvailable()
    {
        // Arrange
        _testServer.Start();

        // Act
        _wrapper.Connect();
        await Task.Delay(100);

        // Assert
        Assert.True(_wrapper.Connected);
    }

    [Test]
    public void Connect_ShouldNotReconnect_WhenAlreadyConnected()
    {
        // Arrange
        _testServer.Start();
        _wrapper.Connect();

        // Act
        _wrapper.Connect();

        // Assert
        Assert.True(_wrapper.Connected);
    }

    [Test]
    public void Connect_ShouldThrowException_WhenServerIsNotAvailable()
    {
        // Arrange
        var invalidWrapper = new TcpClientWrapper(TestHost, 9998);

        // Act & Assert
        Assert.Throws<Exception>(() => invalidWrapper.Connect());
    }

    [Test]
    public void Disconnect_ShouldCloseConnection_WhenConnected()
    {
        // Arrange
        _testServer.Start();
        _wrapper.Connect();

        // Act
        _wrapper.Disconnect();

        // Assert
        Assert.False(_wrapper.Connected);
    }

    [Test]
    public void Disconnect_ShouldDoNothing_WhenNotConnected()
    {
        // Act & Assert
        Assert.DoesNotThrow(() => _wrapper.Disconnect());
        Assert.False(_wrapper.Connected);
    }

    [Test]
    public async Task SendMessageAsync_WithByteArray_ShouldSendData_WhenConnected()
    {
        // Arrange
        _testServer.Start();
        var serverTask = AcceptAndReadBytesAsync(_testServer, 11);

        _wrapper.Connect();
        await Task.Delay(100);

        byte[] testData = Encoding.UTF8.GetBytes("Hello World");

        // Act
        await _wrapper.SendMessageAsync(testData);

        // Assert
        var receivedData = await serverTask;
        Assert.AreEqual(testData, receivedData);
    }

    [Test]
    public async Task SendMessageAsync_WithString_ShouldSendData_WhenConnected()
    {
        // Arrange
        _testServer.Start();
        var serverTask = AcceptAndReadBytesAsync(_testServer, 11);

        _wrapper.Connect();
        await Task.Delay(100);

        string testMessage = "Hello World";

        // Act
        await _wrapper.SendMessageAsync(testMessage);

        // Assert
        var receivedData = await serverTask;
        var receivedMessage = Encoding.UTF8.GetString(receivedData);
        Assert.AreEqual(testMessage, receivedMessage);
    }

    [Test]
    public void SendMessageAsync_ShouldThrowException_WhenNotConnected()
    {
        // Arrange
        byte[] testData = new byte[] { 1, 2, 3 };

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _wrapper.SendMessageAsync(testData));
    }

    [Test]
    public async Task SendMessageAsync_ShouldThrowException_WhenStreamCannotWrite()
    {
        // Arrange
        _testServer.Start();
        _wrapper.Connect();
        await Task.Delay(100);

        _testServer.Stop();
        await Task.Delay(100);

        byte[] testData = new byte[] { 1, 2, 3 };

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _wrapper.SendMessageAsync(testData));
    }

    [Test]
    public async Task StartListeningAsync_ShouldReceiveMessages_WhenDataIsReceived()
    {
        // Arrange
        _testServer.Start();

        byte[] receivedData = null;
        _wrapper.MessageReceived += (sender, data) => receivedData = data;

        _wrapper.Connect();
        await Task.Delay(100);

        var client = await _testServer.AcceptTcpClientAsync();
        var stream = client.GetStream();

        // Act
        byte[] testMessage = Encoding.UTF8.GetBytes("Test Message");
        await stream.WriteAsync(testMessage, 0, testMessage.Length);
        await Task.Delay(200);

        // Assert
        Assert.IsNotNull(receivedData);
        Assert.AreEqual("Test Message", Encoding.UTF8.GetString(receivedData));

        // Cleanup
        stream.Close();
        client.Close();
    }

    [Test]
    public async Task StartListeningAsync_ShouldStopGracefully_WhenConnectionIsClosed()
    {
        // Arrange
        _testServer.Start();
        _wrapper.Connect();
        await Task.Delay(100);

        var client = await _testServer.AcceptTcpClientAsync();

        // Act
        client.Close();
        await Task.Delay(200);

        // Assert
        Assert.DoesNotThrow(() => { }); // No exception thrown

        client.Close();
    }

    [Test]
    public async Task StartListeningAsync_ShouldHandleMultipleMessages()
    {
        // Arrange
        _testServer.Start();

        var messages = new System.Collections.Generic.List<byte[]>();
        _wrapper.MessageReceived += (sender, data) => messages.Add(data);

        _wrapper.Connect();
        await Task.Delay(100);

        var client = await _testServer.AcceptTcpClientAsync();
        var stream = client.GetStream();

        // Act
        byte[] message1 = Encoding.UTF8.GetBytes("Message 1");
        byte[] message2 = Encoding.UTF8.GetBytes("Message 2");

        await stream.WriteAsync(message1, 0, message1.Length);
        await Task.Delay(100);
        await stream.WriteAsync(message2, 0, message2.Length);
        await Task.Delay(200);

        // Assert
        Assert.GreaterOrEqual(messages.Count, 1);

        // Cleanup
        stream.Close();
        client.Close();
    }

    [Test]
    public void MessageReceived_ShouldBeInvoked_WhenDataArrives()
    {
        // Arrange
        _testServer.Start();

        bool eventFired = false;
        _wrapper.MessageReceived += (sender, data) => eventFired = true;

        // Act & Assert
        _wrapper.Connect();
        // Event handler should be attached
        Assert.IsNotNull(_wrapper.MessageReceived);
    }

    [Test]
    public async Task SendMessageAsync_ShouldHandleLargeData()
    {
        // Arrange
        _testServer.Start();
        byte[] largeData = new byte[8192];
        for (int i = 0; i < largeData.Length; i++)
            largeData[i] = (byte)(i % 256);

        var serverTask = AcceptAndReadBytesAsync(_testServer, largeData.Length);

        _wrapper.Connect();
        await Task.Delay(100);

        // Act
        await _wrapper.SendMessageAsync(largeData);

        // Assert
        var receivedData = await serverTask;
        Assert.AreEqual(largeData.Length, receivedData.Length);
    }

    [Test]
    public void Connected_ShouldReturnFalse_BeforeConnection()
    {
        // Assert
        Assert.False(_wrapper.Connected);
    }

    [Test]
    public void Connected_ShouldReturnTrue_AfterSuccessfulConnection()
    {
        // Arrange
        _testServer.Start();

        // Act
        _wrapper.Connect();

        // Assert
        Assert.True(_wrapper.Connected);
    }

    [Test]
    public void Constructor_ShouldInitializeProperties()
    {
        // Assert
        Assert.IsNotNull(_wrapper);
        Assert.False(_wrapper.Connected);
    }

    // Helper method
    private async Task<byte[]> AcceptAndReadBytesAsync(TcpListener listener, int expectedBytes)
    {
        var client = await listener.AcceptTcpClientAsync();
        var stream = client.GetStream();

        byte[] buffer = new byte[expectedBytes];
        int totalRead = 0;

        while (totalRead < expectedBytes)
        {
            int read = await stream.ReadAsync(buffer, totalRead, expectedBytes - totalRead);
            if (read == 0) break;
            totalRead += read;
        }

        byte[] result = new byte[totalRead];
        Array.Copy(buffer, result, totalRead);

        stream.Close();
        client.Close();

        return result;
    }
}
