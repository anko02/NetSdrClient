using NetSdrClientApp.Messages;

namespace NetSdrClientAppTests
{
    public class NetSdrMessageHelperTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void GetControlItemMessageTest()
        {
            //Arrange
            var type = NetSdrMessageHelper.MsgTypes.Ack;
            var code = NetSdrMessageHelper.ControlItemCodes.ReceiverState;
            int parametersLength = 7500;

            //Act
            byte[] msg = NetSdrMessageHelper.GetControlItemMessage(type, code, new byte[parametersLength]);

            var headerBytes = msg.Take(2);
            var codeBytes = msg.Skip(2).Take(2);
            var parametersBytes = msg.Skip(4);

            var num = BitConverter.ToUInt16(headerBytes.ToArray());
            var actualType = (NetSdrMessageHelper.MsgTypes)(num >> 13);
            var actualLength = num - ((int)actualType << 13);
            var actualCode = BitConverter.ToInt16(codeBytes.ToArray());

            //Assert
            Assert.That(headerBytes.Count(), Is.EqualTo(2));
            Assert.That(msg.Length, Is.EqualTo(actualLength));
            Assert.That(type, Is.EqualTo(actualType));

            Assert.That(actualCode, Is.EqualTo((short)code));

            Assert.That(parametersBytes.Count(), Is.EqualTo(parametersLength));
        }

        [Test]
        public void GetDataItemMessageTest()
        {
            //Arrange
            var type = NetSdrMessageHelper.MsgTypes.DataItem2;
            int parametersLength = 7500;

            //Act
            byte[] msg = NetSdrMessageHelper.GetDataItemMessage(type, new byte[parametersLength]);

            var headerBytes = msg.Take(2);
            var parametersBytes = msg.Skip(2);

            var num = BitConverter.ToUInt16(headerBytes.ToArray());
            var actualType = (NetSdrMessageHelper.MsgTypes)(num >> 13);
            var actualLength = num - ((int)actualType << 13);

            //Assert
            Assert.That(headerBytes.Count(), Is.EqualTo(2));
            Assert.That(msg.Length, Is.EqualTo(actualLength));
            Assert.That(type, Is.EqualTo(actualType));

            Assert.That(parametersBytes.Count(), Is.EqualTo(parametersLength));
        }

        //TODO: add more NetSdrMessageHelper tests

        [Test]
        public void TranslateMessage_DataItem_WithItemCode_Success()
        {
            //Arrange
            var testBody = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
            var itemCodeValue = (ushort)NetSdrMessageHelper.ControlItemCodes.ReceiverState;
            var itemCodeBytes = BitConverter.GetBytes(itemCodeValue);
            var fullBody = itemCodeBytes.Concat(testBody).ToArray();

            var msgType = NetSdrMessageHelper.MsgTypes.DataItem0;
            var msg = NetSdrMessageHelper.GetDataItemMessage(msgType, fullBody);

            //Act
            bool result = NetSdrMessageHelper.TranslateMessage(msg, out var type, out var itemCode,
                out var sequenceNumber, out var body);

            //Assert
            Assert.That(result, Is.True);
            Assert.That(type, Is.EqualTo(msgType));
            Assert.That(itemCode, Is.EqualTo(NetSdrMessageHelper.ControlItemCodes.ReceiverState));
            Assert.That(body, Is.EqualTo(testBody));
        }

        [Test]
        public void TranslateMessage_DataItem_WithSequenceNumber_Success()
        {
            //Arrange
            var testBody = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };
            var sequenceNum = (ushort)12345;
            var seqNumBytes = BitConverter.GetBytes(sequenceNum);
            var fullBody = seqNumBytes.Concat(testBody).ToArray();

            var msgType = NetSdrMessageHelper.MsgTypes.DataItem1;
            var msg = NetSdrMessageHelper.GetDataItemMessage(msgType, fullBody);

            //Act
            bool result = NetSdrMessageHelper.TranslateMessage(msg, out var type, out var itemCode,
                out var sequenceNumber, out var body);

            //Assert
            Assert.That(result, Is.True);
            Assert.That(type, Is.EqualTo(msgType));
            Assert.That(itemCode, Is.EqualTo(NetSdrMessageHelper.ControlItemCodes.None));
            Assert.That(sequenceNumber, Is.EqualTo(sequenceNum));
            Assert.That(body, Is.EqualTo(testBody));
        }

        [Test]
        public void TranslateMessage_InvalidItemCode_ReturnsFalse()
        {
            //Arrange
            var itemCodeValue = (ushort)9999; // Invalid code
            var itemCodeBytes = BitConverter.GetBytes(itemCodeValue);
            var testBody = new byte[] { 0x01, 0x02 };
            var fullBody = itemCodeBytes.Concat(testBody).ToArray();

            var msg = NetSdrMessageHelper.GetDataItemMessage(NetSdrMessageHelper.MsgTypes.DataItem0, fullBody);

            //Act
            bool result = NetSdrMessageHelper.TranslateMessage(msg, out var type, out var itemCode,
                out var sequenceNumber, out var body);

            //Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void TranslateMessage_BodyLengthMismatch_ReturnsFalse()
        {
            //Arrange
            var testBody = new byte[] { 0x01, 0x02, 0x03 };
            var msg = NetSdrMessageHelper.GetDataItemMessage(NetSdrMessageHelper.MsgTypes.DataItem2, testBody);

            // Corrupt the message by truncating it
            var corruptedMsg = msg.Take(msg.Length - 1).ToArray();

            //Act
            bool result = NetSdrMessageHelper.TranslateMessage(corruptedMsg, out var type, out var itemCode,
                out var sequenceNumber, out var body);

            //Assert
            Assert.That(result, Is.False);
        }

        
        [Test]
        public void GetSamples_InvalidSampleSize_ThrowsException()
        {
            //Arrange
            ushort sampleSize = 40; // Exceeds 32 bits (4 bytes)
            var body = new byte[10];

            //Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                NetSdrMessageHelper.GetSamples(sampleSize, body).ToList());
        }

        [Test]
        public void GetSamples_EmptyBody_ReturnsEmptyEnumerable()
        {
            //Arrange
            ushort sampleSize = 16;
            var body = new byte[0];

            //Act
            var samples = NetSdrMessageHelper.GetSamples(sampleSize, body).ToList();

            //Assert
            Assert.That(samples.Count, Is.EqualTo(0));
        }

        [Test]
        public void TranslateMessage_MultipleSequentialCalls_WorksCorrectly()
        {
            //Arrange
            var messages = new List<byte[]>
            {
                NetSdrMessageHelper.GetDataItemMessage(NetSdrMessageHelper.MsgTypes.DataItem0,
                    BitConverter.GetBytes((ushort)NetSdrMessageHelper.ControlItemCodes.ReceiverState).Concat(new byte[] { 0x01 }).ToArray()),
                NetSdrMessageHelper.GetDataItemMessage(NetSdrMessageHelper.MsgTypes.DataItem1,
                    BitConverter.GetBytes((ushort)100).Concat(new byte[] { 0x02 }).ToArray()),
                NetSdrMessageHelper.GetDataItemMessage(NetSdrMessageHelper.MsgTypes.DataItem2,
                    new byte[] { 0x03 })
            };

            //Act & Assert
            foreach (var msg in messages)
            {
                bool result = NetSdrMessageHelper.TranslateMessage(msg, out var type, out var itemCode,
                    out var sequenceNumber, out var body);
                Assert.That(result, Is.True);
            }
        }

    }
}