using EchoServer;
using System.Net;
using System.Net.Sockets;
using System.Text;
namespace EchoServerTests
{
    public class EchoServerTests
    {
        private EchoServer.EchoServer? _server;
        private const int TestPort = 5555;

        [TearDown]
        public void TearDown()
        {
            _server?.Stop();
            _server = null;
            Thread.Sleep(100);
        }

        [Test]
        public async Task StartAsync_StartsServerSuccessfully()
        {
            // Arrange
            _server = new EchoServer.EchoServer(TestPort);
            var serverTask = Task.Run(() => _server.StartAsync());

            // Act
            await Task.Delay(500);

            // Assert
            using var client = new TcpClient();
            Assert.That(async () => await client.ConnectAsync("127.0.0.1", TestPort), Throws.Nothing);
        }

        [Test]
        public async Task HandleClient_EchoesBackReceivedData()
        {
            // Arrange
            _server = new EchoServer.EchoServer(TestPort);
            _ = Task.Run(() => _server.StartAsync());
            await Task.Delay(500);

            string testMessage = "Hello, Echo Server!";
            byte[] sendBuffer = Encoding.UTF8.GetBytes(testMessage);
            byte[] receiveBuffer = new byte[8192];

            // Act
            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", TestPort);
            NetworkStream stream = client.GetStream();

            await stream.WriteAsync(sendBuffer, 0, sendBuffer.Length);
            int bytesRead = await stream.ReadAsync(receiveBuffer, 0, receiveBuffer.Length);
            string receivedMessage = Encoding.UTF8.GetString(receiveBuffer, 0, bytesRead);

            // Assert
            Assert.That(receivedMessage, Is.EqualTo(testMessage));
            Assert.That(bytesRead, Is.EqualTo(sendBuffer.Length));
        }

        [Test]
        public async Task HandleClient_HandlesMultipleMessagesFromSameClient()
        {
            // Arrange
            _server = new EchoServer.EchoServer(TestPort);
            _ = Task.Run(() => _server.StartAsync());
            await Task.Delay(500);

            string[] messages = { "Message 1", "Message 2", "Message 3" };

            // Act & Assert
            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", TestPort);
            NetworkStream stream = client.GetStream();

            foreach (string message in messages)
            {
                byte[] sendBuffer = Encoding.UTF8.GetBytes(message);
                byte[] receiveBuffer = new byte[8192];

                await stream.WriteAsync(sendBuffer, 0, sendBuffer.Length);
                int bytesRead = await stream.ReadAsync(receiveBuffer, 0, receiveBuffer.Length);
                string receivedMessage = Encoding.UTF8.GetString(receiveBuffer, 0, bytesRead);

                Assert.That(receivedMessage, Is.EqualTo(message));
            }
        }

        [Test]
        public async Task HandleClient_HandlesMultipleConcurrentClients()
        {
            // Arrange
            _server = new EchoServer.EchoServer(TestPort);
            _ = Task.Run(() => _server.StartAsync());
            await Task.Delay(500);

            int clientCount = 5;
            var tasks = new Task[clientCount];

            // Act
            for (int i = 0; i < clientCount; i++)
            {
                int clientId = i;
                tasks[i] = Task.Run(async () =>
                {
                    using var client = new TcpClient();
                    await client.ConnectAsync("127.0.0.1", TestPort);
                    NetworkStream stream = client.GetStream();

                    string message = $"Client {clientId}";
                    byte[] sendBuffer = Encoding.UTF8.GetBytes(message);
                    byte[] receiveBuffer = new byte[8192];

                    await stream.WriteAsync(sendBuffer, 0, sendBuffer.Length);
                    int bytesRead = await stream.ReadAsync(receiveBuffer, 0, receiveBuffer.Length);
                    string receivedMessage = Encoding.UTF8.GetString(receiveBuffer, 0, bytesRead);

                    Assert.That(receivedMessage, Is.EqualTo(message));
                });
            }

            // Assert
            Assert.That(async () => await Task.WhenAll(tasks), Throws.Nothing);
        }

        [Test]
        public async Task HandleClient_HandlesLargeData()
        {
            // Arrange
            _server = new EchoServer.EchoServer(TestPort);
            _ = Task.Run(() => _server.StartAsync());
            await Task.Delay(500);

            byte[] largeData = new byte[8000];
            new Random().NextBytes(largeData);

            // Act
            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", TestPort);
            NetworkStream stream = client.GetStream();

            await stream.WriteAsync(largeData, 0, largeData.Length);
            byte[] receiveBuffer = new byte[8192];
            int bytesRead = await stream.ReadAsync(receiveBuffer, 0, receiveBuffer.Length);

            // Assert
            Assert.That(bytesRead, Is.EqualTo(largeData.Length));
            Assert.That(receiveBuffer[0..bytesRead], Is.EqualTo(largeData));
        }

        [Test]
        public async Task HandleClient_ClosesConnectionWhenClientDisconnects()
        {
            // Arrange
            _server = new EchoServer.EchoServer(TestPort);
            _ = Task.Run(() => _server.StartAsync());
            await Task.Delay(500);

            // Act
            var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", TestPort);
            client.Close();
            await Task.Delay(500);

            // Assert - Should be able to connect again with a new client
            using var newClient = new TcpClient();
            Assert.That(async () => await newClient.ConnectAsync("127.0.0.1", TestPort), Throws.Nothing);
        }
    }

    public class UdpTimedSenderTests
    {
        private UdpTimedSender? _sender;
        private UdpClient? _receiver;
        private const int TestPort = 6666;
        private const string TestHost = "127.0.0.1";

        [SetUp]
        public void SetUp()
        {
            _receiver = new UdpClient(TestPort);
        }

