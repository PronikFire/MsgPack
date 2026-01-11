using System;

namespace MsgPack;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class MsgPackIncludeAttribute : Attribute
{
}
