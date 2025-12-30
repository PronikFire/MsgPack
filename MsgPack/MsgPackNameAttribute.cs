using System;

namespace MsgPack;

[AttributeUsage(AttributeTargets.Field)]
public class MsgPackNameAttribute(string name) : Attribute
{
    public string name = name;
}
