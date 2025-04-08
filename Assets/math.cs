using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using TrueSync;

namespace RandomBenchmark
{
    public class XorShiftRandom
    {
        private const double NormalizationFactor = 1.0 / int.MaxValue;

        private readonly ulong[] _state;
        private ulong _seed;
        private bool _takeMsb = true;
        private ulong _currentPartialResult;

        public XorShiftRandom(int seed)
        {
            _seed = (ulong)seed;
            _state = new[] { SplitMix64(), SplitMix64() };
        }

        public virtual int Next()
        {
            var sample = InternalSample() & int.MaxValue;

            return sample == int.MaxValue
                ? --sample
                : sample;
        }

        public virtual int Next(int minValue, int maxValue)
        {
            if (minValue > maxValue)
                throw new ArgumentOutOfRangeException(nameof(minValue));

            var range = (long)maxValue - minValue;

            return minValue + (int)(range * NextDouble());
        }

        public virtual int Next(int maxValue)
        {
            if (maxValue < 0)
                throw new ArgumentOutOfRangeException(nameof(maxValue));

            return (int)(NextDouble() * maxValue);
        }

        public virtual double NextDouble()
        {
            var sample = Next();
            return sample * NormalizationFactor;
        }

        public virtual void NextBytes(byte[] buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            var tmp = BitConverter.GetBytes(InternalSample());
            short index = 0;
            for (var i = 0; i < buffer.Length; ++i)
            {
                if (index == 4)
                {
                    index = 0;
                    tmp = BitConverter.GetBytes(InternalSample());
                }

                buffer[i] = tmp[index++];
            }
        }

        private int InternalSample()
        {
            int sample;

            if (_takeMsb)
            {
                _currentPartialResult = XorShift128Plus();
                sample = unchecked((int)(_currentPartialResult >> 32));
            }
            else
            {
                sample = unchecked((int)_currentPartialResult);
            }

            _takeMsb = !_takeMsb;

            return sample;
        }

        private ulong SplitMix64()
        {
            var z = unchecked(_seed += 0x9E3779B97F4A7C15);
            z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9);
            z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EB);
            return z ^ (z >> 31);
        }

        private ulong XorShift128Plus()
        {
            var s1 = _state[0];
            var s0 = _state[1];
            var result = s0 + s1;
            _state[0] = s0;
            s1 ^= s1 << 23;
            _state[1] = s1 ^ s0 ^ (s1 >> 18) ^ (s0 >> 5);
            return result;
        }
    }
}
public class KalmanFilterVector3
{

    //-----------------------------------------------------------------------------------------
    // Constants:
    //-----------------------------------------------------------------------------------------

    public const float DEFAULT_Q = 0.000001f;
    public const float DEFAULT_R = 0.01f;

    public const float DEFAULT_P = 1;

    //-----------------------------------------------------------------------------------------
    // Private Fields:
    //-----------------------------------------------------------------------------------------

    private float q;
    private float r;
    private float p = DEFAULT_P;
    private Vector3 x;
    private float k;

    //-----------------------------------------------------------------------------------------
    // Constructors:
    //-----------------------------------------------------------------------------------------

    // N.B. passing in DEFAULT_Q is necessary, even though we have the same value (as an optional parameter), because this
    // defines a parameterless constructor, allowing us to be new()'d in generics contexts.
    public KalmanFilterVector3() : this(DEFAULT_Q) { }
    public KalmanFilterVector3 InitPosition(Vector3 initalPosition) {
        x = initalPosition;
        return this;
    }

    public KalmanFilterVector3(float aQ = DEFAULT_Q, float aR = DEFAULT_R)
    {
        q = aQ;
        r = aR;
    }

    //-----------------------------------------------------------------------------------------
    // Public Methods:
    //-----------------------------------------------------------------------------------------

