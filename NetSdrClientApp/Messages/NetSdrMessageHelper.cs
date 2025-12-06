using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NetSdrClientApp.Messages
{

    public static class NetSdrMessageHelper
    {
        private const short _maxMessageLength = 8191;
        private const short _maxDataItemMessageLength = 8194;
        private const short _msgHeaderLength = 2; //2 byte, 16 bit
        private const short _msgControlItemLength = 2; //2 byte, 16 bit
        private const short _msgSequenceNumberLength = 2; //2 byte, 16 bit

        public const short MaxMessageLength = 8191;
        public const short MaxDataItemMessageLength = 8194;

        public enum MsgTypes
        {
            SetControlItem,
            CurrentControlItem,
            ControlItemRange,
            Ack,
            DataItem0,
            DataItem1,
            DataItem2,
            DataItem3
        }

        public enum ControlItemCodes
        {
            None = 0,
            IQOutputDataSampleRate = 0x00B8,
            RFFilter = 0x0044,
            ADModes = 0x008A,
            ReceiverState = 0x0018,
            ReceiverFrequency = 0x0020
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct ControlItemHeader
        {
            public ushort MessageHeader;
            public ushort ItemCode;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct DataItemHeader
        {
            public ushort MessageHeader;
            public ushort SequenceNumber;
        }

        public static byte[] GetControlItemMessage(MsgTypes type, ControlItemCodes itemCode, byte[] parameters)
        {
            return GetMessage(type, itemCode, parameters);
        }

        public static byte[] GetDataItemMessage(MsgTypes type, byte[] parameters)
        {
            return GetMessage(type, ControlItemCodes.None, parameters);
        }

        private static byte[] GetMessage(MsgTypes type, ControlItemCodes itemCode, byte[] parameters)
        {
            var itemCodeBytes = Array.Empty<byte>();
            if (itemCode != ControlItemCodes.None)
            {
                itemCodeBytes = BitConverter.GetBytes((ushort)itemCode);
            }

            var headerBytes = GetHeader(type, itemCodeBytes.Length + parameters.Length);

            List<byte> msg = new List<byte>();
            msg.AddRange(headerBytes);
            msg.AddRange(itemCodeBytes);
            msg.AddRange(parameters);

            return msg.ToArray();
        }

        public static bool TranslateMessage(byte[] msg, out MsgTypes type, out ControlItemCodes itemCode, out ushort sequenceNumber, out byte[] body)
        {
            // Initialize all out parameters at the start
            type = MsgTypes.SetControlItem;
            itemCode = ControlItemCodes.None;
            sequenceNumber = 0;
            body = Array.Empty<byte>();

            // Validate input
            if (!ValidateMessageInput(msg))
            {
                return false;
            }

            // Parse header
            if (!ParseMessageHeader(msg, out type, out int messageLength))
            {
                return false;
            }

            // Parse message body based on type
            return ParseMessageBody(msg, type, messageLength, out itemCode, out sequenceNumber, out body);
        }

        private static bool ValidateMessageInput(byte[] msg)
        {
            return msg != null && msg.Length >= 2;
        }

        private static bool ParseMessageHeader(byte[] msg, out MsgTypes type, out int messageLength)
        {
            type = MsgTypes.SetControlItem;
            messageLength = 0;

            try
            {
                var headerValue = BitConverter.ToUInt16(msg, 0);
                type = (MsgTypes)(headerValue >> 13);
                messageLength = headerValue & 0x1FFF;

                return msg.Length == messageLength;
            }
            catch
            {
                return false;
            }
        }

        private static bool ParseMessageBody(byte[] msg, MsgTypes type, int messageLength, out ControlItemCodes itemCode, out ushort sequenceNumber, out byte[] body)
        {
            itemCode = ControlItemCodes.None;
            sequenceNumber = 0;
            body = Array.Empty<byte>();

            if (IsDataItemMessage(type))
            {
                return ParseDataItemMessage(msg, type, out itemCode, out sequenceNumber, out body);
            }
            else
            {
                return ParseControlMessage(msg, out itemCode, out body);
            }
        }

        private static bool IsDataItemMessage(MsgTypes type)
        {
            return type == MsgTypes.DataItem0 || type == MsgTypes.DataItem1 || type == MsgTypes.DataItem2;
        }

        private static bool ParseDataItemMessage(byte[] msg, MsgTypes type, out ControlItemCodes itemCode, out ushort sequenceNumber, out byte[] body)
        {
            itemCode = ControlItemCodes.None;
            sequenceNumber = 0;
            body = Array.Empty<byte>();

            try
            {
                if (type == MsgTypes.DataItem0)
                {
                    if (msg.Length < 4) return false;

                    var itemCodeValue = BitConverter.ToUInt16(msg, 2);
                    if (!Enum.IsDefined(typeof(ControlItemCodes), (int)itemCodeValue))
                    {
                        return false;
                    }

                    itemCode = (ControlItemCodes)itemCodeValue;
                    body = msg.Skip(4).ToArray();
                }
                else if (type == MsgTypes.DataItem1)
                {
                    if (msg.Length < 4) return false;

                    sequenceNumber = BitConverter.ToUInt16(msg, 2);
                    body = msg.Skip(4).ToArray();
                }
                else // DataItem2
                {
                    body = msg.Skip(2).ToArray();
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool ParseControlMessage(byte[] msg, out ControlItemCodes itemCode, out byte[] body)
        {
            itemCode = ControlItemCodes.None;
            body = Array.Empty<byte>();

            try
            {
                if (msg.Length < 4) return false;

                var itemCodeValue = BitConverter.ToUInt16(msg, 2);
                if (!Enum.IsDefined(typeof(ControlItemCodes), (int)itemCodeValue))
                {
                    return false;
                }

                itemCode = (ControlItemCodes)itemCodeValue;
                body = msg.Skip(4).ToArray();

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static IEnumerable<int> GetSamples(ushort sampleSize, byte[] body)
        {
            ValidateSampleSize(sampleSize);
            return GetSamplesIterator(sampleSize, body);
        }

        private static void ValidateSampleSize(ushort sampleSize)
        {
            if (sampleSize > 32 || sampleSize == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleSize),
                    "Sample size must be between 1 and 32 bits");
            }
        }

        private static IEnumerable<int> GetSamplesIterator(ushort sampleSize, byte[] body)
        {
            if (body == null || body.Length == 0)
            {
                yield break;
            }

            int bytesPerSample = (sampleSize + 7) / 8;

            for (int i = 0; i < body.Length; i += bytesPerSample)
            {
                if (i + bytesPerSample > body.Length)
                {
                    break;
                }

                int sample = 0;
                for (int j = 0; j < bytesPerSample && j < 4; j++)
                {
                    sample |= (body[i + j] << (j * 8));
                }

                yield return sample;
            }
        }

        private static byte[] GetHeader(MsgTypes type, int msgLength)
        {
            int lengthWithHeader = msgLength + 2;

            //Data Items edge case
            if (type >= MsgTypes.DataItem0 && lengthWithHeader == _maxDataItemMessageLength)
            {
                lengthWithHeader = 0;
            }

            if (msgLength < 0 || lengthWithHeader > _maxMessageLength)
            {
                throw new ArgumentException("Message length exceeds allowed value");
            }

            return BitConverter.GetBytes((ushort)(lengthWithHeader + ((int)type << 13)));
        }

        private static void TranslateHeader(byte[] header, out MsgTypes type, out int msgLength)
        {
            var num = BitConverter.ToUInt16(header.ToArray());
            type = (MsgTypes)(num >> 13);
            msgLength = num - ((int)type << 13);

            if (type >= MsgTypes.DataItem0 && msgLength == 0)
            {
                msgLength = _maxDataItemMessageLength;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MessageHeader
    {
        public ushort HeaderValue;
        public MessageHeader(NetSdrMessageHelper.MsgTypes type, int msgLength)
        {
            int lengthWithHeader = msgLength + 2;

            //Data Items edge case
            if (type >= NetSdrMessageHelper.MsgTypes.DataItem0 && lengthWithHeader == NetSdrMessageHelper.MaxDataItemMessageLength)
            {
                lengthWithHeader = 0;
            }

            if (msgLength < 0 || lengthWithHeader > NetSdrMessageHelper.MaxMessageLength)
            {
                throw new ArgumentException("Message length exceeds allowed value");
            }

            HeaderValue = (ushort)(lengthWithHeader + ((int)type << 13));
        }

        public NetSdrMessageHelper.MsgTypes GetMessageType()
        {
            return (NetSdrMessageHelper.MsgTypes)(HeaderValue >> 13);
        }
    }
}
