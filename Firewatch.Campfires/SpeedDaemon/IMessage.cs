using System.Text;
using System.Threading.Channels;

namespace Firewatch.Campfires.SpeedDaemon;

public interface IMessage
{
    byte MessageType { get; }
    void WriteTo(Stream stream);
}

public static class MessageFactory
{
    public static async Task<IMessage> ReadMessageAsync(Stream stream)
    {
        var buffer = new byte[1];
        _ = await stream.ReadAsync(buffer);
        return buffer[0] switch
        {
            0x10 => Error.ReadFrom(stream),
            0x20 => Plate.ReadFrom(stream),
            0x21 => Ticket.ReadFrom(stream),
            0x40 => WantHeartBeat.ReadFrom(stream),
            0x41 => HeartBeat.ReadFrom(stream),
            0x80 => AmCamera.ReadFrom(stream),
            0x81 => AmDispatcher.ReadFrom(stream),
            _ => throw new NotSupportedException($"Message type {buffer[0]} is not supported.")
        };
    }
}

// MESSAGE TYPES

public record Error() : IMessage
{
    public byte MessageType => 0x10;

    public void WriteTo(Stream stream)
    {
        MessageFunctions.WriteU8(stream, MessageType);
    }
    
    public static Error ReadFrom(Stream stream)
    {
        return new Error();
    }
}

public record Plate(string PlateNumber, uint Timestamp) : IMessage
{
    public byte MessageType => 0x20;

    public void WriteTo(Stream stream)
    {
        MessageFunctions.WriteU8(stream, MessageType);
        MessageFunctions.WriteString(stream, PlateNumber);
        MessageFunctions.WriteU32(stream, Timestamp);
    }
    
    public static Plate ReadFrom(Stream stream)
    {
        var plateNumber = MessageFunctions.ReadString(stream);
        var timestamp = MessageFunctions.ReadU32(stream);
        return new Plate(plateNumber, timestamp);
    }
}

public record Reading(ushort Road, ushort Mile, string PlateNumber, uint Timestamp) : IMessage
{
    public byte MessageType => 0x22;

    public void WriteTo(Stream stream)
    {
        MessageFunctions.WriteU8(stream, MessageType);
        MessageFunctions.WriteU16(stream, Road);
        MessageFunctions.WriteU16(stream, Mile);
        MessageFunctions.WriteString(stream, PlateNumber);
        MessageFunctions.WriteU32(stream, Timestamp);
    }
    
    public static Reading ReadFrom(Stream stream)
    {
        var road = MessageFunctions.ReadU16(stream);
        var mile = MessageFunctions.ReadU16(stream);
        var plateNumber = MessageFunctions.ReadString(stream);
        var timestamp = MessageFunctions.ReadU32(stream);
        return new Reading(road, mile, plateNumber, timestamp);
    }
}

public record Ticket(
    string Plate,
    ushort Road,
    ushort MileOne,
    uint TimeStampOne,
    ushort MileTwo,
    uint TimeStampTwo,
    ushort Speed) : IMessage
{
    public byte MessageType => 0x21;

    public void WriteTo(Stream stream)
    {
        MessageFunctions.WriteU8(stream, MessageType);
        MessageFunctions.WriteString(stream, Plate);
        MessageFunctions.WriteU16(stream, Road);
        MessageFunctions.WriteU16(stream, MileOne);
        MessageFunctions.WriteU32(stream, TimeStampOne);
        MessageFunctions.WriteU16(stream, MileTwo);
        MessageFunctions.WriteU32(stream, TimeStampTwo);
        MessageFunctions.WriteU16(stream, Speed);
    }
    
    public static Ticket ReadFrom(Stream stream)
    {
        var plate = MessageFunctions.ReadString(stream);
        var road = MessageFunctions.ReadU16(stream);
        var mileOne = MessageFunctions.ReadU16(stream);
        var timeStampOne = MessageFunctions.ReadU32(stream);
        var mileTwo = MessageFunctions.ReadU16(stream);
        var timeStampTwo = MessageFunctions.ReadU32(stream);
        var speed = MessageFunctions.ReadU16(stream);
        return new Ticket(plate, road, mileOne, timeStampOne, mileTwo, timeStampTwo, speed);
    }
}

