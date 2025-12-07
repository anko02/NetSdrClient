using NetSdrClientApp.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NetSdrClientAppTests
{
    public class TcpClientWrapperTests
    {
        private TcpClientWrapper? _wrapper;
        private TcpListener? _testServer;
        private int _testPort;
        private CancellationTokenSource? _serverCts;

        [SetUp]
        public void SetUp()
        {
            _testPort = GetAvailablePort();
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                _wrapper?.Disconnect();
            }
            catch
            {
                // Ignore disposal errors in teardown
            }

            try
            {
                _serverCts?.Cancel();
            }
            catch
            {
                // Ignore cancellation errors
            }

            try
            {
                _serverCts?.Dispose();
            }
            catch
            {
                // Ignore disposal errors
            }

            try
            {
                _testServer?.Stop();
            }
            catch
            {
                // Ignore stop errors
            }
        }

        private static int GetAvailablePort()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private async Task<TcpClient> StartTestServerAsync()
        {
            _testServer = new TcpListener(IPAddress.Loopback, _testPort);
            _testServer.Start();
            _serverCts = new CancellationTokenSource();

            var acceptTask = _testServer.AcceptTcpClientAsync();
            return await acceptTask;
        }

        private static void SafeCloseClient(TcpClient? client)
        {
            try
            {
                client?.Close();
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        [Test]
        public void Constructor_SetsHostAndPort()
        {
            // Arrange & Act
            _wrapper = new TcpClientWrapper("localhost", _testPort);

            // Assert
            Assert.That(_wrapper, Is.Not.Null);
            Assert.That(_wrapper.Connected, Is.False);
        }

        [Test]
        public void Connected_WhenNotConnected_ReturnsFalse()
        {
            // Arrange
            _wrapper = new TcpClientWrapper("localhost", _testPort);

            // Act & Assert
            Assert.That(_wrapper.Connected, Is.False);
        }

        [Test]
        public async Task Connect_SuccessfulConnection_SetsConnectedTrue()
        {
            // Arrange
            var serverTask = StartTestServerAsync();
            _wrapper = new TcpClientWrapper("localhost", _testPort);

            // Act
            _wrapper.Connect();
            await Task.Delay(200);

            // Assert
            Assert.That(_wrapper.Connected, Is.True);

            // Cleanup server client
            SafeCloseClient(await serverTask);
        }

        [Test]
        public async Task Connect_WhenAlreadyConnected_DoesNotReconnect()
        {
            // Arrange
            var serverTask = StartTestServerAsync();
            _wrapper = new TcpClientWrapper("localhost", _testPort);
            _wrapper.Connect();
            await Task.Delay(200);

            // Act
            _wrapper.Connect();

            // Assert
            Assert.That(_wrapper.Connected, Is.True);

            SafeCloseClient(await serverTask);
        }

        [Test]
        public void Connect_InvalidHost_HandlesException()
        {
            // Arrange
            _wrapper = new TcpClientWrapper("invalid.host.that.does.not.exist", 12345);

            // Act & Assert - should not throw
            Assert.DoesNotThrow(() => _wrapper.Connect());
            Assert.That(_wrapper.Connected, Is.False);
        }

        [Test]
        public async Task Disconnect_WhenConnected_ClosesConnection()
        {
            // Arrange
            var serverTask = StartTestServerAsync();
            _wrapper = new TcpClientWrapper("localhost", _testPort);
            _wrapper.Connect();
            await Task.Delay(200);

            // Act
            _wrapper.Disconnect();

            // Assert
            Assert.That(_wrapper.Connected, Is.False);

            SafeCloseClient(await serverTask);
        }

        [Test]
        public void Disconnect_WhenNotConnected_DoesNotThrow()
        {
            // Arrange
            _wrapper = new TcpClientWrapper("localhost", _testPort);

            // Act & Assert
            Assert.DoesNotThrow(() => _wrapper.Disconnect());
        }

        [Test]
        public void Disconnect_MultipleCalls_DoesNotThrow()
        {
            // Arrange
            _wrapper = new TcpClientWrapper("localhost", _testPort);

            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                _wrapper.Disconnect();
                _wrapper.Disconnect();
                _wrapper.Disconnect();
            });
        }

        [Test]
        public async Task SendMessageAsync_ByteArray_SendsData()
        {
            // Arrange
            var serverTask = StartTestServerAsync();
            _wrapper = new TcpClientWrapper("localhost", _testPort);
            _wrapper.Connect();
            await Task.Delay(200);

            var serverClient = await serverTask;
            var serverStream = serverClient.GetStream();
            var testData = new byte[] { 0x01, 0x02, 0x03, 0x04 };

            // Act
            await _wrapper.SendMessageAsync(testData);

            // Assert - Read from server side
            var buffer = new byte[1024];
            var bytesRead = await serverStream.ReadAsync(buffer, 0, buffer.Length);
            var receivedData = buffer.Take(bytesRead).ToArray();

            Assert.That(receivedData, Is.EqualTo(testData));

            // Cleanup
            SafeCloseClient(serverClient);
        }

        [Test]
        public async Task SendMessageAsync_String_SendsEncodedData()
        {
            // Arrange
            var serverTask = StartTestServerAsync();
            _wrapper = new TcpClientWrapper("localhost", _testPort);
            _wrapper.Connect();
            await Task.Delay(200);

            var serverClient = await serverTask;
            var serverStream = serverClient.GetStream();
            var testMessage = "Hello, TCP!";

            // Act
            await _wrapper.SendMessageAsync(testMessage);

            // Assert - Read from server side
            var buffer = new byte[1024];
            var bytesRead = await serverStream.ReadAsync(buffer, 0, buffer.Length);
            var receivedMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);

            Assert.That(receivedMessage, Is.EqualTo(testMessage));

            // Cleanup
            SafeCloseClient(serverClient);
        }

        [Test]
        public void SendMessageAsync_ByteArray_WhenNotConnected_ThrowsInvalidOperationException()
        {
            // Arrange
            _wrapper = new TcpClientWrapper("localhost", _testPort);
            var testData = new byte[] { 0x01, 0x02 };

            // Act & Assert
            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _wrapper.SendMessageAsync(testData));
        }

        [Test]
        public void SendMessageAsync_String_WhenNotConnected_ThrowsInvalidOperationException()
        {
            // Arrange
            _wrapper = new TcpClientWrapper("localhost", _testPort);

            // Act & Assert
            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _wrapper.SendMessageAsync("test"));
        }

        [Test]
        public async Task MessageReceived_ReceivesDataFromServer()
        {
            // Arrange
            var serverTask = StartTestServerAsync();
            _wrapper = new TcpClientWrapper("localhost", _testPort);

            byte[]? receivedData = null;
            var messageReceivedEvent = new ManualResetEventSlim(false);

            _wrapper.MessageReceived += (sender, data) =>
            {
                receivedData = data;
                messageReceivedEvent.Set();
            };

            _wrapper.Connect();
            await Task.Delay(200);

            var serverClient = await serverTask;
            var serverStream = serverClient.GetStream();

            // Act - Send data from server to client
            var testData = Encoding.UTF8.GetBytes("Server message");
            await serverStream.WriteAsync(testData, 0, testData.Length);

            // Assert
            bool received = messageReceivedEvent.Wait(TimeSpan.FromSeconds(2));
            Assert.That(received, Is.True, "Message was not received within timeout");
            Assert.That(receivedData, Is.Not.Null);
            Assert.That(receivedData, Is.EqualTo(testData));

            // Cleanup
            SafeCloseClient(serverClient);
        }

        [Test]
        public async Task MessageReceived_MultipleMessages_AllReceived()
        {
            // Arrange
            var serverTask = StartTestServerAsync();
            _wrapper = new TcpClientWrapper("localhost", _testPort);

            var receivedMessages = new System.Collections.Concurrent.ConcurrentBag<byte[]>();
            var messageCount = 0;
            var expectedMessages = 3;
            var allMessagesReceived = new ManualResetEventSlim(false);

            _wrapper.MessageReceived += (sender, data) =>
            {
                receivedMessages.Add(data);
                if (Interlocked.Increment(ref messageCount) >= expectedMessages)
                {
                    allMessagesReceived.Set();
                }
            };

            _wrapper.Connect();
            await Task.Delay(200);

            var serverClient = await serverTask;
            var serverStream = serverClient.GetStream();

            // Act - Send multiple messages
            for (int i = 0; i < expectedMessages; i++)
            {
                var data = Encoding.UTF8.GetBytes($"Message {i}");
                await serverStream.WriteAsync(data, 0, data.Length);
                await Task.Delay(50);
            }

            // Assert
            bool received = allMessagesReceived.Wait(TimeSpan.FromSeconds(3));
            Assert.That(received, Is.True, $"Expected {expectedMessages} messages but received {messageCount}");
            Assert.That(receivedMessages.Count, Is.EqualTo(expectedMessages));

            // Cleanup
            SafeCloseClient(serverClient);
        }

        [Test]
        public async Task MessageReceived_NoSubscribers_DoesNotThrow()
        {
            // Arrange
            var serverTask = StartTestServerAsync();
            _wrapper = new TcpClientWrapper("localhost", _testPort);

            _wrapper.Connect();
            await Task.Delay(200);

            var serverClient = await serverTask;
            var serverStream = serverClient.GetStream();

            // Act - Send data with no subscribers
            var testData = Encoding.UTF8.GetBytes("Test");
            await serverStream.WriteAsync(testData, 0, testData.Length);
            await Task.Delay(200);

            // Assert - Should not throw
            Assert.That(_wrapper.Connected, Is.True);

            // Cleanup
            SafeCloseClient(serverClient);
        }

        [Test]
        public async Task StartListening_StopsOnDisconnect()
        {
            // Arrange
            var serverTask = StartTestServerAsync();
            _wrapper = new TcpClientWrapper("localhost", _testPort);

            var messageReceived = false;
            _wrapper.MessageReceived += (sender, data) => { messageReceived = true; };

            _wrapper.Connect();
            await Task.Delay(200);

            var serverClient = await serverTask;

            // Act - Disconnect
            _wrapper.Disconnect();
            await Task.Delay(200);

            var serverStream = serverClient.GetStream();
            var testData = Encoding.UTF8.GetBytes("After disconnect");
            try
            {
                await serverStream.WriteAsync(testData, 0, testData.Length);
            }
            catch
            {
                // Expected - connection closed
            }

            await Task.Delay(200);

            // Assert - Message should not be received after disconnect
            Assert.That(_wrapper.Connected, Is.False);
            Assert.That(messageReceived, Is.False, "Should not receive messages after disconnect");

            // Cleanup
            SafeCloseClient(serverClient);
        }

        [Test]
        public async Task Connect_StartsListeningAutomatically()
        {
            // Arrange
            var serverTask = StartTestServerAsync();
            _wrapper = new TcpClientWrapper("localhost", _testPort);

            var messageReceived = false;
            var messageReceivedEvent = new ManualResetEventSlim(false);

            _wrapper.MessageReceived += (sender, data) =>
            {
                messageReceived = true;
                messageReceivedEvent.Set();
            };

            // Act - Connect starts listening automatically
            _wrapper.Connect();
            await Task.Delay(200);

            var serverClient = await serverTask;
            var serverStream = serverClient.GetStream();
            var testData = Encoding.UTF8.GetBytes("Auto listening test");
            await serverStream.WriteAsync(testData, 0, testData.Length);

            // Assert
            bool received = messageReceivedEvent.Wait(TimeSpan.FromSeconds(2));
            Assert.That(received, Is.True);
            Assert.That(messageReceived, Is.True);

            // Cleanup
            SafeCloseClient(serverClient);
        }

        [Test]
        public async Task SendMessageAsync_AfterDisconnect_ThrowsException()
        {
            // Arrange
            var serverTask = StartTestServerAsync();
            _wrapper = new TcpClientWrapper("localhost", _testPort);
            _wrapper.Connect();
            await Task.Delay(200);

            var serverClient = await serverTask;

            // Act
            _wrapper.Disconnect();

            // Assert
            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _wrapper.SendMessageAsync("test"));

            // Cleanup
            SafeCloseClient(serverClient);
        }

        [Test]
        public async Task LargeMessage_SendsAndReceivesCorrectly()
        {
            // Arrange
            var serverTask = StartTestServerAsync();
            _wrapper = new TcpClientWrapper("localhost", _testPort);

            byte[]? receivedData = null;
            var messageReceivedEvent = new ManualResetEventSlim(false);

            _wrapper.MessageReceived += (sender, data) =>
            {
                if (receivedData == null)
                {
                    receivedData = data;
                    messageReceivedEvent.Set();
                }
            };

            _wrapper.Connect();
            await Task.Delay(200);

            var serverClient = await serverTask;
            var serverStream = serverClient.GetStream();

            // Act - Send large message
            var largeData = new byte[4096];
            new Random().NextBytes(largeData);
            await serverStream.WriteAsync(largeData, 0, largeData.Length);
            await serverStream.FlushAsync();

            // Assert
            bool received = messageReceivedEvent.Wait(TimeSpan.FromSeconds(3));
            Assert.That(received, Is.True, "Large message was not received");
            Assert.That(receivedData, Is.Not.Null);
            Assert.That(receivedData.Length, Is.GreaterThanOrEqualTo(largeData.Length));

            // Cleanup
            SafeCloseClient(serverClient);
        }
    }
}
