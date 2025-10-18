using System.Buffers.Binary;
using System.Reflection;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System;

namespace MsgPack;

public static class MsgPackSerialize
{
    public static byte[] Serialize(object? obj)
    {
        if (obj == null)
            return [0xC0];

        switch (obj)
        {
            case byte byteValue:
                return [0xCC, byteValue];
            case ushort ushortValue:
                {
                    Span<byte> buf = stackalloc byte[2];
                    BinaryPrimitives.WriteUInt16BigEndian(buf, ushortValue);
                    return [0xCD, .. buf];
                }
            case uint uintValue:
                {
                    Span<byte> buf = stackalloc byte[4];
                    BinaryPrimitives.WriteUInt32BigEndian(buf, uintValue);
                    return [0xCE, .. buf];
                }
            case ulong ulongValue:
                {
                    Span<byte> buf = stackalloc byte[8];
                    BinaryPrimitives.WriteUInt64BigEndian(buf, ulongValue);
                    return [0xCF, .. buf];
                }
            case sbyte sbyteValue:
                return [0xD0, (byte)sbyteValue];
            case short shortValue:
                {
                    Span<byte> buf = stackalloc byte[2];
                    BinaryPrimitives.WriteInt16BigEndian(buf, shortValue);
                    return [0xD1, .. buf];
                }
            case int intValue:
                {
                    Span<byte> buf = stackalloc byte[4];
                    BinaryPrimitives.WriteInt32BigEndian(buf, intValue);
                    return [0xD2, .. buf];
                }
            case long longValue:
                {
                    Span<byte> buf = stackalloc byte[8];
                    BinaryPrimitives.WriteInt64BigEndian(buf, longValue);
                    return [0xD3, .. buf];
                }
            case bool boolValue:
                return [boolValue ? (byte)0xC3 : (byte)0xC2];
            case float floatValue:
                {
                    Span<byte> buf = stackalloc byte[4];
                    BinaryPrimitives.WriteSingleBigEndian(buf, floatValue);
                    return [0xCA, .. buf];
                }
            case double doubleValue:
                {
                    Span<byte> buf = stackalloc byte[8];
                    BinaryPrimitives.WriteDoubleBigEndian(buf, doubleValue);
                    return [0xCB, .. buf];
                }
            case string strValue:
                var bytes = Encoding.UTF8.GetBytes(strValue);
                uint len = (uint)bytes.LongLength;
                if (len <= 31)
                    return [(byte)(0xA0 | len), .. bytes];
                else if (len <= byte.MaxValue)
                    return [0xD9, (byte)len, .. bytes];
                else if (len <= ushort.MaxValue)
                {
                    Span<byte> buf = stackalloc byte[2];
                    BinaryPrimitives.WriteUInt16BigEndian(buf, (ushort)len);
                    return [0xDA, .. buf, .. bytes];
                }
                else
                {
                    Span<byte> buf = stackalloc byte[4];
                    BinaryPrimitives.WriteUInt32BigEndian(buf, len);
                    return [0xDB, .. buf, .. bytes];
                }
            case byte[] byteArrayValue:
                long length = byteArrayValue.LongLength;
                if (length <= byte.MaxValue)
                    return [0xC4, (byte)length, .. byteArrayValue];
                else if (length <= ushort.MaxValue)
                {
                    Span<byte> buf = stackalloc byte[2];
                    BinaryPrimitives.WriteUInt16BigEndian(buf, (ushort)length);
                    return [0xC5, .. buf, .. byteArrayValue];
                }
                else
                {
                    Span<byte> buf = stackalloc byte[4];
                    BinaryPrimitives.WriteUInt32BigEndian(buf, (uint)length);
                    return [0xC6, .. buf, .. byteArrayValue];
                }
            case object?[] valuesArray:
                {
                    uint count = (uint)valuesArray.LongLength;
                    if (count == 0)
                        return [0x90];

                    List<byte> buf = [];
                    foreach (var item in valuesArray)
                        buf.AddRange(Serialize(item));

                    if (count <= 15)
                        return [(byte)(0x90 | count), .. buf];
                    else if (count <= ushort.MaxValue)
                    {
                        Span<byte> lenBuf = stackalloc byte[2];
                        BinaryPrimitives.WriteUInt16BigEndian(lenBuf, (ushort)count);
                        return [0xDC, .. lenBuf, .. buf];
                    }
                    else
                    {
                        Span<byte> lenBuf = stackalloc byte[4];
                        BinaryPrimitives.WriteUInt32BigEndian(lenBuf, count);
                        return [0xDD, .. lenBuf, .. buf];
                    }
                }
            case System.Collections.IDictionary dictionary:
                {
                    uint count = (uint)dictionary.Count;
                    if (count == 0)
                        return [0x80];

                    List<byte> buf = [];
                    foreach (System.Collections.DictionaryEntry kvp in dictionary)
                    {
                        buf.AddRange(Serialize(kvp.Key));
                        buf.AddRange(Serialize(kvp.Value));
                    }

                    if (count <= 15)
                        return [(byte)(0x80 | count), .. buf];
                    else if (count <= ushort.MaxValue)
                    {
                        Span<byte> lenBuf = stackalloc byte[2];
                        BinaryPrimitives.WriteUInt16BigEndian(lenBuf, (ushort)count);
                        return [0xDE, .. lenBuf, .. buf];
                    }
                    else
                    {
                        Span<byte> lenBuf = stackalloc byte[4];
                        BinaryPrimitives.WriteUInt32BigEndian(lenBuf, count);
                        return [0xDF, .. lenBuf, .. buf];
                    }
                }
            default:
                {
                    var fields = obj.GetType().GetFields();
                    
                    Dictionary<string, object> objDictionary = [];
                    foreach (var field in fields)
                    {
                        if (field.GetCustomAttribute<MsgPackIgnoreAttribute>() is not null)
                            continue;

                        string name = field.GetCustomAttribute<MsgPackNameAttribute>()?.Name ?? field.Name;
                        objDictionary.Add(name, field.GetValue(obj)!);
                    }
                    return Serialize(objDictionary);
                }
        }
    }

