using System;

namespace MsgPack;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class MsgPackIgnoreAttribute : Attribute
{
}
