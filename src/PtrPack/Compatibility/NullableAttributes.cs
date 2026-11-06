using System;

namespace System.Runtime.CompilerServices;

// Il2Cppmscorlib does not ship the C# 8 nullable metadata attributes.
// Defining them locally lets the compiler emit nullable annotations while
// still compiling against the generated interop assemblies.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.ReturnValue | AttributeTargets.GenericParameter, AllowMultiple = false)]
internal sealed class NullableAttribute : Attribute
{
    internal readonly byte[] NullableFlags;

    internal NullableAttribute(byte flag)
    {
        NullableFlags = new[] { flag };
    }

    internal NullableAttribute(byte[] flags)
    {
        NullableFlags = flags;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Interface | AttributeTargets.Delegate, AllowMultiple = false)]
internal sealed class NullableContextAttribute : Attribute
{
    internal readonly byte Flag;

    internal NullableContextAttribute(byte flag)
    {
        Flag = flag;
    }
}