    public static object? Deserialize(Span<byte> data) => Deserialize(data, out _);
    public static T Deserialize<T>(Span<byte> data)
    {
        object? deserialized = Deserialize(data);
        if (deserialized is not Dictionary<object, object?> dict || dict.Count == 0)
            throw new Exception("Deserialized data cannot be converted to the target type.");

        var type = typeof(T);
        object obj = type.IsValueType ? Activator.CreateInstance(type)! : Activator.CreateInstance<T>();
        var fields = type.GetFields();
        foreach (var field in fields)
        {
            if (field.GetCustomAttribute<MsgPackIgnoreAttribute>() is not null)
                continue;

            string name = field.GetCustomAttribute<MsgPackNameAttribute>()?.Name ?? field.Name;
            if (!dict.TryGetValue(name, out object? value))
                throw new Exception($"Field '{name}' not found in the deserialized data.");
            try
            {
                field.SetValue(obj, Convert.ChangeType(value, field.FieldType));
            }
            catch
            {
                field.SetValue(obj, value);
            }
        }
        return (T)obj;
    }


    private static object? Deserialize(Span<byte> data, out uint length)
    {
        length = 1;
        if (data[0] == 0xC0)
            return null;

        switch (data[0])
        {
            case > 0x00 and < 0x7F:
                return data[0];
            case > 0xE0 and < 0xFF:
                return -(data[0] & 0x1F);
            case 0xCC:
                length = 2;
                return data[1];
            case 0xCD:
                length = 3;
                return BinaryPrimitives.ReadUInt16BigEndian(data[1..]);
            case 0xCE:
                length = 5;
                return BinaryPrimitives.ReadUInt32BigEndian(data[1..]);
            case 0xCF:
                length = 9;
                return BinaryPrimitives.ReadUInt64BigEndian(data[1..]);
            case 0xD0:
                length = 2;
                return (sbyte)data[1];
            case 0xD1:
                length = 3;
                return BinaryPrimitives.ReadInt16BigEndian(data[1..3]);
            case 0xD2:
                length = 5;
                return BinaryPrimitives.ReadInt32BigEndian(data[1..5]);
            case 0xD3:
                length = 9;
                return BinaryPrimitives.ReadInt64BigEndian(data[1..9]);
            case 0xC2:
                return false;
            case 0xC3:
                return true;
            case 0xCA:
                length = 5;
                return BinaryPrimitives.ReadSingleBigEndian(data[1..5]);
            case 0xCB:
                length = 9;
                return BinaryPrimitives.ReadDoubleBigEndian(data[1..9]);
            case >= 0xA0 and <= 0xBF:
                length = (uint)(1 + (data[0] & 0x1F));
                return Encoding.UTF8.GetString(data[1..(int)length]);
            case 0xD9:
                length = (uint)(2 + data[1]);
                return Encoding.UTF8.GetString(data[2..(int)length]);
            case 0xDA:
                {
                    length = (uint)(3 + BinaryPrimitives.ReadUInt16BigEndian(data[1..3]));
                    return Encoding.UTF8.GetString(data[3..(int)length]);
                }
            case 0xDB:
                {
                    length = 5 + BinaryPrimitives.ReadUInt32BigEndian(data[1..5]);
                    return Encoding.UTF8.GetString(data[5..(int)length]);
                }
            case 0xC4:
                {
                    uint arrayLength = data[1];
                    byte[] byteArray = new byte[arrayLength];
                    length = 2 + arrayLength;
                    byteArray = [.. data[2..(int)length]];
                    return byteArray;
                }
            case 0xC5:
                {
                    uint arrayLength = BinaryPrimitives.ReadUInt16BigEndian(data[1..3]);
                    byte[] byteArray = new byte[arrayLength];
                    length = 3 + arrayLength;
                    byteArray = [.. data[3..(int)length]];
                    return byteArray;
                }
            case 0xC6:
                {
                    uint arrayLength = BinaryPrimitives.ReadUInt32BigEndian(data[1..5]);
                    byte[] byteArray = new byte[arrayLength];
                    length = 5 + arrayLength;
                    byteArray = [.. data[5..(int)length]];
                    return byteArray;
                }
            case >= 0x90 and <= 0x9F:
                {
                    uint count = (uint)(data[0] & 0x0F);
                    object?[] list = new object?[count];
                    length = 1;
                    for (uint i = 0; i < count; i++)
                    {
                        list[i] = Deserialize(data[(int)length..], out uint itemLength);
                        length += itemLength;
                    }
                    return list;
                }
            case 0xDC:
                {
                    uint count = BinaryPrimitives.ReadUInt16BigEndian(data[1..3]);
                    object?[] list = new object?[count];
                    length = 3;
                    for (uint i = 0; i < count; i++)
                    {
                        list[i] = Deserialize(data[(int)length..], out uint itemLength);
                        length += itemLength;
                    }
                    return list;
                }
            case 0xDD:
                {
                    uint count = BinaryPrimitives.ReadUInt32BigEndian(data[1..5]);
                    object?[] list = new object?[count];
                    length = 5;
                    for (uint i = 0; i < count; i++)
                    {
                        list[i] = Deserialize(data[(int)length..], out uint itemLength);
                        length += itemLength;
                    }
                    return list;
                }
            case >= 0x80 and <= 0x8F:
                {
                    uint count = (uint)(data[0] & 0x0F);
                    Dictionary<object, object?> dict = [];
                    length = 1;
                    for (uint i = 0; i < count; i++)
                    {
                        object? key = Deserialize(data[(int)length..], out uint keyLength);
                        length += keyLength;
                        object? value = Deserialize(data[(int)length..], out uint valueLength);
                        length += valueLength;
                        dict.Add(key!, value);
                    }
                    return dict;
                }
            case 0xDE:
                {
                    uint count = BinaryPrimitives.ReadUInt16BigEndian(data[1..3]);
                    Dictionary<object, object?> dict = [];
                    length = 3;
                    for (uint i = 0; i < count; i++)
                    {
                        object? key = Deserialize(data[(int)length..], out uint keyLength);
                        length += keyLength;
                        object? value = Deserialize(data[(int)length..], out uint valueLength);
                        length += valueLength;
                        dict.Add(key!, value);
                    }
                    return dict;
                }
            case 0xDF:
                {
                    uint count = BinaryPrimitives.ReadUInt32BigEndian(data[1..5]);
                    Dictionary<object, object?> dict = [];
                    length = 5;
                    for (uint i = 0; i < count; i++)
                    {
                        object? key = Deserialize(data[(int)length..], out uint keyLength);
                        length += keyLength;
                        object? value = Deserialize(data[(int)length..], out uint valueLength);
                        length += valueLength;
                        dict.Add(key!, value);
                    }
                    return dict;
                }
            default:
                throw new Exception($"Unsupported MsgPack format: 0x{data[0]:X2}");
        }
    }
}
