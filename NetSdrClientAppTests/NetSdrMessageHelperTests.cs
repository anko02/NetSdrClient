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

        [Test]
        public void TranslateMessage_NullMessage_ReturnsFalse()
        {
            //Arrange
            byte[]? msg = null;

            //Act
            bool result = NetSdrMessageHelper.TranslateMessage(msg!, out var type, out var itemCode,
                out var sequenceNumber, out var body);

            //Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void TranslateMessage_MessageTooShort_ReturnsFalse()
        {
            //Arrange
            byte[] msg = new byte[] { 0x01 }; // Only 1 byte, needs at least 2

            //Act
            bool result = NetSdrMessageHelper.TranslateMessage(msg, out var type, out var itemCode,
                out var sequenceNumber, out var body);

            //Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void TranslateMessage_ControlMessage_Success()
        {
            //Arrange
            var testBody = new byte[] { 0x11, 0x22, 0x33 };
            var msgType = NetSdrMessageHelper.MsgTypes.SetControlItem;
            var controlCode = NetSdrMessageHelper.ControlItemCodes.ReceiverFrequency;
            var msg = NetSdrMessageHelper.GetControlItemMessage(msgType, controlCode, testBody);

            //Act
            bool result = NetSdrMessageHelper.TranslateMessage(msg, out var type, out var itemCode,
                out var sequenceNumber, out var body);

            //Assert
            Assert.That(result, Is.True);
            Assert.That(type, Is.EqualTo(msgType));
            Assert.That(itemCode, Is.EqualTo(controlCode));
            Assert.That(body, Is.EqualTo(testBody));
        }

        [Test]
        public void TranslateMessage_ControlMessage_InvalidItemCode_ReturnsFalse()
        {
            //Arrange
            var msgType = NetSdrMessageHelper.MsgTypes.CurrentControlItem;
            var invalidCode = (ushort)7777; // Invalid code
            var testBody = new byte[] { 0xAA, 0xBB };

            // Manually construct message with invalid code
            var headerBytes = GetHeaderBytes(msgType, 4 + testBody.Length);
            var codeBytes = BitConverter.GetBytes(invalidCode);
            var msg = headerBytes.Concat(codeBytes).Concat(testBody).ToArray();

            //Act
            bool result = NetSdrMessageHelper.TranslateMessage(msg, out var type, out var itemCode,
                out var sequenceNumber, out var body);

            //Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void TranslateMessage_ControlMessage_TooShort_ReturnsFalse()
        {
            //Arrange
            var msgType = NetSdrMessageHelper.MsgTypes.Ack;
            var headerBytes = GetHeaderBytes(msgType, 3); // Header says 3 bytes but we need 4 minimum
            var msg = headerBytes.Concat(new byte[] { 0x01 }).ToArray();

            //Act
            bool result = NetSdrMessageHelper.TranslateMessage(msg, out var type, out var itemCode,
                out var sequenceNumber, out var body);

            //Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void TranslateMessage_DataItem2_Success()
        {
            //Arrange
            var testBody = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
            var msgType = NetSdrMessageHelper.MsgTypes.DataItem2;
            var msg = NetSdrMessageHelper.GetDataItemMessage(msgType, testBody);

            //Act
            bool result = NetSdrMessageHelper.TranslateMessage(msg, out var type, out var itemCode,
                out var sequenceNumber, out var body);

            //Assert
            Assert.That(result, Is.True);
            Assert.That(type, Is.EqualTo(msgType));
            Assert.That(itemCode, Is.EqualTo(NetSdrMessageHelper.ControlItemCodes.None));
            Assert.That(sequenceNumber, Is.EqualTo(0));
            Assert.That(body, Is.EqualTo(testBody));
        }

        [Test]
        public void TranslateMessage_DataItem0_TooShort_ReturnsFalse()
        {
            //Arrange
            var msgType = NetSdrMessageHelper.MsgTypes.DataItem0;
            var headerBytes = GetHeaderBytes(msgType, 3); // Too short for DataItem0 (needs 4+ bytes)
            var msg = headerBytes.Concat(new byte[] { 0x01 }).ToArray();

            //Act
            bool result = NetSdrMessageHelper.TranslateMessage(msg, out var type, out var itemCode,
                out var sequenceNumber, out var body);

            //Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void TranslateMessage_DataItem1_TooShort_ReturnsFalse()
        {
            //Arrange
            var msgType = NetSdrMessageHelper.MsgTypes.DataItem1;
            var headerBytes = GetHeaderBytes(msgType, 3); // Too short for DataItem1 (needs 4+ bytes)
            var msg = headerBytes.Concat(new byte[] { 0x01 }).ToArray();

            //Act
            bool result = NetSdrMessageHelper.TranslateMessage(msg, out var type, out var itemCode,
                out var sequenceNumber, out var body);

            //Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void GetSamples_8BitSamples_Success()
        {
            //Arrange
            ushort sampleSize = 8;
            var body = new byte[] { 0x01, 0x02, 0x03, 0x04 };

            //Act
            var samples = NetSdrMessageHelper.GetSamples(sampleSize, body).ToList();

            //Assert
            Assert.That(samples.Count, Is.EqualTo(4));
            Assert.That(samples[0], Is.EqualTo(0x01));
            Assert.That(samples[1], Is.EqualTo(0x02));
            Assert.That(samples[2], Is.EqualTo(0x03));
            Assert.That(samples[3], Is.EqualTo(0x04));
        }

        [Test]
        public void GetSamples_16BitSamples_Success()
        {
            //Arrange
            ushort sampleSize = 16;
            var body = new byte[] { 0x01, 0x02, 0x03, 0x04 };

            //Act
            var samples = NetSdrMessageHelper.GetSamples(sampleSize, body).ToList();

            //Assert
            Assert.That(samples.Count, Is.EqualTo(2));
            Assert.That(samples[0], Is.EqualTo(0x0201)); // Little-endian
            Assert.That(samples[1], Is.EqualTo(0x0403));
        }

        [Test]
        public void GetSamples_24BitSamples_Success()
        {
            //Arrange
            ushort sampleSize = 24;
            var body = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 };

            //Act
            var samples = NetSdrMessageHelper.GetSamples(sampleSize, body).ToList();

            //Assert
            Assert.That(samples.Count, Is.EqualTo(2));
            Assert.That(samples[0], Is.EqualTo(0x030201)); // Little-endian
            Assert.That(samples[1], Is.EqualTo(0x060504));
        }

        [Test]
        public void GetSamples_32BitSamples_Success()
        {
            //Arrange
            ushort sampleSize = 32;
            var body = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };

            //Act
            var samples = NetSdrMessageHelper.GetSamples(sampleSize, body).ToList();

            //Assert
            Assert.That(samples.Count, Is.EqualTo(2));
            Assert.That(samples[0], Is.EqualTo(0x04030201)); // Little-endian
            Assert.That(samples[1], Is.EqualTo(0x08070605));
        }

        [Test]
        public void GetSamples_PartialLastSample_SkipsIncompleteData()
        {
            //Arrange
            ushort sampleSize = 16;
            var body = new byte[] { 0x01, 0x02, 0x03 }; // 3 bytes, last byte is incomplete

            //Act
            var samples = NetSdrMessageHelper.GetSamples(sampleSize, body).ToList();

            //Assert
            Assert.That(samples.Count, Is.EqualTo(1)); // Only complete samples
            Assert.That(samples[0], Is.EqualTo(0x0201));
        }

        [Test]
        public void GetSamples_NullBody_ReturnsEmpty()
        {
            //Arrange
            ushort sampleSize = 16;
            byte[]? body = null;

            //Act
            var samples = NetSdrMessageHelper.GetSamples(sampleSize, body!).ToList();

            //Assert
            Assert.That(samples.Count, Is.EqualTo(0));
        }

        [Test]
        public void GetSamples_ZeroSampleSize_ThrowsException()
        {
            //Arrange
            ushort sampleSize = 0;
            var body = new byte[] { 0x01, 0x02 };

            //Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                NetSdrMessageHelper.GetSamples(sampleSize, body).ToList());
        }

        [Test]
        public void GetSamples_SampleSize33_ThrowsException()
        {
            //Arrange
            ushort sampleSize = 33; // Just over the limit
            var body = new byte[] { 0x01, 0x02 };

            //Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                NetSdrMessageHelper.GetSamples(sampleSize, body).ToList());
        }

        [Test]
        public void GetControlItemMessage_NegativeLengthCalculation_ThrowsException()
        {
            //Arrange
            var type = NetSdrMessageHelper.MsgTypes.SetControlItem;
            var code = NetSdrMessageHelper.ControlItemCodes.ReceiverState;
            var parameters = new byte[0];

            //Act & Assert - This should work with 0 length
            var msg = NetSdrMessageHelper.GetControlItemMessage(type, code, parameters);
            Assert.That(msg.Length, Is.EqualTo(4)); // 2 header + 2 code
        }

        [Test]
        public void GetDataItemMessage_MaxDataItemLength_EdgeCase()
        {
            //Arrange
            var type = NetSdrMessageHelper.MsgTypes.DataItem0;
            // MaxDataItemMessageLength is 8194, subtract 2 for header, 2 for item code = 8190
            var parameters = BitConverter.GetBytes((ushort)NetSdrMessageHelper.ControlItemCodes.ReceiverState)
                .Concat(new byte[8190]).ToArray();

            //Act
            var msg = NetSdrMessageHelper.GetDataItemMessage(type, parameters);

            //Assert
            Assert.That(msg, Is.Not.Null);
            Assert.That(msg.Length, Is.EqualTo(8194)); // Should equal MaxDataItemMessageLength
        }

        [Test]
        public void MessageHeader_Constructor_ValidMessage()
        {
            //Arrange
            var type = NetSdrMessageHelper.MsgTypes.Ack;
            int msgLength = 100;

            //Act
            var header = new NetSdrMessageHelper.MessageHeader(type, msgLength);

            //Assert
            Assert.That(header.GetMessageType(), Is.EqualTo(type));
        }

        [Test]
        public void MessageHeader_Constructor_DataItemEdgeCase()
        {
            //Arrange
            var type = NetSdrMessageHelper.MsgTypes.DataItem1;
            int msgLength = 8192; // Will result in lengthWithHeader == MaxDataItemMessageLength

            //Act
            var header = new NetSdrMessageHelper.MessageHeader(type, msgLength);

            //Assert
            Assert.That(header.GetMessageType(), Is.EqualTo(type));
        }

        [Test]
        public void MessageHeader_Constructor_ExceedsMaxLength_ThrowsException()
        {
            //Arrange
            var type = NetSdrMessageHelper.MsgTypes.SetControlItem;
            int msgLength = 8200; // Exceeds max

            //Act & Assert
            Assert.Throws<ArgumentException>(() =>
                new NetSdrMessageHelper.MessageHeader(type, msgLength));
        }

        [Test]
        public void MessageHeader_Constructor_NegativeLength_ThrowsException()
        {
            //Arrange
            var type = NetSdrMessageHelper.MsgTypes.CurrentControlItem;
            int msgLength = -1;

            //Act & Assert
            Assert.Throws<ArgumentException>(() =>
                new NetSdrMessageHelper.MessageHeader(type, msgLength));
        }

        private byte[] GetHeaderBytes(NetSdrMessageHelper.MsgTypes type, int totalLength)
        {
            return BitConverter.GetBytes((ushort)(totalLength + ((int)type << 13)));
        }

        [Test]
        public void GetControlItemMessage_AllControlMessageTypes_Success()
        {
            //Arrange & Act & Assert
            var types = new[]
            {
                NetSdrMessageHelper.MsgTypes.SetControlItem,
                NetSdrMessageHelper.MsgTypes.CurrentControlItem,
                NetSdrMessageHelper.MsgTypes.ControlItemRange,
                NetSdrMessageHelper.MsgTypes.Ack
            };

            foreach (var type in types)
            {
                var msg = NetSdrMessageHelper.GetControlItemMessage(
                    type,
                    NetSdrMessageHelper.ControlItemCodes.ReceiverState,
                    new byte[] { 0x01, 0x02 });

                Assert.That(msg, Is.Not.Null);
                Assert.That(msg.Length, Is.GreaterThan(0));
            }
        }

        [Test]
        public void GetDataItemMessage_AllDataItemTypes_Success()
        {
            //Arrange & Act & Assert
            var types = new[]
            {
                NetSdrMessageHelper.MsgTypes.DataItem0,
                NetSdrMessageHelper.MsgTypes.DataItem1,
                NetSdrMessageHelper.MsgTypes.DataItem2,
                NetSdrMessageHelper.MsgTypes.DataItem3
            };

            foreach (var type in types)
            {
                var msg = NetSdrMessageHelper.GetDataItemMessage(type, new byte[] { 0x01, 0x02 });

                Assert.That(msg, Is.Not.Null);
                Assert.That(msg.Length, Is.GreaterThan(0));
            }
        }

        [Test]
        public void TranslateMessage_AllValidControlItemCodes_Success()
        {
            //Arrange & Act & Assert
            var codes = new[]
            {
                NetSdrMessageHelper.ControlItemCodes.IQOutputDataSampleRate,
                NetSdrMessageHelper.ControlItemCodes.RFFilter,
                NetSdrMessageHelper.ControlItemCodes.ADModes,
                NetSdrMessageHelper.ControlItemCodes.ReceiverState,
                NetSdrMessageHelper.ControlItemCodes.ReceiverFrequency
            };

            foreach (var code in codes)
            {
                var msg = NetSdrMessageHelper.GetControlItemMessage(
                    NetSdrMessageHelper.MsgTypes.SetControlItem,
                    code,
                    new byte[] { 0x01 });

                bool result = NetSdrMessageHelper.TranslateMessage(msg, out var type, out var itemCode,
                    out var sequenceNumber, out var body);

                Assert.That(result, Is.True);
                Assert.That(itemCode, Is.EqualTo(code));
            }
        }
    }
}