using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

public static class MathUtils
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Wrap(int value, int length)
    {
        var r = value % length;
        return r < 0 ? r + length : r;
    }
}