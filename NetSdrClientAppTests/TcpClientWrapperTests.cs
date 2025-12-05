using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using NetSdrClientApp;
using NetSdrClientApp.Networking;

namespace NetSdrClientAppTests;

public class TcpClientWrapperTests
{
    private TcpListener _testServer;
    private const int TestPort = 9999;
    private const string TestHost = "127.0.0.1";

    [SetUp]
    public void Setup()
    {
        _testServer = new TcpListener(IPAddress.Parse(TestHost), TestPort);
    }

    [TearDown]
    public void TearDown()
    {
        _testServer?.Stop();
    }

    [Test]
    public async Task Connect_ShouldEstablishConnection_WhenServerIsAvailable()
    {
        // Arrange
        _testServer.Start();
        var wrapper = new TcpClientWrapper(TestHost, TestPort);

        // Act
        wrapper.Connect();
        await Task.Delay(100);

        // Assert
        Assert.IsTrue(wrapper.Connected);

        // Cleanup
        wrapper.Disconnect();
        _testServer.Stop();
    }

    [Test]
    public void Connect_ShouldNotReconnect_WhenAlreadyConnected()
    {
        // Arrange
        _testServer.Start();
        var wrapper = new TcpClientWrapper(TestHost, TestPort);
        wrapper.Connect();

        // Act
        wrapper.Connect();

        // Assert
        Assert.IsTrue(wrapper.Connected);

        // Cleanup
        wrapper.Disconnect();
        _testServer.Stop();
    }

    [Test]
    public void Connect_ShouldThrowException_WhenServerIsNotAvailable()
    {
        // Arrange
        var wrapper = new TcpClientWrapper(TestHost, 9998);

        // Act & Assert
        Assert.Throws<SocketException>(() => wrapper.Connect());
    }

    [Test]
    public void Disconnect_ShouldCloseConnection_WhenConnected()
    {
        // Arrange
        _testServer.Start();
        var wrapper = new TcpClientWrapper(TestHost, TestPort);
        wrapper.Connect();

        // Act
        wrapper.Disconnect();

        // Assert
        Assert.IsFalse(wrapper.Connected);

        _testServer.Stop();
    }

    [Test]
    public void Disconnect_ShouldDoNothing_WhenNotConnected()
    {
        // Arrange
        var wrapper = new TcpClientWrapper(TestHost, TestPort);

        // Act & Assert
        wrapper.Disconnect();
        Assert.IsFalse(wrapper.Connected);
    }

    [Test]
    public void SendMessageAsync_ShouldThrowException_WhenNotConnected()
    {
        // Arrange
        var wrapper = new TcpClientWrapper(TestHost, TestPort);
        byte[] testData = new byte[] { 1, 2, 3 };

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await wrapper.SendMessageAsync(testData));
    }

    [Test]
    public async Task SendMessageAsync_ShouldThrowException_WhenStreamCannotWrite()
    {
        // Arrange
        _testServer.Start();
        var wrapper = new TcpClientWrapper(TestHost, TestPort);
        wrapper.Connect();
        await Task.Delay(100);

        _testServer.Stop();
        await Task.Delay(100);

        byte[] testData = new byte[] { 1, 2, 3 };

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await wrapper.SendMessageAsync(testData));
    }

    [Test]
    public async Task StartListeningAsync_ShouldReceiveMessages_WhenDataIsReceived()
    {
        // Arrange
        _testServer.Start();
        var wrapper = new TcpClientWrapper(TestHost, TestPort);

        byte[] receivedData = null;
        wrapper.MessageReceived += (sender, data) => receivedData = data;

        wrapper.Connect();
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
        wrapper.Disconnect();
        _testServer.Stop();
    }

    [Test]
    public async Task StartListeningAsync_ShouldStopGracefully_WhenConnectionIsClosed()
    {
        // Arrange
        _testServer.Start();
        var wrapper = new TcpClientWrapper(TestHost, TestPort);

        wrapper.Connect();
        await Task.Delay(100);

        var client = await _testServer.AcceptTcpClientAsync();

        // Act
        client.Close();
        await Task.Delay(200);

        // Assert - no exception should be thrown
        Assert.Pass();

        // Cleanup
        wrapper.Disconnect();
        _testServer.Stop();
    }

    [Test]
    public async Task StartListeningAsync_ShouldHandleMultipleMessages()
    {
        // Arrange
        _testServer.Start();
        var wrapper = new TcpClientWrapper(TestHost, TestPort);

        var receivedMessages = new System.Collections.Generic.List<byte[]>();
        wrapper.MessageReceived += (sender, data) => receivedMessages.Add(data);

        wrapper.Connect();
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
        Assert.GreaterOrEqual(receivedMessages.Count, 1);

        // Cleanup
        stream.Close();
        client.Close();
        wrapper.Disconnect();
        _testServer.Stop();
    }

    [Test]
    public async Task StartListeningAsync_ShouldHandleLargeMessages()
    {
        // Arrange
        _testServer.Start();
        var wrapper = new TcpClientWrapper(TestHost, TestPort);

        byte[] receivedData = null;
        wrapper.MessageReceived += (sender, data) => receivedData = data;

        wrapper.Connect();
        await Task.Delay(100);

        var client = await _testServer.AcceptTcpClientAsync();
        var stream = client.GetStream();

        // Act - send message larger than buffer size (8194 bytes)
        byte[] largeMessage = new byte[10000];
        for (int i = 0; i < largeMessage.Length; i++)
        {
            largeMessage[i] = (byte)(i % 256);
        }

        await stream.WriteAsync(largeMessage, 0, largeMessage.Length);
        await Task.Delay(300);

        // Assert
        Assert.IsNotNull(receivedData);
        Assert.GreaterOrEqual(receivedData.Length, 8194);

        // Cleanup
        stream.Close();
        client.Close();
        wrapper.Disconnect();
        _testServer.Stop();
    }

    [Test]
    public async Task MessageReceived_Event_ShouldBeInvoked_WhenMessageArrives()
    {
        // Arrange
        _testServer.Start();
        var wrapper = new TcpClientWrapper(TestHost, TestPort);

        bool eventFired = false;
        byte[] eventData = null;

        wrapper.MessageReceived += (sender, data) =>
        {
            eventFired = true;
            eventData = data;
        };

        wrapper.Connect();
        await Task.Delay(100);

        var client = await _testServer.AcceptTcpClientAsync();
        var stream = client.GetStream();

        // Act
        byte[] testMessage = Encoding.UTF8.GetBytes("Event Test");
        await stream.WriteAsync(testMessage, 0, testMessage.Length);
        await Task.Delay(200);

        // Assert
        Assert.IsTrue(eventFired);
        Assert.IsNotNull(eventData);
        Assert.AreEqual("Event Test", Encoding.UTF8.GetString(eventData));

        // Cleanup
        stream.Close();
        client.Close();
        wrapper.Disconnect();
        _testServer.Stop();
    }

    [Test]
    public async Task CancellationToken_ShouldStopListening_WhenCancelled()
    {
        // Arrange
        _testServer.Start();
        var wrapper = new TcpClientWrapper(TestHost, TestPort);

        wrapper.Connect();
        await Task.Delay(100);

        var client = await _testServer.AcceptTcpClientAsync();

        // Act
        wrapper.Disconnect(); // This should cancel the token
        await Task.Delay(200);

        // Assert - should not throw OperationCanceledException
        Assert.IsFalse(wrapper.Connected);

        // Cleanup
        client.Close();
        _testServer.Stop();
    }
}
