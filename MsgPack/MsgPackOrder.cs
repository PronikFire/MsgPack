using System;

namespace MsgPack;

[AttributeUsage(AttributeTargets.Field)]
public class MsgPackOrder(int order) : Attribute
{
    public int order;
}