    public Vector3 Update(Vector3 measurement, float? newQ = null, float? newR = null)
    {

        // update values if supplied.
        if (newQ != null && q != newQ)
        {
            q = (float)newQ;
        }
        if (newR != null && r != newR)
        {
            r = (float)newR;
        }

        // update measurement.
        {
            k = (p + q) / (p + q + r);
            p = r * (p + q) / (r + p + q);
        }

        // filter result back into calculation.
        Vector3 result = x + (measurement - x) * k;
        x = result;
        return result;
    }

    public Vector3 Update(List<Vector3> measurements, bool areMeasurementsNewestFirst = false, float? newQ = null, float? newR = null)
    {

        Vector3 result = Vector3.zero;
        int i = (areMeasurementsNewestFirst) ? measurements.Count - 1 : 0;

        while (i < measurements.Count && i >= 0)
        {

            // decrement or increment the counter.
            if (areMeasurementsNewestFirst)
            {
                --i;
            }
            else
            {
                ++i;
            }

            result = Update(measurements[i], newQ, newR);
        }

        return result;
    }

    public void Reset(Vector3? InitPosition=null)
    {
        p = 1;
        x = InitPosition??Vector3.zero;
        k = 0;
    }
}
public static class math
{
    public static readonly FP sqrt2 = FP.Sqrt(2); 
    public static bool checkOut(TSVector2 border, TSVector pos, TSVector2 size)
    {
        FP max = TSMath.Max(size.x,size.y);
        border *= 0.5;
        size = TSVector2.one * (max * sqrt2 * 0.5);
        if (pos.x - size.x > border.x || pos.x + size.x < -border.x || pos.y - size.y  > border.y || pos.y + size.y < -border.y)
        {
            return true;
        }
        return false;
    }
    /// <summary>
    /// 圆弧接起来的曲线
    /// </summary>
    /// <param name="time">时间</param>
    /// <param name="r">半径</param>
    /// <param name="w">角速度</param>
    /// <param name="theta">每个圆弧的弧度</param>
    /// <returns></returns>
    public static Vector2 curve_joined_by_arc(float time,float r,float w,float theta) {
        float theta2 = Mathf.PI / 2f - (2 * Mathf.PI - theta) / 2f;
        float theta3 = Mathf.PI / 2f + (2 * Mathf.PI - theta) / 2f;
        //目前处于的弧度
        float currentArc = ((w * time) % theta + theta3) * Mathf.Pow((-1) , Mathf.Floor((w*time) / theta));
        Vector2 circleCenter = new Vector2(
            Mathf.Cos(theta2)*(2*Mathf.Floor((w*time) / theta + 1) -1)*r,
            Mathf.Sin(theta2)*Mathf.Pow((-1) , Mathf.Floor((w*time) / theta + 1))*r
        );
        return new Vector2(Mathf.Cos(currentArc), Mathf.Sin(currentArc))*r+circleCenter;
    }
    /// <summary>
    ///Catmull-Rom线，会通过相关的控制点
    /// 根据起点，n个控制点，终点 计算Cspline曲线插值（首尾为必要的点，其余为控制点，所以至少有4个点）
    /// </summary>
    /// <param name="points">起点，n-1个控制点，终点</param>
    /// <param name="t">当前插值位置0~1 ，0为起点，1为终点</param>
    /// <returns></returns>
    public static TSVector Interp(TSVector[] pts, FP t)
    {
        t = TSMath.Clamp(t, 0.0, 2.0);
        int numSections = pts.Length - 3;
        int currPt = (int)TSMath.Min(TSMath.Floor(t * numSections), numSections - 1);
        FP u = t * numSections - currPt;
        TSVector a = pts[currPt];
        TSVector b = pts[currPt + 1];
        TSVector c = pts[currPt + 2];
        TSVector d = pts[currPt + 3];

        return 0.5 * (
        ((TSVector.zero-a) + 3 * b - 3 * c + d) * (u * u * u)
        + (2 * a - 5 * b + 4 * c - d) * (u * u)
        + ((TSVector.zero - a) + c) * u
        + 2 * b
        );
    }
    /// <summary>
    /// n阶贝塞尔曲线插值计算函数
    /// 根据起点，n个控制点，终点 计算贝塞尔曲线插值
    /// </summary>
    /// <param name="points">起点，n-1个控制点，终点</param>
    /// <param name="t">当前插值位置0~1 ，0为起点，1为终点</param>
    /// <returns></returns>
    public static Vector3 bezier_interpolation_func(Vector3[] points, float t)
    {
        Vector3 PointF = new Vector3();
        if (t == 1)
        {
            return points[points.Length - 1];
        }
        int count = points.Length;
        float[] part = new float[count];
        float sum_x = 0, sum_y = 0;
        for (int i = 0; i < count; i++)
        {
            ulong tmp;
            int n_order = count - 1;    // 阶数
            tmp = calc_combination_number(n_order, i);
            sum_x += (float)(tmp * points[i].x * Math.Pow((1 - t), n_order - i) * Math.Pow(t, i));
            sum_y += (float)(tmp * points[i].y * Math.Pow((1 - t), n_order - i) * Math.Pow(t, i));
        }
        PointF.x = sum_x;
        PointF.y = sum_y;
        return PointF;
    }
    public static TSVector2 expandVectorOnTargetAngle(in TSVector2 vec,in TSVector2 scale,FP angle=default)
    {
        /*
         * (a cos^2(d) + b sin^2(d) | a sin(d) cos(d) - b sin(d) cos(d)
            a sin(d) cos(d) - b sin(d) cos(d) | a sin^2(d) + b cos^2(d))
         */
        angle*=TSMath.Deg2Rad;
        FP sin = FP.FastSin(angle);
        FP cos = FP.FastCos(angle);
        FP sin2 = sin * sin;
        FP cos2 = cos * cos;
        FP sincos = sin * cos;
        return new TSVector2(
            (scale.x*cos2+scale.y*sin2)*vec.x+(scale.x-scale.y)*sincos*vec.y,
            (scale.x-scale.y)*sincos*vec.x+(scale.x*sin2+scale.y*cos2)*vec.y
        );
    }
    /// <summary>
    /// 计算组合数公式
    /// </summary>
    /// <param name="n"></param>
    /// <param name="k"></param>
    /// <returns></returns>
    private static ulong calc_combination_number(int n, int k)
    {
        ulong[] result = new ulong[n + 1];
        for (int i = 1; i <= n; i++)
        {
            result[i] = 1;
            for (int j = i - 1; j >= 1; j--)
                result[j] += result[j - 1];
            result[0] = 1;
        }
        return result[k];
    }


