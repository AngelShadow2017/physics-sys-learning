using System.Runtime.CompilerServices;
using TrueSync;
using Unity.Burst;
using UnityEngine;
[BurstCompile]
public partial struct MathBurstedFix
{
    public const long MAX_VALUE = long.MaxValue;
    public const long MIN_VALUE = long.MinValue;
    public const long MaxValue = long.MaxValue-1;
    public const long MinValue = long.MinValue + 2;
    public const int NUM_BITS = 64;
    public const int FRACTIONAL_PLACES = 32;
    public const long ONE = 1L << FRACTIONAL_PLACES;
    public const long TEN = 10L << FRACTIONAL_PLACES;
    public const long HALF = 1L << (FRACTIONAL_PLACES - 1);
    public const long PI_TIMES_2 = 0x6487ED511;
    public const long PI = 0x3243F6A88;
    public const long PI_OVER_2 = 0x1921FB544;
    public const long LN2 = 0xB17217F7;
    public const long LOG2MAX = 0x1F00000000;
    public const long LOG2MIN = -0x2000000000;
    public const int LUT_SIZE = (int)(PI_OVER_2 >> 15);
    [BurstCompile(DisableDirectCall = true,OptimizeFor = OptimizeFor.Performance)]
    public static int CountLeadingZeroes(ulong x)
    {
        int result = 0;
        while ((x & 0xF000000000000000) == 0) { result += 4; x <<= 4; }
        while ((x & 0x8000000000000000) == 0) { result += 1; x <<= 1; }
        return result;
    }
    [BurstCompile(DisableDirectCall=true, OptimizeFor = OptimizeFor.Performance)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long Division(in long xl,in long yl)
    {

        if (yl == 0)
        {
            return MAX_VALUE;
            //throw new DivideByZeroException();
        }

        var remainder = (ulong)(xl >= 0 ? xl : -xl);
        var divider = (ulong)(yl >= 0 ? yl : -yl);
        var quotient = 0UL;
        var bitPos = NUM_BITS / 2 + 1;


        // If the divider is divisible by 2^n, take advantage of it.
        while ((divider & 0xF) == 0 && bitPos >= 4)
        {
            divider >>= 4;
            bitPos -= 4;
        }

        while (remainder != 0 && bitPos >= 0)
        {
            int shift = CountLeadingZeroes(remainder);
            if (shift > bitPos)
            {
                shift = bitPos;
            }
            remainder <<= shift;
            bitPos -= shift;

            var div = remainder / divider;
            remainder = remainder % divider;
            quotient += div << bitPos;

            // Detect overflow
            if ((div & ~(0xFFFFFFFFFFFFFFFF >> bitPos)) != 0)
            {
                return ((xl ^ yl) & MIN_VALUE) == 0 ? MaxValue : MinValue;
            }

            remainder <<= 1;
            --bitPos;
        }

        // rounding
        ++quotient;
        var result = (long)(quotient >> 1);
        if (((xl ^ yl) & MIN_VALUE) != 0)
        {
            result = -result;
        }
        return result;
        //return new FP(result);
    }
    [BurstCompile(DisableDirectCall = true, OptimizeFor = OptimizeFor.Performance)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long Multiply(in long xl,in long yl) {
        var xlo = (ulong)(xl & 0x00000000FFFFFFFF);
        var xhi = xl >> FRACTIONAL_PLACES;
        var ylo = (ulong)(yl & 0x00000000FFFFFFFF);
        var yhi = yl >> FRACTIONAL_PLACES;

        var lolo = xlo * ylo;
        var lohi = (long)xlo * yhi;
        var hilo = xhi * (long)ylo;
        var hihi = xhi * yhi;

        var loResult = lolo >> FRACTIONAL_PLACES;
        var midResult1 = lohi;
        var midResult2 = hilo;
        var hiResult = hihi << FRACTIONAL_PLACES;

        var sum = (long)loResult + midResult1 + midResult2 + hiResult;
        return sum;
    }
    [BurstCompile(DisableDirectCall = true, OptimizeFor = OptimizeFor.Performance)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static long AddOverflowHelper(in long x, in long y, ref bool overflow)
    {
        var sum = x + y;
        // x + y overflows if sign(x) ^ sign(y) != sign(sum)
        overflow |= ((x ^ y ^ sum) & MIN_VALUE) != 0;
        return sum;
    }
    [BurstCompile(DisableDirectCall = true, OptimizeFor = OptimizeFor.Performance)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long OverflowMultiply(in long xl,in long yl) {
        var xlo = (ulong)(xl & 0x00000000FFFFFFFF);
        var xhi = xl >> FRACTIONAL_PLACES;
        var ylo = (ulong)(yl & 0x00000000FFFFFFFF);
        var yhi = yl >> FRACTIONAL_PLACES;

        var lolo = xlo * ylo;
        var lohi = (long)xlo * yhi;
        var hilo = xhi * (long)ylo;
        var hihi = xhi * yhi;

        var loResult = lolo >> FRACTIONAL_PLACES;
        var midResult1 = lohi;
        var midResult2 = hilo;
        var hiResult = hihi << FRACTIONAL_PLACES;

        bool overflow = false;
        var sum = AddOverflowHelper((long)loResult, midResult1, ref overflow);
        sum = AddOverflowHelper(sum, midResult2, ref overflow);
        sum = AddOverflowHelper(sum, hiResult, ref overflow);

        bool opSignsEqual = ((xl ^ yl) & MIN_VALUE) == 0;

        // if signs of operands are equal and sign of result is negative,
        // then multiplication overflowed positively
        // the reverse is also true
        if (opSignsEqual)
        {
            if (sum < 0 || (overflow && xl > 0))
            {
                return MaxValue;
            }
        }
        else
        {
            if (sum > 0)
            {
                return MinValue;
            }
        }

        // if the top 32 bits of hihi (unused in the result) are neither all 0s or 1s,
        // then this means the result overflowed.
        var topCarry = hihi >> FRACTIONAL_PLACES;
        if (topCarry != 0 && topCarry != -1 /*&& xl != -17 && yl != -17*/)
        {
            return opSignsEqual ? MaxValue : MinValue;
        }

        // If signs differ, both operands' magnitudes are greater than 1,
        // and the result is greater than the negative operand, then there was negative overflow.
        if (!opSignsEqual)
        {
            long posOp, negOp;
            if (xl > yl)
            {
                posOp = xl;
                negOp = yl;
            }
            else
            {
                posOp = yl;
                negOp = xl;
            }
            if (sum > negOp && negOp < -ONE && posOp > ONE)
            {
                return MinValue;
            }
        }
        return sum;

    }
    [BurstCompile(DisableDirectCall = true, OptimizeFor = OptimizeFor.Performance)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Sqrt(ref long xl) {
        if (xl < 0)
        {
            // We cannot represent infinities like Single and Double, and Sqrt is
            // mathematically undefined for x < 0. So we just throw an exception.
            //throw new ArgumentOutOfRangeException("Negative value passed to Sqrt", "x");
            xl = long.MaxValue;
        }

        var num = (ulong)xl;
        var result = 0UL;

        // second-to-top bit
        var bit = 1UL << (NUM_BITS - 2);

        while (bit > num)
        {
            bit >>= 2;
        }

        // The main part is executed twice, in order to avoid
        // using 128 bit values in computations.
        for (var i = 0; i < 2; ++i)
        {
            // First we get the top 48 bits of the answer.
            while (bit != 0)
            {
                if (num >= result + bit)
                {
                    num -= result + bit;
                    result = (result >> 1) + bit;
                }
                else
                {
                    result = result >> 1;
                }
                bit >>= 2;
            }

            if (i == 0)
            {
                // Then process it again to get the lowest 16 bits.
                if (num > (1UL << (NUM_BITS / 2)) - 1)
                {
                    // The remainder 'num' is too large to be shifted left
                    // by 32, so we have to add 1 to result manually and
                    // adjust 'num' accordingly.
                    // num = a - (result + 0.5)^2
                    //       = num + result^2 - (result + 0.5)^2
                    //       = num - result - 0.5
                    num -= result;
                    num = (num << (NUM_BITS / 2)) - 0x80000000UL;
                    result = (result << (NUM_BITS / 2)) + 0x80000000UL;
                }
                else
                {
                    num <<= (NUM_BITS / 2);
                    result <<= (NUM_BITS / 2);
                }

                bit = 1UL << (NUM_BITS / 2 - 2);
            }
        }
        // Finally, if next bit would have been 1, round the result upwards.
        if (num > result)
        {
            ++result;
        }
        return result;
    }
    [BurstCompile(DisableDirectCall = true, OptimizeFor = OptimizeFor.Performance)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long ClampSinValue(long angle, out bool flipHorizontal, out bool flipVertical)
    {
        // Clamp value to 0 - 2*PI using modulo; this is very slow but there's no better way AFAIK
        var clamped2Pi = angle % PI_TIMES_2;
        if (angle < 0)
        {
            clamped2Pi += PI_TIMES_2;
        }

        // The LUT contains values for 0 - PiOver2; every other value must be obtained by
        // vertical or horizontal mirroring
        flipVertical = clamped2Pi >= PI;
        // obtain (angle % PI) from (angle % 2PI) - much faster than doing another modulo
        var clampedPi = clamped2Pi;
        while (clampedPi >= PI)
        {
            clampedPi -= PI;
        }
        flipHorizontal = clampedPi >= PI_OVER_2;
        // obtain (angle % PI_OVER_2) from (angle % PI) - much faster than doing another modulo
        var clampedPiOver2 = clampedPi;
        if (clampedPiOver2 >= PI_OVER_2)
        {
            clampedPiOver2 -= PI_OVER_2;
        }
        return clampedPiOver2;
    }
    [BurstCompile(DisableDirectCall = true, OptimizeFor = OptimizeFor.Performance)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long SinLutCalc(in long x) {
        bool flipHorizontal, flipVertical;
        var clampedL = MathBurstedFix.ClampSinValue(x, out flipHorizontal, out flipVertical);

        // Here we use the fact that the SinLut table has a number of entries
        // equal to (PI_OVER_2 >> 15) to use the angle to index directly into it
        var rawIndex = (uint)(clampedL >> 15);
        if (rawIndex >= LUT_SIZE)
        {
            rawIndex = LUT_SIZE - 1;
        }
        var nearestValue = SinLut[flipHorizontal ?
            SinLut.Length - 1 - (int)rawIndex :
            (int)rawIndex];
        return flipVertical ? -nearestValue : nearestValue;
    }
    [BurstCompile(DisableDirectCall = true, OptimizeFor = OptimizeFor.Performance)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long DistanceSquared(in long x,in long y) {
        return Multiply(x, x)+Multiply(y,y);
    }
    [BurstCompile(DisableDirectCall = true, OptimizeFor = OptimizeFor.Performance)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long Distance(in long x, in long y)
    {
        long res = DistanceSquared(x, y);
        res = (long)Sqrt(ref res);
        return res;
    }
    [BurstCompile(DisableDirectCall = true, OptimizeFor = OptimizeFor.Performance)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long DistanceSquared3D(in long x, in long y, in long z)
    {
        return Multiply(x, x) + Multiply(y, y) + Multiply(z, z);
    }
    [BurstCompile(DisableDirectCall = true, OptimizeFor = OptimizeFor.Performance)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long Distance3D(in long x, in long y, in long z)
    {
        long res = DistanceSquared3D(x, y, z);
        res = (long)Sqrt(ref res);
        return res;
    }
}
