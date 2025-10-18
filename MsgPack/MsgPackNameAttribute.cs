using System;

namespace MsgPack;

[AttributeUsage(AttributeTargets.Field)]
public class MsgPackNameAttribute : Attribute
{
    public string? Name;
}
