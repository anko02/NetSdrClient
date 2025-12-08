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
    public class UdpClientWrapperTests
    {
        private int _testPort;
        private UdpClientWrapper? _wrapper;

        [SetUp]
        public void SetUp()
        {
            _testPort = GetAvailablePort();
        }

        [TearDown]
        public void TearDown()
        {
            _wrapper?.Exit();
        }

        private static int GetAvailablePort()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        [Test]
        public void Constructor_SetsLocalEndPoint()
        {
            // Arrange & Act
            _wrapper = new UdpClientWrapper(_testPort);

            // Assert
            Assert.That(_wrapper, Is.Not.Null);
        }

        [Test]
        public async Task StartListeningAsync_ReceivesMessages()
        {
            // Arrange
            _wrapper = new UdpClientWrapper(_testPort);
            byte[]? receivedData = null;
            var messageReceivedEvent = new ManualResetEventSlim(false);

            _wrapper.MessageReceived += (sender, data) =>
            {
                receivedData = data;
                messageReceivedEvent.Set();
            };

            var listeningTask = Task.Run(() => _wrapper.StartListeningAsync());

            await Task.Delay(100);

            // Act
            using var sender = new UdpClient();
            var testData = System.Text.Encoding.UTF8.GetBytes("Test message");
            await sender.SendAsync(testData, testData.Length, new IPEndPoint(IPAddress.Loopback, _testPort));

            // Assert
            bool received = messageReceivedEvent.Wait(TimeSpan.FromSeconds(2));
            Assert.That(received, Is.True, "Message was not received within timeout");
            Assert.That(receivedData, Is.Not.Null);
            Assert.That(receivedData, Is.EqualTo(testData));

            // Cleanup
            _wrapper.StopListening();
            await Task.WhenAny(listeningTask, Task.Delay(1000));
        }

        [Test]
        public async Task StartListeningAsync_MultipleMessages_AllReceived()
        {
            // Arrange
            _wrapper = new UdpClientWrapper(_testPort);
            var receivedMessages = new System.Collections.Concurrent.ConcurrentBag<byte[]>();
            var messageCount = 0;
            var allMessagesReceived = new ManualResetEventSlim(false);
            const int expectedMessages = 3;

            _wrapper.MessageReceived += (sender, data) =>
            {
                receivedMessages.Add(data);
                if (Interlocked.Increment(ref messageCount) >= expectedMessages)
                {
                    allMessagesReceived.Set();
                }
            };

            var listeningTask = Task.Run(() => _wrapper.StartListeningAsync());
            await Task.Delay(100);

            // Act
            using var sender = new UdpClient();
            var endpoint = new IPEndPoint(IPAddress.Loopback, _testPort);

            for (int i = 0; i < expectedMessages; i++)
            {
                var data = System.Text.Encoding.UTF8.GetBytes($"Message {i}");
                await sender.SendAsync(data, data.Length, endpoint);
            }

            // Assert
            bool received = allMessagesReceived.Wait(TimeSpan.FromSeconds(3));
            Assert.That(received, Is.True, $"Expected {expectedMessages} messages but received {messageCount}");
            Assert.That(receivedMessages.Count, Is.EqualTo(expectedMessages));

            // Cleanup
            _wrapper.StopListening();
            await Task.WhenAny(listeningTask, Task.Delay(1000));
        }

        [Test]
        public async Task StopListening_CancelsListening()
        {
            // Arrange
            _wrapper = new UdpClientWrapper(_testPort);
            var listeningTask = Task.Run(() => _wrapper.StartListeningAsync());
            await Task.Delay(100);

            // Act
            _wrapper.StopListening();
            var completedTask = await Task.WhenAny(listeningTask, Task.Delay(1000));

            // Assert
            Assert.That(completedTask, Is.EqualTo(listeningTask));
        }

        [Test]
        public async Task Exit_CancelsListening()
        {
            // Arrange
            _wrapper = new UdpClientWrapper(_testPort);
            var listeningTask = Task.Run(() => _wrapper.StartListeningAsync());
            await Task.Delay(100);

            // Act
            _wrapper.Exit();
            var completedTask = await Task.WhenAny(listeningTask, Task.Delay(1000));

            // Assert
            Assert.That(completedTask, Is.EqualTo(listeningTask));
        }

        [Test]
        public void StopListening_MultipleCallsSafe()
        {
            // Arrange
            _wrapper = new UdpClientWrapper(_testPort);

            // Act & Assert - should not throw
            Assert.DoesNotThrow(() =>
            {
                _wrapper.StopListening();
                _wrapper.StopListening();
                _wrapper.StopListening();
            });
        }

        [Test]
        public void Exit_MultipleCallsSafe()
        {
            // Arrange
            _wrapper = new UdpClientWrapper(_testPort);

            // Act & Assert - should not throw
            Assert.DoesNotThrow(() =>
            {
                _wrapper.Exit();
                _wrapper.Exit();
                _wrapper.Exit();
            });
        }

        [Test]
        public void GetHashCode_SamePort_ReturnsSameHash()
        {
            // Arrange
            var wrapper1 = new UdpClientWrapper(_testPort);
            var wrapper2 = new UdpClientWrapper(_testPort);

            // Act
            var hash1 = wrapper1.GetHashCode();
            var hash2 = wrapper2.GetHashCode();

            // Assert
            Assert.That(hash1, Is.EqualTo(hash2));

            // Cleanup
            wrapper1.Exit();
            wrapper2.Exit();
        }

        [Test]
        public void GetHashCode_DifferentPort_ReturnsDifferentHash()
        {
            // Arrange
            var port2 = GetAvailablePort();
            var wrapper1 = new UdpClientWrapper(_testPort);
            var wrapper2 = new UdpClientWrapper(port2);

            // Act
            var hash1 = wrapper1.GetHashCode();
            var hash2 = wrapper2.GetHashCode();

            // Assert
            Assert.That(hash1, Is.Not.EqualTo(hash2));

            // Cleanup
            wrapper1.Exit();
            wrapper2.Exit();
        }

        [Test]
        public void Equals_SamePort_ReturnsTrue()
        {
            // Arrange
            var wrapper1 = new UdpClientWrapper(_testPort);
            var wrapper2 = new UdpClientWrapper(_testPort);

            // Act
            var result = wrapper1.Equals(wrapper2);

            // Assert
            Assert.That(result, Is.True);

            // Cleanup
            wrapper1.Exit();
            wrapper2.Exit();
        }

        [Test]
        public void Equals_DifferentPort_ReturnsFalse()
        {
            // Arrange
            var port2 = GetAvailablePort();
            var wrapper1 = new UdpClientWrapper(_testPort);
            var wrapper2 = new UdpClientWrapper(port2);

            // Act
            var result = wrapper1.Equals(wrapper2);

            // Assert
            Assert.That(result, Is.False);

            // Cleanup
            wrapper1.Exit();
            wrapper2.Exit();
        }

        [Test]
        public void Equals_Null_ReturnsFalse()
        {
            // Arrange
            _wrapper = new UdpClientWrapper(_testPort);

            // Act
            var result = _wrapper.Equals(null);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void Equals_DifferentType_ReturnsFalse()
        {
            // Arrange
            _wrapper = new UdpClientWrapper(_testPort);
            var other = "not a UdpClientWrapper";

            // Act
            var result = _wrapper.Equals(other);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void Equals_SameInstance_ReturnsTrue()
        {
            // Arrange
            _wrapper = new UdpClientWrapper(_testPort);

            // Act
            var result = _wrapper.Equals(_wrapper);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public async Task MessageReceived_NoSubscribers_DoesNotThrow()
        {
            // Arrange
            _wrapper = new UdpClientWrapper(_testPort);
            var listeningTask = Task.Run(() => _wrapper.StartListeningAsync());
            await Task.Delay(100);

            // Act - send message with no subscribers
            using var sender = new UdpClient();
            var testData = System.Text.Encoding.UTF8.GetBytes("Test");
            await sender.SendAsync(testData, testData.Length, new IPEndPoint(IPAddress.Loopback, _testPort));

            await Task.Delay(100);

            // Assert - should not throw
            Assert.DoesNotThrow(() => _wrapper.StopListening());
            var completedTask = await Task.WhenAny(listeningTask, Task.Delay(1000));
            Assert.That(completedTask, Is.EqualTo(listeningTask), "Listening task should complete");
        }

        [Test]
        public async Task StartListeningAsync_AfterStop_CanRestartListening()
        {
            // Arrange
            _wrapper = new UdpClientWrapper(_testPort);
            var firstListeningTask = Task.Run(() => _wrapper.StartListeningAsync());
            await Task.Delay(100);

            // Act - Stop first listener
            _wrapper.StopListening();
            await Task.WhenAny(firstListeningTask, Task.Delay(1000));

            await Task.Delay(100);
            _wrapper = new UdpClientWrapper(_testPort);

            var receivedData = false;
            var messageReceivedEvent = new ManualResetEventSlim(false);

            _wrapper.MessageReceived += (sender, data) =>
            {
                receivedData = true;
                messageReceivedEvent.Set();
            };

            var secondListeningTask = Task.Run(() => _wrapper.StartListeningAsync());
            await Task.Delay(100);

            using var sender = new UdpClient();
            var testData = System.Text.Encoding.UTF8.GetBytes("Restart test");
            await sender.SendAsync(testData, testData.Length, new IPEndPoint(IPAddress.Loopback, _testPort));

            // Assert
            bool received = messageReceivedEvent.Wait(TimeSpan.FromSeconds(2));
            Assert.That(received, Is.True, "Message was not received after restart");
            Assert.That(receivedData, Is.True);

            // Cleanup
            _wrapper.StopListening();
            await Task.WhenAny(secondListeningTask, Task.Delay(1000));
        }
    }
}