public record WantHeartBeat(uint Interval) : IMessage
{
    public byte MessageType => 0x40;
    
    public void WriteTo(Stream stream)
    {
        MessageFunctions.WriteU8(stream, MessageType);
        MessageFunctions.WriteU32(stream, Interval);
    }
    
    public static WantHeartBeat ReadFrom(Stream stream)
    {
        var interval = MessageFunctions.ReadU32(stream);
        return new WantHeartBeat(interval);
    }
}

public record HeartBeat() : IMessage
{
    public byte MessageType => 0x41;
    
    public void WriteTo(Stream stream)
    {
        MessageFunctions.WriteU8(stream, MessageType);
    }
    
    public static HeartBeat ReadFrom(Stream stream)
    {
        return new HeartBeat();
    }
}

public record AmCamera(ushort Road, ushort Mile, ushort SpeedLimit) : IMessage
{
    public byte MessageType => 0x80;
    
    public void WriteTo(Stream stream)
    {
        MessageFunctions.WriteU8(stream, MessageType);
        MessageFunctions.WriteU16(stream, Road);
        MessageFunctions.WriteU16(stream, Mile);
        MessageFunctions.WriteU16(stream, SpeedLimit);
    }
    
    public static AmCamera ReadFrom(Stream stream)
    {
        var road = MessageFunctions.ReadU16(stream);
        var mile = MessageFunctions.ReadU16(stream);
        var limit = MessageFunctions.ReadU16(stream);
        return new AmCamera(road, mile, limit);
    }
}

public record AmDispatcher(byte NumRoads, ushort[] Roads) : IMessage
{
    public byte MessageType => 0x81;
    
    public void WriteTo(Stream stream)
    {
        MessageFunctions.WriteU8(stream, MessageType);
        MessageFunctions.WriteU8(stream, NumRoads);
        foreach (var road in Roads)
        {
            MessageFunctions.WriteU16(stream, road);
        }
    }
    
    public static AmDispatcher ReadFrom(Stream stream)
    {
        var numRoads = MessageFunctions.ReadU8(stream);
        var roads = new ushort[numRoads];
        for (int i = 0; i < numRoads; i++)
        {
            roads[i] = MessageFunctions.ReadU16(stream);
        }
        return new AmDispatcher(numRoads, roads);
    }
}

public record RegisterDispatcher(byte NumRoads, ushort[] Roads, ChannelWriter<Ticket> TicketChannel)
    : AmDispatcher(NumRoads, Roads);

// MESSAGE FUNCTIONS

public static class MessageFunctions
{
    
    // String methods
    public static void WriteString(Stream stream, string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        stream.WriteByte((byte)bytes.Length);
        stream.Write(bytes);
    }

    public static string ReadString(Stream stream)
    {
        var length = stream.ReadByte();
        var buffer = new byte[length];
        stream.ReadExactly(buffer, 0, length);
        return Encoding.ASCII.GetString(buffer);
    }
    
    // U8 methods (big endian)
    public static void WriteU8(Stream stream, byte value)
    {
        stream.WriteByte(value);
    }
    
    public static byte ReadU8(Stream stream)
    {
        return (byte)stream.ReadByte();
    }
    
    // U16 methods (big endian)
    public static void WriteU16(Stream stream, ushort value)
    {
        stream.WriteByte((byte)(value >> 8)); // Write the high byte
        stream.WriteByte((byte)(value)); // Truncated automatically to 8 bits
    }

    public static ushort ReadU16(Stream stream)
    {
        return (ushort)((stream.ReadByte() << 8) | stream.ReadByte());
    }
    
    // U32 methods (big endian)
    public static void WriteU32(Stream stream, uint value)
    {
        stream.WriteByte((byte)(value >> 24)); // Write the highest byte
        stream.WriteByte((byte)(value >> 16)); // Write the second highest byte
        stream.WriteByte((byte)(value >> 8)); // Write the third highest byte
        stream.WriteByte((byte)(value)); // Write the lowest byte
    }
    
    public static uint ReadU32(Stream stream)
    {
        return (uint)((stream.ReadByte() << 24) | (stream.ReadByte() << 16) | (stream.ReadByte() << 8) | stream.ReadByte());
    }
}