    public static long MoveToward(long target, long current, long maxDistanceDelta)
    {
        long num = target - current;
        long power = num * num;
        if (power == 0 || (maxDistanceDelta >= 0 && power <= maxDistanceDelta * maxDistanceDelta))
        {
            return target;
        }
        long num5 = power;
        return current + num / num5 * maxDistanceDelta;
    }
    public static Vector2 UnitVector(float ang)
    {
        return new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad));
    }
    public static TSVector2 UnitVector(int ang)
    {
        return new TSVector2(FP.FastCos(ang * TSMath.Deg2Rad), FP.FastSin(ang * TSMath.Deg2Rad));
    }
    public static TSVector2 UnitVector(FP ang)
    {
        return new TSVector2(FP.FastCos(ang * TSMath.Deg2Rad), FP.FastSin(ang * TSMath.Deg2Rad));
    }
    public static Mesh CreateMesh(float radius, float innerradius, float angledegree, int segments)
    {
        //vertices(顶点):
        int vertices_count = segments * 2 + 2;              //因为vertices(顶点)的个数与triangles（索引三角形顶点数）必须匹配
        Vector3[] vertices = new Vector3[vertices_count];
        float angleRad = Mathf.Deg2Rad * angledegree;
        float angleCur = angleRad;
        float angledelta = angleRad / segments;
        for (int i = 0; i < vertices_count; i += 2)
        {
            float cosA = Mathf.Cos(angleCur);
            float sinA = Mathf.Sin(angleCur);
            vertices[i] = new Vector3(radius * cosA, radius * sinA, 0);
            vertices[i + 1] = new Vector3(innerradius * cosA, innerradius * sinA, 0);
            angleCur -= angledelta;
        }
        //triangles:
        int triangle_count = segments * 6;
        int[] triangles = new int[triangle_count];
        for (int i = 0, vi = 0; i < triangle_count; i += 6, vi += 2)
        {
            triangles[i] = vi;
            triangles[i + 1] = vi + 3;
            triangles[i + 2] = vi + 1;
            triangles[i + 3] = vi + 2;
            triangles[i + 4] = vi + 3;
            triangles[i + 5] = vi;
        }
        //uv:
        Vector2[] uvs = new Vector2[vertices_count];
        for (int i = 0; i < vertices_count; i+=2)
        {
            uvs[i] = new Vector2((float)i / vertices_count, 1);
            uvs[i+1] = new Vector2((float)i / vertices_count, 0);
        }
        //负载属性与mesh
        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        return mesh;
    }
    public static FP MapAngleTo180Range(FP angle)
    {
        // 使用取余运算符，首先要将负数角度转换为正数，以便正确取余
        // 然后根据取余结果决定是否减去360度，转换回原区间并考虑正负
        return (angle + 540) % 360 - 180;
    }

    public static bool CircleTriggerCircle(in TSVector point1, in FP r1, in TSVector point2, in FP r2, FP r1_width_height_ratio = default)
    {
        if (r1_width_height_ratio == 0)
        {
            r1_width_height_ratio = 1;
        }
        r1_width_height_ratio = (r1 * r1_width_height_ratio + r2) / (r1 + r2);//这个判定只适用于大椭圆与小圆判定，因为其实不是椭圆，只是近似为一个椭圆
        return (TSMath.FastQuadratic(TSMath.Abs((point2.x - point1.x) / r1_width_height_ratio)) + TSMath.FastQuadratic(TSMath.Abs(point2.y - point1.y))) <= TSMath.FastQuadratic(r1 + r2);
    }
}
namespace ZeroAs.DataStructure {