        [TearDown]
        public void TearDown()
        {
            _sender?.Dispose();
            _receiver?.Close();
            _receiver?.Dispose();
        }

        [Test]
        public void Constructor_InitializesCorrectly()
        {
            // Act
            _sender = new UdpTimedSender(TestHost, TestPort);

            // Assert
            Assert.That(_sender, Is.Not.Null);
        }

        [Test]
        public void StartSending_ThrowsExceptionWhenDisposed()
        {
            // Arrange
            _sender = new UdpTimedSender(TestHost, TestPort);
            _sender.Dispose();

            // Act & Assert
            Assert.That(() => _sender.StartSending(1000), Throws.TypeOf<ObjectDisposedException>());
        }

        [Test]
        public void StartSending_ThrowsExceptionWhenAlreadyRunning()
        {
            // Arrange
            _sender = new UdpTimedSender(TestHost, TestPort);
            _sender.StartSending(1000);

            // Act & Assert
            Assert.That(() => _sender.StartSending(1000), Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public async Task StartSending_SendsMessagesAtSpecifiedInterval()
        {
            // Arrange
            _sender = new UdpTimedSender(TestHost, TestPort);
            int messagesReceived = 0;
            int expectedMessages = 3;

            // Act
            _sender.StartSending(500);

            var receiveTask = Task.Run(async () =>
            {
                while (messagesReceived < expectedMessages)
                {
                    var result = await _receiver!.ReceiveAsync();
                    messagesReceived++;
                }
            });

            await Task.WhenAny(receiveTask, Task.Delay(3000));

            // Assert
            Assert.That(messagesReceived, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public async Task SendMessage_SendsCorrectMessageFormat()
        {
            // Arrange
            _sender = new UdpTimedSender(TestHost, TestPort);

            // Act
            _sender.StartSending(100);
            var result = await _receiver!.ReceiveAsync();

            // Assert
            Assert.That(result.Buffer, Is.Not.Null);
            Assert.That(result.Buffer.Length, Is.GreaterThan(4));
            Assert.That(result.Buffer[0], Is.EqualTo(0x04));
            Assert.That(result.Buffer[1], Is.EqualTo(0x84));
        }

        [Test]
        public async Task SendMessage_IncrementsSequenceNumber()
        {
            // Arrange
            _sender = new UdpTimedSender(TestHost, TestPort);

            // Act
            _sender.StartSending(100);

            var result1 = await _receiver!.ReceiveAsync();
            ushort seq1 = BitConverter.ToUInt16(result1.Buffer, 2);

            var result2 = await _receiver!.ReceiveAsync();
            ushort seq2 = BitConverter.ToUInt16(result2.Buffer, 2);

            // Assert
            Assert.That(seq2, Is.EqualTo(seq1 + 1));
        }

        [Test]
        public async Task SendMessage_ContainsRandomData()
        {
            // Arrange
            _sender = new UdpTimedSender(TestHost, TestPort);

            // Act
            _sender.StartSending(100);

            var result1 = await _receiver!.ReceiveAsync();
            var result2 = await _receiver!.ReceiveAsync();

            byte[] data1 = result1.Buffer[6..]; // Skip header
            byte[] data2 = result2.Buffer[6..];

            // Assert - Random data should be different
            Assert.That(data2, Is.Not.EqualTo(data1));
        }

        [Test]
        public async Task StopSending_StopsMessageTransmission()
        {
            // Arrange
            _sender = new UdpTimedSender(TestHost, TestPort);
            _sender.StartSending(200);

            await _receiver!.ReceiveAsync();

            // Act
            _sender.StopSending();
            await Task.Delay(500);

            // Assert
            _receiver.Client.ReceiveTimeout = 300;
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
            Assert.That(() => _receiver.Receive(ref remoteEP), Throws.TypeOf<SocketException>());
        }

        [Test]
        public void Dispose_CanBeCalledMultipleTimes()
        {
            // Arrange
            _sender = new UdpTimedSender(TestHost, TestPort);

            // Act & Assert
            Assert.That(() =>
            {
                _sender.Dispose();
                _sender.Dispose();
            }, Throws.Nothing);
        }

        [Test]
        public async Task SendMessage_SendsExpectedPayloadSize()
        {
            // Arrange
            _sender = new UdpTimedSender(TestHost, TestPort);
            int expectedSize = 2 + 2 + 1024;

            // Act
            _sender.StartSending(100);
            var result = await _receiver!.ReceiveAsync();

            // Assert
            Assert.That(result.Buffer.Length, Is.EqualTo(expectedSize));
        }
    }

    [TestFixture]
    public class IntegrationTests
    {
        [Test]
        public async Task EchoServerAndUdpSender_CanRunConcurrently()
        {
            // Arrange
            var echoServer = new EchoServer.EchoServer(5557);
            var serverTask = Task.Run(() => echoServer.StartAsync());
            await Task.Delay(500);

            using var udpReceiver = new UdpClient(6667);
            using var udpSender = new UdpTimedSender("127.0.0.1", 6667);

            // Act
            udpSender.StartSending(300);

            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync("127.0.0.1", 5557);
            NetworkStream stream = tcpClient.GetStream();

            byte[] testData = Encoding.UTF8.GetBytes("Test");
            await stream.WriteAsync(testData, 0, testData.Length);

            byte[] receiveBuffer = new byte[1024];
            int bytesRead = await stream.ReadAsync(receiveBuffer, 0, receiveBuffer.Length);

            var udpResult = await udpReceiver.ReceiveAsync();

            // Assert
            Assert.That(bytesRead, Is.EqualTo(testData.Length));
            Assert.That(udpResult.Buffer, Is.Not.Null);
            Assert.That(udpResult.Buffer.Length, Is.GreaterThan(0));

            // Cleanup
            udpSender.StopSending();
            echoServer.Stop();
        }
    }
}