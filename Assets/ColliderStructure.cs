using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Core.Algorithm;
using TrueSync;
using Unity.Burst;
using Unity.Collections;

namespace ZeroAs.DOTS.Colliders
{
    public enum ColliderType
    {
        Circle,
        Oval,
        Polygon,
        DoubleCircle,
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct ColliderStructure
    {
        public ColliderType colliderType;
        public TSVector2 center;
        #region 圆形，和双头圆形
            public FP radius;//圆形半径
        #endregion
        #region 椭圆形
            public TSVector2 Axis, SqrAxis;
            public FP b2Dividea2,rot;
        #endregion
        #region 多边形
            public int vertexStartIndex, vertexCount;
        #endregion
        #region 双头圆形
            public TSVector2 circleCenter1{
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => this.Axis;
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                set => this.Axis = value;
            }
            public TSVector2 circleCenter2{
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => this.SqrAxis;
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                set => this.SqrAxis = value;
            }
        #endregion
    }
    [BurstCompile(DisableDirectCall = true,OptimizeFor = OptimizeFor.Performance)]
    public struct GetFurthestPointExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile(DisableDirectCall = true,OptimizeFor = OptimizeFor.Performance)]
        public static void RotateRad(out TSVector2 result, in TSVector2 self, in FP rad)
        {
            FP cos = FP.FastCos(rad);
            FP sin = FP.FastSin(rad);
            result.x = self.x * cos - self.y * sin;
            result.y = self.x * sin + self.y * cos;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile(DisableDirectCall = true,OptimizeFor = OptimizeFor.Performance)]
        public static void Negate(ref FP val)
        {
            val._serializedValue=val._serializedValue == MathBurstedFix.MIN_VALUE ? MathBurstedFix.MaxValue : (-val._serializedValue);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile(DisableDirectCall = true,OptimizeFor = OptimizeFor.Performance)]
        public static void Negate(ref TSVector2 val)
        {
            Negate(ref val.x);
            Negate(ref val.y);
        }
        static NativeArray<TSVector2> points
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile(DisableDirectCall = true,OptimizeFor = OptimizeFor.Performance)]
        public static void Circle(out TSVector2 result,in TSVector2 center,in TSVector2 direction,in FP radius)
        {
            result = center + direction.normalized*radius;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile(DisableDirectCall = true,OptimizeFor = OptimizeFor.Performance)]
        public static void Oval(out TSVector2 result, in TSVector2 _direction,
            in TSVector2 centerPos,in FP rot,in TSVector2 Axis,in TSVector2 SqrAxis,in FP b2Dividea2)
        {
            //sin和cos还是很快的，因为是线性插值……有Lut
            FP neg = rot;
            Negate(ref neg);
            RotateRad(out var direction,in _direction,in neg);
            if (direction.x == 0)
            {
                RotateRad(out direction, TSMath.Sign(direction.y) * Axis.y * TSVector2.up, rot);
                result = direction + centerPos;
                return;
            }else if (direction.y == 0)
            {
                RotateRad(out direction, (TSMath.Sign(direction.x) * Axis.x * TSVector2.right), rot);
                result = direction + centerPos;
                return;
            }
            FP signX = TSMath.Sign(direction.x);
            FP k = direction.y / direction.x;//目标斜率
            FP a2 = SqrAxis.x;
            FP b2 = SqrAxis.y;
            FP ratio = FP.OverflowMul(k, b2Dividea2);
            FP denominator = FP.OverflowAdd(1, FP.OverflowMul(k, ratio));
            if (denominator._serializedValue >= MathBurstedFix.MaxValue || denominator._serializedValue <= MathBurstedFix.MinValue)
            {
                //Debug.Log("denominatorOverflow "+ direction + " "+ new TSVector2(0, TSMath.Sign(direction.y) * Axis.y).RotateRad(rot));
                RotateRad(out direction, new TSVector2(0, TSMath.Sign(direction.y) * Axis.y), rot);

                result = direction + centerPos;
                return;
            }
            FP value = 1.0/denominator;
        
            FP tarX = signX * (Axis.x*TSMath.Sqrt(value));
            FP tarY = FP.OverflowMul(tarX,ratio);
            //Debug.Log("ovalthings: "+tarX+" "+tarY+" "+k+" "+ratio);
            if (tarY._serializedValue >= MathBurstedFix.MaxValue||tarY._serializedValue<=MathBurstedFix.MinValue)
            {
                RotateRad(out direction, new TSVector2(0, TSMath.Sign(direction.y)*Axis.y), rot);

                result = direction + centerPos;
                return;
            }
            RotateRad(out direction, new TSVector2(tarX,tarY), rot);

            result = direction+centerPos;
            return;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile(DisableDirectCall = true,OptimizeFor = OptimizeFor.Performance)]
        public static void Polygon(out TSVector2 result, in TSVector2 _direction, in NativeArray<TSVector2> movedVertexs,int offset,int length)
        {
            TSVector2 direction = _direction.normalized;
            FP maxLen,len;
            maxLen._serializedValue = MathBurstedFix.MinValue;
            TSVector2 pos = movedVertexs[offset];
            int len_ = offset + length;
            for(int i = len_-1; i >= 0; --i)
            {
                if((len= TSVector2.Dot(direction, movedVertexs[i])) > maxLen)
                {
                    maxLen = len;
                    pos = movedVertexs[i];
                }
            }
            result= pos;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile(DisableDirectCall = true,OptimizeFor = OptimizeFor.Performance)]
        public static void DoubleCircle(out TSVector2 result,in TSVector2 direction,in TSVector2 centerCircle1,in TSVector2 centerCircle2,in FP r,in TSVector2 centerPos)
        {
            //d.normal*
            if (TSVector2.Dot(direction, centerCircle1-centerPos) > 0)
            {
                result= direction.normalized * r + centerCircle1;
            }
            else
            {
                result= direction.normalized * r + centerCircle2;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile(DisableDirectCall = true,OptimizeFor = OptimizeFor.Performance)]
        public static void GetFurthestPoint(out TSVector2 result, in ColliderStructure structure,in TSVector2 direction)
        {
            switch (structure.colliderType)
            {
                case ColliderType.Circle:
                    Circle(out result,structure.center,direction,structure.radius);
                    return;
                case ColliderType.Oval:
                    Oval(out result,direction,structure.center,structure.rot,structure.Axis,structure.SqrAxis,structure.b2Dividea2);
                    return;
                case ColliderType.Polygon:
                    Polygon(out result,direction,points,structure.vertexStartIndex,structure.vertexCount);
                    return;
                case ColliderType.DoubleCircle:
                    DoubleCircle(out result,direction,structure.circleCenter1,structure.circleCenter2,structure.radius,structure.center);
                    return;
                default:
                    throw new NotImplementedException();
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile(DisableDirectCall = true,OptimizeFor = OptimizeFor.Performance)]
        public static void SupportFunc(out TSVector2 result,in ColliderStructure structure1,in ColliderStructure structure2,in TSVector2 direciton) {
            GetFurthestPoint(out var resTmp,structure1,direciton);
            TSVector2 negateDirection = direciton;
            Negate(ref negateDirection);
            GetFurthestPoint(out var resTmp2, structure2, negateDirection);
            //Debug.Log("direction: "+direciton+" "+ shape1.GetFurthestPoint(direciton)+" "+shape2.GetFurthestPoint(-direciton));
            result=resTmp-resTmp2;
        }
    }
    [BurstCompile(DisableDirectCall = true,OptimizeFor = OptimizeFor.Performance)]
    public struct CollideExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile(DisableDirectCall = true,OptimizeFor = OptimizeFor.Performance)]
        public static void Abs(out FP result,in FP value) {
            if (value._serializedValue == MathBurstedFix.MIN_VALUE) {
                result._serializedValue= MathBurstedFix.MaxValue;
                return;
            }

            // branchless implementation, see http://www.strchr.com/optimized_abs_function
            var mask = value._serializedValue >> 63;
            result._serializedValue = (value._serializedValue + mask) ^ mask;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile(DisableDirectCall = true,OptimizeFor = OptimizeFor.Performance)]
        public static void TripleProduct2d(out TSVector2 result,in TSVector2 a,in TSVector2 b,in TSVector2 c) {
            FP sign = (a.x * b.y - a.y * b.x);
            FP cY = c.y;
            GetFurthestPointExtensions.Negate(ref cY);
            result = new TSVector2(cY, c.x) * sign;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile(DisableDirectCall = true,OptimizeFor = OptimizeFor.Performance)]
        public static bool GJK(in ColliderStructure shape1, in ColliderStructure shape2) {
            /*
         #两个形状s1,s2相交则返回True。所有的向量/点都是二维的，例如（[x,y]）
         #第一步：选择一个初始方向，这个初始方向可以是随机选择的，但通常来说是两个形状中心之间的向量，即：

         */
            TSVector2 tmpVec;
            TSVector2 direction = (shape2.center - shape1.center).normalized;
            //#第二步：找到支撑点，即第一个支撑点（即闵可夫斯基差的边上的点之一……）
            NativeArray<TSVector2> Simplex = new NativeArray<TSVector2>(3,Allocator.Temp);//单纯形数组，最多只能是3个
            GetFurthestPointExtensions.SupportFunc(out tmpVec,shape1, shape2, direction);
            Simplex[0] = tmpVec;
            int simplexLastInd = 1;
            int interateTimeMax = 100;//最大迭代次数
            //#第三步：找到第一个支撑点后，以第一个支撑点为起点指向原点O的方向为新方向d
            direction = tmpVec;//= -Simplex[0].normalized
            GetFurthestPointExtensions.Negate(ref direction);
            TSVector2.Normalize(direction,out direction);
            //#第四步：开始循环，找下一个支撑点
            while (interateTimeMax-- > 0)
            {
                GetFurthestPointExtensions.SupportFunc(out var A,shape1,shape2,direction);
                //因为A点是闵可夫斯基差形状在给定方向的最远点，如果那个点没有超过原点，就不想交
                //#当新的支撑点A没有包含原点，那我们就返回False，即两个形状没有相交
                if (TSVector2.Dot(A,direction)<0)
                {
                    Simplex.Dispose();
                    return false;
                }
                Simplex[simplexLastInd++] = A;
                //Debug.Log("input: "+A+shape1.GetType()+" "+shape2.GetType());
                //处理为线段的情况
                if (simplexLastInd == 2)
                {
                    //三维的处理方式
                    /*
                TSVector AB = Simplex[simplexLastInd-2] - Simplex[simplexLastInd - 1];
                TSVector AO = -Simplex[simplexLastInd-1];
                TSVector ABPrep = TSVector.Cross(TSVector.Cross(AB, AO),AB);//垂直于AB的那个点！
                */
                    //在2d里面可以这么简化
                    TSVector2 AB = Simplex[simplexLastInd - 2] - Simplex[simplexLastInd - 1];
                    TSVector2 AO = Simplex[simplexLastInd - 1];
                    GetFurthestPointExtensions.Negate(ref AO);//这里记得取反
                    TripleProduct2d(out var ABPrep,AB,AO,AB);
                    direction = ABPrep.normalized;
                    /*
                 * A是最新插入的点，B是第一次插入的点
                 当我们拥有两个点时，我们怎么选择新的方向？
                1.	构建向量：
                o	构建向量 𝐴𝑂（从点A到原点O），即 𝐴𝑂=𝑂−𝐴
                o	构建向量 𝐴𝐵（从点A到点B），即 𝐴𝐵=𝐵−𝐴
                2.	求解垂直向量：
                o	通过叉积 𝐴𝐵×𝐴𝑂，我们可以得到一个垂直于这两个向量的向量。这个向量垂直于 𝐴𝐵 和 𝐴𝑂 所在的平面，并且指向由右手定则决定的方向。
                3.	求解新的方向：
                o	为了得到新的方向 𝑑，我们需要一个向量，这个向量既垂直于 𝐴𝐵×𝐴𝑂，又垂直于 𝐴𝐵。这可以通过三重积来实现，即：
                𝑑=(𝐴𝐵×𝐴𝑂)×𝐴𝐵
                这个三重积的结果是一个向量，它垂直于 𝐴𝐵 和 𝐴𝐵×𝐴𝑂 所在的平面。换句话说，它是垂直于 𝐴𝐵 的并且指向原点的可能性最大。

                简单来说：通过选择垂直于 𝐴𝐵 的方向，我们可以在最有可能包含原点的方向上进行搜索，从而提高搜索效率。
                 */
                }
                else//处理为三角形的情况
                {
                    //C是单纯形第一次插入的元素，B是第二次插入的，A是最后插入的
                    //构建向量AB,AC与AO,并来检测原点在空间的哪个沃罗诺伊区域（通过排除法可以知道肯定在AB或AC或ABC三角形内部区域）
                    TSVector2 AC = Simplex[simplexLastInd - 3] - Simplex[simplexLastInd - 1];
                    TSVector2 AB = Simplex[simplexLastInd - 2] - Simplex[simplexLastInd - 1];
                    TSVector2 AO = Simplex[simplexLastInd - 1];
                    GetFurthestPointExtensions.Negate(ref AO);//这里记得取反
                    //#通过三重积 分别得到垂直于AB、AC转向特定方向的的向量，检测区域Rab、Rac中是否包含原点。
                    TripleProduct2d(out var ABPrep,AC,AB,AB);
                    //TSVector2 ABPrep = TripleProduct2d(AC, AB, AB).normalized;
                    TSVector2.Normalize(ABPrep,out ABPrep);
                    TripleProduct2d(out TSVector2 ACPrep,AB, AC, AC);
                    TSVector2.Normalize(ACPrep,out ACPrep);
                    //Debug.Log(ABPrep+" "+ACPrep+" "+AC+" "+AB+" "+AO);
                    //#如果原点在AB区域中，我们移除点C以寻找更加完美的simplex（C离原点最远），新的方向就是垂直于AB的向量
                    if (TSVector2.Dot(ABPrep, AO) > 0)
                    {
                        for (int i = 1; i < 3; i++)
                        {
                            Simplex[i - 1] = Simplex[i];
                        }//删除数组首个元素（C点），当前的单纯形并不包含原点，
                        simplexLastInd--;
                        direction = ABPrep;
                    } else if (TSVector2.Dot(ACPrep, AO) > 0) {
                        //#如果原点在AC区域中，我们移除点B以寻找更加完美的simplex，新的方向就是垂直于AC的向量
                        Simplex[simplexLastInd - 2] = Simplex[simplexLastInd-1];
                        simplexLastInd--;
                        direction = ACPrep;
                    }
                    else
                    {
                        //否则单纯形包含原点，碰到了
                        Simplex.Dispose();
                        return true;
                    }
                }
            }
            //如果超过迭代次数都没有找到点，则判定为没有碰到。
            Simplex.Dispose();
            return false;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile(DisableDirectCall = true,OptimizeFor = OptimizeFor.Performance)]
        public static bool CircleCollideWithCircle(in ColliderStructure circle1,in ColliderStructure circle2)
        {
            TSVector2.DistanceSquared(in circle1.center, in circle2.center, out var dis);
            return dis <= TSMath.FastQuadratic(circle1.radius + circle2.radius);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile(DisableDirectCall = true,OptimizeFor = OptimizeFor.Performance)]
        public static bool CircleCollideWithDoubleCircle(in ColliderStructure doubleCircle,in ColliderStructure circle2)
        {
            TSVector2 centerShape = circle2.center;
            TSVector2 deltaPos = centerShape-doubleCircle.center;
            TSVector2 delta = (doubleCircle.circleCenter2 - doubleCircle.circleCenter1);
            FP len = delta.magnitude;
            FP halfLen = len * 0.5;
            TSVector2 unit = delta/len;
            FP distance = TSVector2.Dot(unit, deltaPos);
            Abs(out var absDis,distance);
            if (absDis > halfLen+doubleCircle.radius+circle2.radius) {
                //Debug.Log("Type1: "+distance + " " + (this.r + shape2.r) + " " + halfLen);
                return false;
            }
            if (absDis >= halfLen)
            {
                //Debug.Log("Type2: " + distance + " "+ centerShape + " " + this.centerCircle2+ " " + this.centerCircle1 + " " + (this.r + shape2.r) + " " + halfLen);
                if (distance > 0)
                {
                    return (doubleCircle.circleCenter2 - centerShape).LengthSquared() <= TSMath.FastQuadratic(doubleCircle.radius+circle2.radius);
                }
                else
                {
                    return (doubleCircle.circleCenter1 - centerShape).LengthSquared() <= TSMath.FastQuadratic(doubleCircle.radius+circle2.radius);
                }
            }
            else
            {
                //Debug.Log("Type3: " + distance + " " + deltaPos.LengthSquared() + " " + (this.r + shape2.r) + " " + halfLen);
                //勾股定理
                return deltaPos.LengthSquared() - TSMath.FastQuadratic(distance) <= TSMath.FastQuadratic(doubleCircle.radius+circle2.radius);
            }
        }
    }
}