    public class MaxHeap<T> where T : IComparable<T>
    {
        public List<T> container = new List<T>();
        int cnt = 0;
        public int Count { get { return cnt; } }
        protected virtual bool cmp(T a, T b)
        {
            return a.CompareTo(b) > 0;
        }
        void swap(int a, int b)
        {
            T tmp = container[a]; container[a] = container[b]; container[b] = tmp;
        }
        void up()
        {
            int pos = cnt - 1;
            while (pos > 0)
            {
                int father = (pos - 1) >> 1;
                //pos>father
                if (cmp(container[pos], container[father]))
                {
                    swap(pos, father);
                    pos = father;
                }
                else
                {
                    break;
                }
            }
        }
        void down()
        {
            int pos = 0, leftChild;
            while ((leftChild = (pos << 1) + 1) < cnt)
            {
                int swapper = leftChild;
                if (leftChild + 1 < cnt && cmp(container[leftChild + 1], container[leftChild]))
                {
                    //right>left
                    swapper++;
                }
                //swapper > pos
                if (cmp(container[swapper], container[pos]))
                {
                    swap(pos, swapper);
                    pos = swapper;
                }
                else
                {
                    break;
                }
            }
        }
        public void Add(T item)
        {
            container.Add(item);
            cnt++;
            up();
        }
        public bool IsEmpty
        {
            get { return cnt == 0; }
        }
        public T Pop()
        {
            if (this.IsEmpty)
            {
                throw new InvalidOperationException("堆为空");
            }
            T res = container[0];
            --cnt;
            container[0] = container[cnt];
            container.RemoveAt(cnt);
            down();
            return res;
        }
        public T Peek()
        {
            if (this.IsEmpty)
            {
                throw new InvalidOperationException("堆为空");
            }
            return container[0];
        }
    }
}