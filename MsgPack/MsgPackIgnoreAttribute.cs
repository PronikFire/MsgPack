using System;

namespace MsgPack;

[AttributeUsage(AttributeTargets.Field)]
public class MsgPackIgnoreAttribute : Attribute
{
    public bool DeserializeToo = true;
}
