using System;

namespace MsgPack;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class MsgPackOrder(int order) : Attribute
{
    public readonly int order = order;
}
