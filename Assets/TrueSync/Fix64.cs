using System;
using System.IO;
using UnityEngine;
using Unity.Burst;
using System.Runtime.CompilerServices;
using MessagePack;


#if UNITY_EDITOR
using UnityEditor;
#endif
namespace TrueSync {
#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(FP))]
    public class FPDrawer : PropertyDrawer
    {

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            EditorGUI.indentLevel = 0;

            Rect xRect = new Rect(position.x, position.y, position.width, position.height);

            EditorGUIUtility.labelWidth = 14f;

            // 获取原始值
            long origX = property.FindPropertyRelative("_serializedValue").longValue;
            double tmp = (float)FP.FromRaw(origX);
            // 显示原始值，但乘以因子
            EditorGUI.BeginChangeCheck();
            double doubleF = EditorGUI.DoubleField(xRect, "", tmp);
            double x = Math.Pow(2, 32);
            origX = (long)(doubleF * x);
            if (EditorGUI.EndChangeCheck())
            {
                // 存储实际值
                property.FindPropertyRelative("_serializedValue").longValue = origX;
                var val = property.FindPropertyRelative("_savedFloat");
                if (val != null)
                {
                    val.floatValue = (float)doubleF;
                }
            }

            EditorGUI.EndProperty();
        }
    }
#endif
    /// <summary>
    /// Represents a Q31.32 fixed-point number.
    /// </summary>
    [Serializable]
    [MessagePackObject]
    public partial struct FP : IEquatable<FP>, IComparable<FP>
    {

        [SerializeField]
        [Key(0)]public long _serializedValue;

        public const long MAX_VALUE = long.MaxValue;
		public const long MIN_VALUE = long.MinValue;
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

        // Precision of this type is 2^-32, that is 2,3283064365386962890625E-10
        public static readonly decimal Precision = (decimal)(new FP(1L));//0.00000000023283064365386962890625m;
        public static readonly FP MaxValue = new FP(MAX_VALUE-1);
        public static readonly FP MinValue = new FP(MIN_VALUE+2);
        public static readonly FP One = new FP(ONE);
		public static readonly FP Ten = new FP(TEN);
        public static readonly FP Half = new FP(HALF);

        public static readonly FP Zero = new FP();
        public static readonly FP PositiveInfinity = new FP(MAX_VALUE);
        public static readonly FP NegativeInfinity = new FP(MIN_VALUE+1);
        public static readonly FP NaN = new FP(MIN_VALUE);

        public static readonly FP EN1 = FP.One / 10;
        public static readonly FP EN2 = FP.One / 100;
        public static readonly FP EN3 = FP.One / 1000;
        public static readonly FP EN4 = FP.One / 10000;
        public static readonly FP EN5 = FP.One / 100000;
        public static readonly FP EN6 = FP.One / 1000000;
        public static readonly FP EN7 = FP.One / 10000000;
        public static readonly FP EN8 = FP.One / 100000000;
        public static readonly FP Epsilon = FP.EN3;

        /// <summary>
        /// The value of Pi
        /// </summary>
        public static readonly FP Pi = new FP(PI);
        public static readonly FP PiOver2 = new FP(PI_OVER_2);
        public static readonly FP PiTimes2 = new FP(PI_TIMES_2);
        public static readonly FP PiInv = (FP)0.3183098861837906715377675267M;
        public static readonly FP PiOver2Inv = (FP)0.6366197723675813430755350535M;

        public static readonly FP Deg2Rad = Pi / new FP(180);

        public static readonly FP Rad2Deg = new FP(180) / Pi;

		public static readonly FP LutInterval = (FP)(LUT_SIZE - 1) / PiOver2;

        public static readonly FP Log2Max = new FP(LOG2MAX);
        public static readonly FP Log2Min = new FP(LOG2MIN);
        public static readonly FP Ln2 = new FP(LN2);

        /// <summary>
        /// Returns a number indicating the sign of a Fix64 number.
        /// Returns 1 if the value is positive, 0 if is 0, and -1 if it is negative.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Sign(FP value) {
            return
                value._serializedValue < 0 ? -1 :
                value._serializedValue > 0 ? 1 :
                0;
        }


        /// <summary>
        /// Returns the absolute value of a Fix64 number.
        /// Note: Abs(Fix64.MinValue) == Fix64.MaxValue.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Abs(FP value) {
            if (value._serializedValue == MIN_VALUE) {
                return MaxValue;
            }

            // branchless implementation, see http://www.strchr.com/optimized_abs_function
            var mask = value._serializedValue >> 63;
            FP result;
            result._serializedValue = (value._serializedValue + mask) ^ mask;
            return result;
            //return new FP((value._serializedValue + mask) ^ mask);
        }

        /// <summary>
        /// Returns the absolute value of a Fix64 number.
        /// FastAbs(Fix64.MinValue) is undefined.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP FastAbs(FP value) {
            // branchless implementation, see http://www.strchr.com/optimized_abs_function
            var mask = value._serializedValue >> 63;
            FP result;
            result._serializedValue = (value._serializedValue + mask) ^ mask;
            return result;
            //return new FP((value._serializedValue + mask) ^ mask);
        }


        /// <summary>
        /// Returns the largest integer less than or equal to the specified number.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Floor(FP value) {
            // Just zero out the fractional part
            FP result;
            result._serializedValue = (long)((ulong)value._serializedValue & 0xFFFFFFFF00000000);
            return result;
            //return new FP((long)((ulong)value._serializedValue & 0xFFFFFFFF00000000));
        }

        /// <summary>
        /// Returns the smallest integral value that is greater than or equal to the specified number.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Ceiling(FP value) {
            var hasFractionalPart = (value._serializedValue & 0x00000000FFFFFFFF) != 0;
            return hasFractionalPart ? Floor(value) + One : value;
        }

        /// <summary>
        /// Rounds a value to the nearest integral value.
        /// If the value is halfway between an even and an uneven value, returns the even value.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Round(FP value) {
            var fractionalPart = value._serializedValue & 0x00000000FFFFFFFF;
            var integralPart = Floor(value);
            if (fractionalPart < 0x80000000) {
                return integralPart;
            }
            if (fractionalPart > 0x80000000) {
                return integralPart + One;
            }
            // if number is halfway between two values, round to the nearest even number
            // this is the method used by System.Math.Round().
            return (integralPart._serializedValue & ONE) == 0
                       ? integralPart
                       : integralPart + One;
        }

        /// <summary>
        /// Adds x and y. Performs saturating addition, i.e. in case of overflow, 
        /// rounds to MinValue or MaxValue depending on sign of operands.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP operator +(FP x, FP y) {
            FP result;
            result._serializedValue = x._serializedValue + y._serializedValue;
            return result;
            //return new FP(x._serializedValue + y._serializedValue);
        }

        /// <summary>
        /// Adds x and y performing overflow checking. Should be inlined by the CLR.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP OverflowAdd(FP x, FP y) {
            var xl = x._serializedValue;
            var yl = y._serializedValue;
            var sum = xl + yl;
            // if signs of operands are equal and signs of sum and x are different
            if (((~(xl ^ yl) & (xl ^ sum)) & MIN_VALUE) != 0) {
                sum = xl > 0 ? MAX_VALUE : MIN_VALUE;
            }
            FP result;
            result._serializedValue = sum;
            return result;
            //return new FP(sum);
        }

        /// <summary>
        /// Adds x and y witout performing overflow checking. Should be inlined by the CLR.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP FastAdd(FP x, FP y) {
            FP result;
            result._serializedValue = x._serializedValue + y._serializedValue;
            return result;
            //return new FP(x._serializedValue + y._serializedValue);
        }

        /// <summary>
        /// Subtracts y from x. Performs saturating substraction, i.e. in case of overflow, 
        /// rounds to MinValue or MaxValue depending on sign of operands.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP operator -(FP x, FP y) {
            FP result;
            result._serializedValue = x._serializedValue - y._serializedValue;
            return result;
            //return new FP(x._serializedValue - y._serializedValue);
        }

        /// <summary>
        /// Subtracts y from x witout performing overflow checking. Should be inlined by the CLR.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP OverflowSub(FP x, FP y) {
            var xl = x._serializedValue;
            var yl = y._serializedValue;
            var diff = xl - yl;
            // if signs of operands are different and signs of sum and x are different
            if ((((xl ^ yl) & (xl ^ diff)) & MIN_VALUE) != 0) {
                diff = xl < 0 ? MIN_VALUE : MAX_VALUE;
            }
            FP result;
            result._serializedValue = diff;
            return result;
            //return new FP(diff);
        }

        /// <summary>
        /// Subtracts y from x witout performing overflow checking. Should be inlined by the CLR.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP FastSub(FP x, FP y) {
            return new FP(x._serializedValue - y._serializedValue);
        }

        static long AddOverflowHelper(long x, long y, ref bool overflow) {
            var sum = x + y;
            // x + y overflows if sign(x) ^ sign(y) != sign(sum)
            overflow |= ((x ^ y ^ sum) & MIN_VALUE) != 0;
            return sum;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP operator *(FP x, FP y) {
            var xl = x._serializedValue;
            var yl = y._serializedValue;

            
            FP result;// = default(FP);
            result._serializedValue = MathBurstedFix.Multiply(xl,yl);
            return result;
        }

        /// <summary>
        /// Performs multiplication without checking for overflow.
        /// Useful for performance-critical code where the values are guaranteed not to cause overflow
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP OverflowMul(FP x, FP y) {
            var xl = x._serializedValue;
            var yl = y._serializedValue;

            FP result;
            result._serializedValue = MathBurstedFix.OverflowMultiply(xl, yl);
            return result;
            //return new FP(sum);
        }

        /// <summary>
        /// Performs multiplication without checking for overflow.
        /// Useful for performance-critical code where the values are guaranteed not to cause overflow
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP FastMul(FP x, FP y) {
            var xl = x._serializedValue;
            var yl = y._serializedValue;

			FP result;// = default(FP);
			result._serializedValue = MathBurstedFix.Multiply(xl, yl);
			return result;
            //return new FP(sum);
        }

        //[MethodImplAttribute(MethodImplOptions.AggressiveInlining)] 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CountLeadingZeroes(ulong x) {
            int result = 0;
            while ((x & 0xF000000000000000) == 0) { result += 4; x <<= 4; }
            while ((x & 0x8000000000000000) == 0) { result += 1; x <<= 1; }
            return result;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP operator /(in FP x,in FP y) {
            var xl = x._serializedValue;
            var yl = y._serializedValue;
            return new FP(MathBurstedFix.Division(xl,yl));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP operator %(FP x, FP y) {
            FP result;
            result._serializedValue = x._serializedValue == MIN_VALUE & y._serializedValue == -1 ?
                0 :
                x._serializedValue % y._serializedValue;
            return result;
            //return new FP(
            //    x._serializedValue == MIN_VALUE & y._serializedValue == -1 ?
            //    0 :
            //    x._serializedValue % y._serializedValue);
        }

        /// <summary>
        /// Performs modulo as fast as possible; throws if x == MinValue and y == -1.
        /// Use the operator (%) for a more reliable but slower modulo.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP FastMod(FP x, FP y) {
            FP result;
            result._serializedValue = x._serializedValue % y._serializedValue;
            return result;
            //return new FP(x._serializedValue % y._serializedValue);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP operator -(FP x) {
            return x._serializedValue == MIN_VALUE ? MaxValue : new FP(-x._serializedValue);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(FP x, FP y) {
            return x._serializedValue == y._serializedValue;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(FP x, FP y) {
            return x._serializedValue != y._serializedValue;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >(FP x, FP y) {
            return x._serializedValue > y._serializedValue;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <(FP x, FP y) {
            return x._serializedValue < y._serializedValue;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >=(FP x, FP y) {
            return x._serializedValue >= y._serializedValue;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <=(FP x, FP y) {
            return x._serializedValue <= y._serializedValue;
        }


        /// <summary>
        /// Returns the square root of a specified number.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// The argument was negative.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Sqrt(FP x) {
            var xl = x._serializedValue;
            FP r;
            r._serializedValue = (long)MathBurstedFix.Sqrt(ref xl);
            return r;
            //return new FP((long)result);
        }

        /// <summary>
        /// Returns the Sine of x.
        /// This function has about 9 decimals of accuracy for small values of x.
        /// It may lose accuracy as the value of x grows.
        /// Performance: about 25% slower than Math.Sin() in x64, and 200% slower in x86.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Sin(FP x) {
            bool flipHorizontal, flipVertical;
            var clampedL = MathBurstedFix.ClampSinValue(x._serializedValue, out flipHorizontal, out flipVertical);
            var clamped = new FP(clampedL);

            // Find the two closest values in the LUT and perform linear interpolation
            // This is what kills the performance of this function on x86 - x64 is fine though
            var rawIndex = FastMul(clamped, LutInterval);
            var roundedIndex = Round(rawIndex);
            var indexError = 0;//FastSub(rawIndex, roundedIndex);

            var nearestValue = new FP(SinLut[flipHorizontal ?
                SinLut.Length - 1 - (int)roundedIndex :
                (int)roundedIndex]);
            var secondNearestValue = new FP(SinLut[flipHorizontal ?
                SinLut.Length - 1 - (int)roundedIndex - Sign(indexError) :
                (int)roundedIndex + Sign(indexError)]);

            var delta = FastMul(indexError, FastAbs(FastSub(nearestValue, secondNearestValue)))._serializedValue;
            var interpolatedValue = nearestValue._serializedValue + (flipHorizontal ? -delta : delta);
            var finalValue = flipVertical ? -interpolatedValue : interpolatedValue;

            //FP a2 = new FP(finalValue);
            FP a2;
            a2._serializedValue = finalValue;
            return a2;
        }

        /// <summary>
        /// Returns a rough approximation of the Sine of x.
        /// This is at least 3 times faster than Sin() on x86 and slightly faster than Math.Sin(),
        /// however its accuracy is limited to 4-5 decimals, for small enough values of x.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP FastSin(FP x) {
            FP result;
            result._serializedValue = MathBurstedFix.SinLutCalc(x._serializedValue);
            return result;
            //return new FP(flipVertical ? -nearestValue : nearestValue);
        }



        //[MethodImplAttribute(MethodImplOptions.AggressiveInlining)] 


        /// <summary>
        /// Returns the cosine of x.
        /// See Sin() for more details.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Cos(FP x) {
            var xl = x._serializedValue;
            var rawAngle = xl + (xl > 0 ? -PI - PI_OVER_2 : PI_OVER_2);
            FP a2 = Sin(new FP(rawAngle));
            return a2;
        }

        /// <summary>
        /// Returns a rough approximation of the cosine of x.
        /// See FastSin for more details.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP FastCos(FP x) {
            var xl = x._serializedValue;
            var rawAngle = xl + (xl > 0 ? -PI - PI_OVER_2 : PI_OVER_2);
            return FastSin(new FP(rawAngle));
        }

        /// <summary>
        /// Returns the tangent of x.
        /// </summary>
        /// <remarks>
        /// This function is not well-tested. It may be wildly inaccurate.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Tan(FP x) {
            var clampedPi = x._serializedValue % PI;
            var flip = false;
            if (clampedPi < 0) {
                clampedPi = -clampedPi;
                flip = true;
            }
            if (clampedPi > PI_OVER_2) {
                flip = !flip;
                clampedPi = PI_OVER_2 - (clampedPi - PI_OVER_2);
            }

            var clamped = new FP(clampedPi);

            // Find the two closest values in the LUT and perform linear interpolation
            var rawIndex = FastMul(clamped, LutInterval);
            var roundedIndex = Round(rawIndex);
            var indexError = FastSub(rawIndex, roundedIndex);

            var nearestValue = new FP(TanLut[(int)roundedIndex]);
            var secondNearestValue = new FP(TanLut[(int)roundedIndex + Sign(indexError)]);

            var delta = FastMul(indexError, FastAbs(FastSub(nearestValue, secondNearestValue)))._serializedValue;
            var interpolatedValue = nearestValue._serializedValue + delta;
            var finalValue = flip ? -interpolatedValue : interpolatedValue;
            FP a2 = new FP(finalValue);
            return a2;
        }

        /// <summary>
        /// Returns the arctan of of the specified number, calculated using Euler series
        /// This function has at least 7 decimals of accuracy.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Atan(FP z)
        {
            if (z.RawValue == 0) return Zero;

            // Force positive values for argument
            // Atan(-z) = -Atan(z).
            var neg = z.RawValue < 0;
            if (neg)
            {
                z = -z;
            }

            FP result;
            var two = (FP)2;
            var three = (FP)3;

            bool invert = z > One;
            if (invert) z = One / z;

            result = One;
            var term = One;

            var zSq = z * z;
            var zSq2 = zSq * two;
            var zSqPlusOne = zSq + One;
            var zSq12 = zSqPlusOne * two;
            var dividend = zSq2;
            var divisor = zSqPlusOne * three;

            for (var i = 2; i < 30; ++i)
            {
                term *= dividend / divisor;
                result += term;

                dividend += zSq2;
                divisor += zSq12;

                if (term.RawValue == 0) break;
            }

            result = result * z / zSqPlusOne;

            if (invert)
            {
                result = PiOver2 - result;
            }

            if (neg)
            {
                result = -result;
            }
            return result;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Atan2(FP y, FP x) {
            var yl = y._serializedValue;
            var xl = x._serializedValue;
            if (xl == 0) {
                if (yl > 0) {
                    return PiOver2;
                }
                if (yl == 0) {
                    return Zero;
                }
                return -PiOver2;
            }
            FP atan;
            var z = y / x;

            FP sm = FP.EN2 * 28;
            // Deal with overflow
            if (One + sm * z * z == MaxValue) {
                return y < Zero ? -PiOver2 : PiOver2;
            }

            if (Abs(z) < One) {
                atan = z / (One + sm * z * z);
                if (xl < 0) {
                    if (yl < 0) {
                        return atan - Pi;
                    }
                    return atan + Pi;
                }
            } else {
                atan = PiOver2 - z / (z * z + sm);
                if (yl < 0) {
                    return atan - Pi;
                }
            }
            return atan;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Asin(FP value) {
            return FastSub(PiOver2, Acos(value));
        }

        /// <summary>
        /// Returns the arccos of of the specified number, calculated using Atan and Sqrt
        /// This function has at least 7 decimals of accuracy.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Acos(FP x)
        {
            if (x < -One || x > One)
            {
                throw new ArgumentOutOfRangeException("Must between -FP.One and FP.One", "x");
            }

            if (x.RawValue == 0) return PiOver2;

            var result = Atan(Sqrt(One - x * x) / x);
            return x.RawValue < 0 ? result + Pi : result;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator FP(long value) {
            FP result;
            result._serializedValue = value * ONE;
            return result;
            //return new FP(value * ONE);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator long(FP value) {
            return value._serializedValue >> FRACTIONAL_PLACES;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator FP(float value) {
            FP result;
            result._serializedValue = (long)(value * ONE);
            return result;
            //return new FP((long)(value * ONE));
        }
        /*
        public static explicit operator float(FP value) {
            return (float)value._serializedValue / ONE;
        }*/
        /*DEBUG 目前为了能顺利运行增加隐式转换，但是要在全部修改为定点数时请删掉并替换成显式转换*/
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator float(FP value)
        {
            return (float)value._serializedValue / ONE;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator FP(double value) {
            FP result;
            result._serializedValue = (long)(value * ONE);
            return result;
            //return new FP((long)(value * ONE));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator double(FP value) {
            return (double)value._serializedValue / ONE;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator FP(decimal value) {
            FP result;
            result._serializedValue = (long)(value * ONE);
            return result;
            //return new FP((long)(value * ONE));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator FP(int value) {
            FP result;
            result._serializedValue = value * ONE;
            return result;
            //return new FP(value * ONE);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator decimal(FP value) {
            return (decimal)value._serializedValue / ONE;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float AsFloat() {
            return (float) this;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int AsInt() {
            return (int) this;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long AsLong() {
            return (long)this;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double AsDouble() {
            return (double)this;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal AsDecimal() {
            return (decimal)this;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ToFloat(FP value) {
            return (float)value;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ToInt(FP value) {
            return (int)value;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP FromFloat(float value) {
            return (FP)value;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInfinity(FP value) {
            return value == NegativeInfinity || value == PositiveInfinity;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNaN(FP value) {
            return value == NaN;
        }

        public override bool Equals(object obj) {
            return obj is FP && ((FP)obj)._serializedValue == _serializedValue;
        }

        public override int GetHashCode() {
            return _serializedValue.GetHashCode();
        }

        public bool Equals(FP other) {
            return _serializedValue == other._serializedValue;
        }

        public int CompareTo(FP other) {
            return _serializedValue.CompareTo(other._serializedValue);
        }

        public override string ToString() {
            return ((float)this).ToString();
        }

        public string ToString(IFormatProvider provider) {
            return ((float)this).ToString(provider);
        }
        public string ToString(string format) {
            return ((float)this).ToString(format);
        }

        public static FP FromRaw(long rawValue) {
            return new FP(rawValue);
        }

        internal static void GenerateAcosLut() {
            using (var writer = new StreamWriter("Fix64AcosLut.cs")) {
                writer.Write(
@"namespace TrueSync {
    partial struct FP {
        public static readonly long[] AcosLut = new[] {");
                int lineCounter = 0;
                for (int i = 0; i < LUT_SIZE; ++i) {
                    var angle = i / ((float)(LUT_SIZE - 1));
                    if (lineCounter++ % 8 == 0) {
                        writer.WriteLine();
                        writer.Write("            ");
                    }
                    var acos = Math.Acos(angle);
                    var rawValue = ((FP)acos)._serializedValue;
                    writer.Write(string.Format("0x{0:X}L, ", rawValue));
                }
                writer.Write(
@"
        };
    }
}");
            }
        }

        internal static void GenerateSinLut() {
            using (var writer = new StreamWriter("Fix64SinLut.cs")) {
                writer.Write(
@"namespace FixMath.NET {
    partial struct Fix64 {
        public static readonly long[] SinLut = new[] {");
                int lineCounter = 0;
                for (int i = 0; i < LUT_SIZE; ++i) {
                    var angle = i * Math.PI * 0.5 / (LUT_SIZE - 1);
                    if (lineCounter++ % 8 == 0) {
                        writer.WriteLine();
                        writer.Write("            ");
                    }
                    var sin = Math.Sin(angle);
                    var rawValue = ((FP)sin)._serializedValue;
                    writer.Write(string.Format("0x{0:X}L, ", rawValue));
                }
                writer.Write(
@"
        };
    }
}");
            }
        }

        internal static void GenerateTanLut() {
            using (var writer = new StreamWriter("Fix64TanLut.cs")) {
                writer.Write(
@"namespace FixMath.NET {
    partial struct Fix64 {
        public static readonly long[] TanLut = new[] {");
                int lineCounter = 0;
                for (int i = 0; i < LUT_SIZE; ++i) {
                    var angle = i * Math.PI * 0.5 / (LUT_SIZE - 1);
                    if (lineCounter++ % 8 == 0) {
                        writer.WriteLine();
                        writer.Write("            ");
                    }
                    var tan = Math.Tan(angle);
                    if (tan > (double)MaxValue || tan < 0.0) {
                        tan = (double)MaxValue;
                    }
                    var rawValue = (((decimal)tan > (decimal)MaxValue || tan < 0.0) ? MaxValue : (FP)tan)._serializedValue;
                    writer.Write(string.Format("0x{0:X}L, ", rawValue));
                }
                writer.Write(
@"
        };
    }
}");
            }
        }

        /// <summary>
        /// The underlying integer representation
        /// </summary>
        [IgnoreMember]public long RawValue { get { return _serializedValue; }  set { _serializedValue = value; }}

        /// <summary>
        /// This is the constructor from raw value; it can only be used interally.
        /// </summary>
        /// <param name="rawValue"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        FP(long rawValue) {
            _serializedValue = rawValue;
        }

        public FP(int value) {
            _serializedValue = value * ONE;
        }
    }
}