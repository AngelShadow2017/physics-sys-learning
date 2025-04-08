using System;
using System.Collections.Generic;
using TrueSync;
using UnityEngine;
using UnityEngine.Pool;
using ZeroAs.DOTS.Colliders;

namespace Core.Algorithm
{
    public interface ICollideShape {
        public ref ColliderStructure Collider { get; }
        //获取在某个方向上的最远点
        public TSVector2 GetFurthestPoint(in TSVector2 direction);
        //设置该形状的旋转角
        public void SetRotation(FP rotRad);
        public void SetCenter(in TSVector2 center);
        public TSVector2 GetCenter();
        //左上点和右下点！！！
        public TSVector4 GetBoundingBox();
        public CollisionManager.CollisionGroup colliGroup { get; set; }
        public bool enabled { get; set; }
        public void DebugDisplayColliderShape(Color color);
    }
    public interface IMasteredCollider<T>
    {
        public T Master { get; set; }
    }
    [Serializable]
    public abstract class ColliderBase : ICollideShape
    {
        public ColliderStructure collider;

        public ref ColliderStructure Collider => ref collider;
        protected CollisionManager.CollisionGroup __colli__ = CollisionManager.CollisionGroup.Default;
        protected bool __enabled__  = true;
        public int tag = -1;//用来识别特定的tag
        public CollisionManager.CollisionGroup colliGroup {
            get
            {
                return __colli__;
            }
            set
            {
                __colli__ = value;
            }
        }
        public bool enabled
        {
            get
            {
                return __enabled__;
            }
            set
            {
                __enabled__ = value;
            }
        }

        static TSVector2 SupportFunc(ColliderBase shape1,ColliderBase shape2,TSVector2 direciton) {
            //Debug.Log("direction: "+direciton+" "+ shape1.GetFurthestPoint(direciton)+" "+shape2.GetFurthestPoint(-direciton));
            return shape1.GetFurthestPoint(direciton) - shape2.GetFurthestPoint(-direciton);
        }
        static TSVector2 TripleProduct2d(TSVector2 a,TSVector2 b,TSVector2 c) {
            FP sign = (a.x * b.y - a.y * b.x);
            return new TSVector2(-c.y, c.x) * sign;
        }
        public abstract TSVector4 GetBoundingBox();
        public TSVector2 GetFurthestPoint(in TSVector2 direction)
        {
            GetFurthestPointExtensions.GetFurthestPoint(out var result, collider, direction);
            return result;
        }
        public abstract void SetRotation(FP rotRad);
        public abstract void SetCenter(in TSVector2 center);
        public abstract TSVector2 GetCenter();
        public static bool CheckCollide(ColliderBase shape1, ColliderBase shape2) {
            /*
         #两个形状s1,s2相交则返回True。所有的向量/点都是二维的，例如（[x,y]）
         #第一步：选择一个初始方向，这个初始方向可以是随机选择的，但通常来说是两个形状中心之间的向量，即：

         */
            TSVector2 direction = (shape2.GetCenter() - shape1.GetCenter()).normalized;
            //#第二步：找到支撑点，即第一个支撑点（即闵可夫斯基差的边上的点之一……）
            TSVector2[] Simplex = new TSVector2[3];//单纯形数组，最多只能是3个
            Simplex[0] = SupportFunc(shape1, shape2, direction);
            int simplexLastInd = 1;
            int interateTimeMax = 100;//最大迭代次数
            //#第三步：找到第一个支撑点后，以第一个支撑点为起点指向原点O的方向为新方向d
            direction = -Simplex[0].normalized;
            //#第四步：开始循环，找下一个支撑点
            while (interateTimeMax-- > 0)
            {
                TSVector2 A = SupportFunc(shape1,shape2,direction);
                //因为A点是闵可夫斯基差形状在给定方向的最远点，如果那个点没有超过原点，就不想交
                //#当新的支撑点A没有包含原点，那我们就返回False，即两个形状没有相交
                if (TSVector2.Dot(A,direction)<0) {
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
                    TSVector2 AO = -Simplex[simplexLastInd - 1];
                    TSVector2 ABPrep = TripleProduct2d(AB,AO,AB);
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
                    TSVector2 AO = -Simplex[simplexLastInd - 1];
                    //#通过三重积 分别得到垂直于AB、AC转向特定方向的的向量，检测区域Rab、Rac中是否包含原点。
                    TSVector2 ABPrep = TripleProduct2d(AC, AB, AB).normalized;
                    TSVector2 ACPrep = TripleProduct2d(AB, AC, AC).normalized;
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
                        return true;
                    }
                }
            }
            //如果超过迭代次数都没有找到点，则判定为没有碰到。
            return false;
        }
        public virtual bool CheckCollide(ColliderBase shape2) {
            return CheckCollide(this, shape2);
        }
        public virtual void DebugDisplayColliderShape(Color color) { }
    }
    [Serializable]
    public abstract class ColliderBase<T> : ColliderBase
    {
        public T Master;
    }
    [Serializable]
    public class CircleCollider : ColliderBase
    {
        public CircleCollider(FP R,TSVector2 center,CollisionManager.CollisionGroup group = CollisionManager.CollisionGroup.Default)
        {
            Collider = new ColliderStructure()
            {
                colliderType = ColliderType.Circle,
                radius = R,
                center = center
            };
            
            colliGroup = group;
        }
        public override void SetRotation(FP rotRad) { }//没错，圆形没有旋转
        public override void SetCenter(in TSVector2 center)
        {
            Collider.center = center;
        }
        public override TSVector2 GetCenter()
        {
            return Collider.center;
        }
        /*public override TSVector2 GetFurthestPoint(in TSVector2 direction)
        {
            GetFurthestPointExtensions.GetFurthestPoint(out var result, this.Collider, direction);
            return result;
        }*/
        public override TSVector4 GetBoundingBox()
        {
            TSVector2 centerPos = GetCenter();
            FP r = this.Collider.radius;
            return new TSVector4(centerPos.x-r,centerPos.y+r,centerPos.x+r,centerPos.y-r);
        }
        bool CircleCollideWithCircle(CircleCollider shape2)
        {
            return CollideExtensions.CircleCollideWithCircle(this.Collider,shape2.Collider);
        }
        public override bool CheckCollide(ColliderBase shape2)
        {
            if(shape2 is CircleCollider sh)
            {
                return CircleCollideWithCircle(sh);
            }else if(shape2 is DoubleCircleCollider d)
            {

                //Debug.Log(d.CircleCollideWithCircle(this));
                return d.CircleCollideWithCircle(this);
            }
            return CheckCollide(this, shape2);
        }
        public override void DebugDisplayColliderShape(Color color)
        {
            float r = (float)collider.radius;
            int circleCount = Mathf.FloorToInt(Mathf.Lerp(5,8,((float)r)/72f));
            float angleDelta = 2 * Mathf.PI / circleCount;

            GL.Begin(GL.TRIANGLE_STRIP);
            GL.Color(color);

            for (int i = 0; i < circleCount + 1; i++)
            {
                float angle = angleDelta * i;
                float angleNext = angle + angleDelta;
                Vector3 cent = GetCenter().ToVector();
                Vector3 cent2 = new Vector3(Mathf.Cos(angle) * (float)r, Mathf.Sin(angle) * (float)r, 0) + cent;
                GL.Vertex3(cent2.x,cent2.y,cent2.z);
                GL.Vertex3(cent.x,cent.y,cent.z);
            }

            GL.End();
        }
    }
    [Serializable]
    public class CircleCollider<T> : CircleCollider,IMasteredCollider<T>
    {
        T _master_;
        public T Master {
            get => _master_;
            set => _master_ = value;
        }

        public CircleCollider(T master,FP R, TSVector2 center, CollisionManager.CollisionGroup group = CollisionManager.CollisionGroup.Default) : base(R, center, group)
        {
            this._master_ = master;
        }

    }
    [Serializable]
    public class OvalCollider : ColliderBase
    {
        //public FP rot, b2Dividea2;//旋转，长轴方除以短轴方，因为定点数除法……真的太慢了。。。
        //public TSVector2 Axis,SqrAxis;//半长轴和半短轴？其实应该叫水平轴和竖直轴
        //TSVector2 centerPos;
        public OvalCollider(TSVector2 axis, TSVector2 center,in FP rotation, CollisionManager.CollisionGroup group = CollisionManager.CollisionGroup.Default)
        {
            collider = new ColliderStructure()
            {
                colliderType = ColliderType.Oval,
                rot = rotation,
                center = center
            };
            SetAxis(axis);
            colliGroup = group;
        }
        public void SetAxis(TSVector2 axis) {
            collider.Axis = axis;
            collider.SqrAxis = new TSVector2(TSMath.FastQuadratic(axis.x), TSMath.FastQuadratic(axis.y));
            if (collider.SqrAxis.x == 0)
            {
                collider.b2Dividea2 = FP.MaxValue;
            }
            else
            {
                collider.b2Dividea2 = collider.SqrAxis.y / collider.SqrAxis.x;
            }
        }
        public override void SetRotation(FP rotRad) {
            collider.rot = rotRad;
        }//没错，圆形没有旋转
        public override void SetCenter(in TSVector2 center)
        {
            collider.center = center;
        }
        public override TSVector2 GetCenter()
        {
            return collider.center;
        }
        /*public override TSVector2 GetFurthestPoint(in TSVector2 direction)
        {
            
            //sin和cos还是很快的，因为是线性插值……有Lut
            direction = direction.RotateRad(-rot);
            if (direction.x == 0)
            {
                return (TSMath.Sign(direction.y)*Axis.y * TSVector2.up).RotateRad(rot) + centerPos;
            }else if (direction.y == 0)
            {
                return (TSMath.Sign(direction.x) * Axis.x * TSVector2.right).RotateRad(rot) + centerPos;
            }
            FP signX = TSMath.Sign(direction.x);
            FP k = direction.y / direction.x;//目标斜率
            FP a2 = SqrAxis.x;
            FP b2 = SqrAxis.y;
            FP ratio = FP.OverflowMul(k, b2Dividea2);
            FP denominator = FP.OverflowAdd(1, FP.OverflowMul(k, ratio));
            if (denominator >= FP.MaxValue || denominator <= FP.MinValue)
            {
                //Debug.Log("denominatorOverflow "+ direction + " "+ new TSVector2(0, TSMath.Sign(direction.y) * Axis.y).RotateRad(rot));
                return new TSVector2(0, TSMath.Sign(direction.y) * Axis.y).RotateRad(rot) + centerPos;
            }
            FP value = 1.0/denominator;

            FP tarX = signX * (Axis.x*TSMath.Sqrt(value));
            FP tarY = FP.OverflowMul(tarX,ratio);
            //Debug.Log("ovalthings: "+tarX+" "+tarY+" "+k+" "+ratio);
            if (tarY >= FP.MaxValue||tarY<=FP.MinValue)
            {
                return new TSVector2(0, TSMath.Sign(direction.y)*Axis.y).RotateRad(rot) + centerPos;
            }
            return new TSVector2(tarX,tarY).RotateRad(rot)+centerPos;
        }*/
        //椭圆的包围盒超级难算，懒了，就这样吧。
        public override TSVector4 GetBoundingBox()
        {
            FP maxBorder = TSMath.Max(collider.Axis.x, collider.Axis.y);
            return new TSVector4(collider.center.x - collider.Axis.x, collider.center.y + collider.Axis.x, collider.center.x + collider.Axis.x, collider.center.y - collider.Axis.x);
        }
        public override void DebugDisplayColliderShape(Color color)
        {
            int circleCount = Mathf.FloorToInt(Mathf.Lerp(16, 32, ((float)collider.Axis.x) / 72f));
            Vector2 size = collider.Axis.ToVector();
            Vector3 cent = collider.center.ToVector();
            float rotRad = ((float)collider.rot);
            GL.Begin(GL.TRIANGLE_STRIP);
            GL.Color(color); 
            float angleDelta = 2 * Mathf.PI / circleCount;
            for (int i = 0; i < circleCount + 1; i++)
            {
                float angle = angleDelta * i;
                float angleNext = angle + angleDelta;
                Vector2 cent2 = new Vector2(Mathf.Cos(angle) * size.x, Mathf.Sin(angle) * size.y);
                cent2 = cent2.RotateRad(rotRad);
                GL.Vertex3(cent2.x+cent.x, cent2.y + cent.y, cent.z);
                GL.Vertex3(cent.x, cent.y, cent.z);
            }
            GL.End();
        }
    }
    [Serializable]
    public class OvalCollider<T> : OvalCollider, IMasteredCollider<T>
    {
        T _master_;
        public T Master
        {
            get => _master_;
            set => _master_ = value;
        }

        public OvalCollider(T master, TSVector2 axis, TSVector2 center, FP rotation, CollisionManager.CollisionGroup group = CollisionManager.CollisionGroup.Default) : base(axis, center,rotation, group)
        {
            this._master_ = master;
        }

    }
    [Serializable]
    public class PolygonCollider : ColliderBase
    {
        public FP rot;
        public TSVector2[] vertexs,movedVertexs;
        public TSVector2 centerPos;
        TSVector4 _boundingBox_=TSVector4.zero;
        public PolygonCollider() {

            //throw new System.NotImplementedException("必须为顶点赋值");
        }
        public PolygonCollider(TSVector2[] vertex, TSVector2 center, FP rotation, CollisionManager.CollisionGroup group = CollisionManager.CollisionGroup.Default)
        {
            movedVertexs = new TSVector2[vertex.Length]; //边数是不能变的
            centerPos = center;
            SetRotation(rotation);
            colliGroup = group;
        }
        public override void SetRotation(FP rotRad)
        {
            rot = rotRad;
            RotateVertexs();
        }//没错，圆形没有旋转
        public override void SetCenter(in TSVector2 center)
        {
            MoveDeltaPos(center-centerPos);
            centerPos = center;
        }
        public override TSVector2 GetCenter()
        {
            return centerPos;
        }
        void RotateVertexs()
        {
            TSVector4 tSVector4 = new TSVector4(FP.MaxValue, FP.MinValue, FP.MinValue, FP.MaxValue);
            for (int i = movedVertexs.Length - 1; i >= 0; --i)
            {
                movedVertexs[i] = vertexs[i].RotateRad(rot) + centerPos;
                if (movedVertexs[i].x > tSVector4.z)
                {
                    tSVector4.z= movedVertexs[i].x;
                }
                if (movedVertexs[i].x < tSVector4.x)
                {
                    tSVector4.x = movedVertexs[i].x;
                }
                if (movedVertexs[i].y > tSVector4.y)
                {
                    tSVector4.y = movedVertexs[i].y;//最大值，代表上方点
                }
                if (movedVertexs[i].y < tSVector4.w)
                {
                    tSVector4.w = movedVertexs[i].y;//最小值，代表下方点
                }
            }
            _boundingBox_ = tSVector4;
        }
        void MoveDeltaPos(TSVector2 pos)
        {
            for (int i = movedVertexs.Length - 1; i >= 0; --i)
            {
                movedVertexs[i] += pos;
            }
            _boundingBox_.x += pos.x;
            _boundingBox_.y += pos.y;
            _boundingBox_.z += pos.x;
            _boundingBox_.w += pos.y;
        }
        public override TSVector2 GetFurthestPoint(TSVector2 direction)
        {
            direction = direction.normalized;
            FP maxLen = FP.MinValue,len;
            TSVector2 pos = movedVertexs[0];
            for(int i = movedVertexs.Length-1; i >= 0; --i)
            {
                if((len= TSVector2.Dot(direction, movedVertexs[i])) > maxLen)
                {
                    maxLen = len;
                    pos = movedVertexs[i];
                }
            }
            return pos;
        }

        public override TSVector4 GetBoundingBox()
        {
            return _boundingBox_;
        }
    }
    [Serializable]
    public class BoxCollider : PolygonCollider
    {
        public BoxCollider(TSVector2 widthHeight,TSVector2 center,FP rotation, CollisionManager.CollisionGroup group = CollisionManager.CollisionGroup.Default)
        {
            TSVector2 a = widthHeight * 0.5;//左下，左上，右上，右下
            TSVector2 b = new TSVector2(a.x,-a.y);
            vertexs = new TSVector2[4] {-a,-b, a, b}; //边数是不能变的
            movedVertexs = new TSVector2[4]; //边数是不能变的
            centerPos = center;
            SetRotation(rotation);
            colliGroup = group;
        
        }
        public override void DebugDisplayColliderShape(Color color)
        {

            GL.Begin(GL.TRIANGLE_STRIP);
            GL.Color(color);
            //因为原来是顺时针的，现在反过来，变成逆时针画图
            for (int i = movedVertexs.Length-1; i >= 0; i--)
            {
                GL.Vertex(movedVertexs[i].ToVector());
            }

            GL.End();
        }
    }
    [Serializable]
    public class BoxCollider<T> : BoxCollider, IMasteredCollider<T>
    {
        T _master_;
        public T Master
        {
            get => _master_;
            set => _master_ = value;
        }

        public BoxCollider(T master, TSVector2 widthHeight, TSVector2 center, FP rotation, CollisionManager.CollisionGroup group = CollisionManager.CollisionGroup.Default) : base(widthHeight, center, rotation, group)
        {
            this._master_ = master;
        }

    }
    [Serializable]
    public class DiamondCollider : PolygonCollider
    {
        public DiamondCollider(TSVector2 widthHeight, TSVector2 center, FP rotation, CollisionManager.CollisionGroup group = CollisionManager.CollisionGroup.Default)
        {
            TSVector2 b = new TSVector2(widthHeight.x * 0.5, 0);
            TSVector2 c = new TSVector2(0, widthHeight.y * 0.5);
            vertexs = new TSVector2[4] { -b, c, b, -c }; //边数是不能变的
            movedVertexs = new TSVector2[4]; //边数是不能变的
            centerPos = center;
            SetRotation(rotation);
            colliGroup = group;
        }

    }
    [Serializable]
//两个相同的圆中间连线
    public class DoubleCircleCollider : ColliderBase
    {
        public FP r;
        protected TSVector2 centerPos;
        public TSVector2 centerCircle1;
        public TSVector2 centerCircle2;
        public DoubleCircleCollider(FP R, TSVector2 center1,TSVector2 center2, CollisionManager.CollisionGroup group = CollisionManager.CollisionGroup.Default)
        {
            SetCircleCenters(center1,center2);
            r = R;
            colliGroup = group;
        }
        public override void SetRotation(FP rotRad) { }//没错，圆形没有旋转
        public override void SetCenter(in TSVector2 center)
        {
            TSVector2 delta = center - centerPos;
            centerCircle1 += delta;
            centerCircle2 += delta;
            centerPos = center;
        }
        public void SetCircleCenters(TSVector2 center1,TSVector2 center2) {

            centerCircle1 = center1;
            centerCircle2 = center2;
            centerPos = (center1 + center2) * 0.5;
        }
        public void SetCircleCenter2(TSVector2 center2)
        {
            centerCircle2 = center2;
            centerPos = (centerCircle1 + center2) * 0.5;
        }
        public void SetCircleCenter1(TSVector2 center1)
        {
            centerCircle1 = center1;
            centerPos = (centerCircle2 + center1) * 0.5;
        }
        public override TSVector2 GetCenter()
        {
            return centerPos;
        }
        public override TSVector2 GetFurthestPoint(TSVector2 direction)
        {
            //d.normal*
            if (TSVector2.Dot(direction, centerCircle1-centerPos) > 0)
            {
                return direction.normalized * r + centerCircle1;
            }
            else
            {
                return direction.normalized * r + centerCircle2;
            }
        }
        public override TSVector4 GetBoundingBox()
        {
            FP minX;
            FP minY;
            FP maxX;
            FP maxY;
            if (centerCircle1.x<centerCircle2.x) {
                minX = centerCircle1.x;
                maxX = centerCircle2.x;
            }
            else
            {
                maxX = centerCircle1.x;
                minX = centerCircle2.x;
            }
            if (centerCircle1.y < centerCircle2.y)
            {
                minY = centerCircle1.y;
                maxY = centerCircle2.y;
            }
            else
            {
                maxY = centerCircle1.y;
                minY = centerCircle2.y;
            }
            return new TSVector4(minX - r, maxY + r, maxX + r, minY - r);
        }
        public bool CircleCollideWithCircle(CircleCollider shape2)
        {
            TSVector2 centerShape = shape2.GetCenter();
            TSVector2 deltaPos = centerShape-centerPos;
            TSVector2 delta = (centerCircle2 - centerCircle1);
            FP len = delta.magnitude;
            FP halfLen = len * 0.5;
            TSVector2 unit = delta/len;
            FP distance = TSVector2.Dot(unit, deltaPos);
            FP absDis = TSMath.Abs(distance);
            if (absDis > halfLen+r+shape2.r) {
                //Debug.Log("Type1: "+distance + " " + (this.r + shape2.r) + " " + halfLen);
                return false;
            }
            if (absDis >= halfLen)
            {
                //Debug.Log("Type2: " + distance + " "+ centerShape + " " + this.centerCircle2+ " " + this.centerCircle1 + " " + (this.r + shape2.r) + " " + halfLen);
                if (distance > 0)
                {
                    return (this.centerCircle2 - centerShape).LengthSquared() <= TSMath.FastQuadratic(this.r + shape2.r);
                }
                else
                {
                    return (this.centerCircle1 - centerShape).LengthSquared() <= TSMath.FastQuadratic(this.r + shape2.r);
                }
            }
            else
            {
                //Debug.Log("Type3: " + distance + " " + deltaPos.LengthSquared() + " " + (this.r + shape2.r) + " " + halfLen);
                //勾股定理
                return deltaPos.LengthSquared() - TSMath.FastQuadratic(distance) <= TSMath.FastQuadratic(this.r + shape2.r);
            }
        }
        public override bool CheckCollide(ColliderBase shape2)
        {
            if (shape2 is CircleCollider sh)
            {
                return CircleCollideWithCircle(sh);
            }
            return CheckCollide(this, shape2);
        }
        public override void DebugDisplayColliderShape(Color color)
        {
            int circleCount = Mathf.FloorToInt(Mathf.Lerp(5, 8, ((float)r) / 72f));
            float angleDelta = 2 * Mathf.PI / circleCount;

            GL.Begin(GL.TRIANGLE_STRIP);
            GL.Color(color);

            for (int i = 0; i < circleCount + 1; i++)
            {
                float angle = angleDelta * i;
                float angleNext = angle + angleDelta;
                Vector3 cent = centerCircle1.ToVector();
                Vector3 cent2 = new Vector3(Mathf.Cos(angle) * (float)r, Mathf.Sin(angle) * (float)r, 0) + cent;
                GL.Vertex3(cent2.x, cent2.y, cent2.z);
                GL.Vertex3(cent.x, cent.y, cent.z);
            }
            for (int i = 0; i < circleCount + 1; i++)
            {
                float angle = angleDelta * i;
                float angleNext = angle + angleDelta;
                Vector3 cent = centerCircle2.ToVector();
                Vector3 cent2 = new Vector3(Mathf.Cos(angle) * (float)r, Mathf.Sin(angle) * (float)r, 0) + cent;
                GL.Vertex3(cent2.x, cent2.y, cent2.z);
                GL.Vertex3(cent.x, cent.y, cent.z);
            }
            GL.End();
        }
    }
    [Serializable]
    public class DoubleCircleCollider<T> : DoubleCircleCollider, IMasteredCollider<T>
    {
        T _master_;
        public T Master
        {
            get => _master_;
            set => _master_ = value;
        }

        public DoubleCircleCollider(T master, FP R, TSVector2 center1,TSVector2 center2, CollisionManager.CollisionGroup group = CollisionManager.CollisionGroup.Default) : base(R, center1,center2, group)
        {
            this._master_ = master;
        }

    }
    [Serializable]
    public class CollisionController:IDisposable {
        /// <summary>
        /// 这个用来防止报错，在正式版的时候
        /// </summary>
        const bool strictMode = true;
        static List<ColliderBase> emptyTmp = new List<ColliderBase>();
        static HashSet<ColliderBase> hashSetTmp = new HashSet<ColliderBase>();//在非严格模式下防止回收池后报奇怪的错
        public List<ColliderBase> Colliders = ListPool<ColliderBase>.Get();
        public HashSet<ColliderBase> hadCollider = HashSetPool<ColliderBase>.Get();
        public bool destroyed = false;
        bool DestroyedChecker() {
            if (strictMode)
            {
                if (destroyed)
                {
                    Debug.LogError("Try To Use Collisions After Destoyed");
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }
        public CollisionController(params ColliderBase[] collider)
        {
            SetCollidersIEnumerable(collider);
        }
        public CollisionController(List<ColliderBase> collider)
        {
            SetCollidersIEnumerable(collider);
        }
        public void SetCollidersIEnumerable(IEnumerable<ColliderBase> colliders)
        {
            if (DestroyedChecker()) return;//销毁后不允许再增加
            foreach (var item in Colliders)
            {
                CollisionManager.instance.RemoveShape(item);//添加物品
            }
            hadCollider.Clear();
            Colliders.Clear();
            foreach (var item in colliders)
            {
                AppendCollider(item);
            }
        }
        public void AppendCollider(ColliderBase item) {
            if(DestroyedChecker()) return;//销毁后不允许再增加
            if (hadCollider.Add(item))
            {
                Colliders.Add(item);
                CollisionManager.instance.AddShape(item);//添加物品
            }
        }
        public void RemoveCollider(int startIndex,int length) {
            if (DestroyedChecker()) return;//销毁后不允许再增加
            int endIndex = startIndex+length;
            for (int i = startIndex;i<endIndex;i++) {
                var item = Colliders[i]; 
                CollisionManager.instance.RemoveShape(item);
                CollisionManager.instance.RemoveListener(item);//尝试停止所有的监听器
                CollisionManager.instance.RemoveReceiver(item);//尝试停止所有的监听器
                hadCollider.Remove(item);
            }
            if (startIndex == 0 && length == Colliders.Count)
            {
                Colliders.Clear();
                return;
            }
            Colliders.RemoveRange(startIndex, length);
        }
        public void SetColliders(params ColliderBase[] colliders) {
            SetCollidersIEnumerable(colliders);
        }
        public CollisionController AddListener(bool multiColli,Action<ColliderBase> collideEnter, Action<ColliderBase> collide, Action<ColliderBase> collideLeave, params CollisionManager.CollisionGroup[] collisionGroups)
        {
            if (DestroyedChecker()) return this;//销毁后不允许再增加
            foreach (var item in Colliders)
            {
                CollisionManager.instance.AddListener(item, collideEnter, collide, collideLeave, collisionGroups).multiColli=multiColli;
            }
            return this;
        }
        public CollisionController AddListener(Action<ColliderBase> collideEnter, Action<ColliderBase> collide, Action<ColliderBase> collideLeave,params CollisionManager.CollisionGroup[] collisionGroups) {
            if (DestroyedChecker()) return this;//销毁后不允许再增加
            foreach (var item in Colliders)
            {
                CollisionManager.instance.AddListener(item, collideEnter, collide, collideLeave, collisionGroups);
            }
            return this;
        }
        public CollisionController AddListener(Action<ColliderBase> collide, params CollisionManager.CollisionGroup[] groups) { 
            return AddListener(null,collide,null,groups);
        }
        public CollisionController AddListener(Action<ColliderBase> collide, bool multiColli=false, params CollisionManager.CollisionGroup[] groups)
        {
            return AddListener(multiColli ,null, collide, null, groups);
        }
        public CollisionController MoveDeltaPos(in TSVector2 deltaPos) {
            if (DestroyedChecker()) return this;//销毁后不允许再增加
            foreach (var i in Colliders)
            {
                CollisionManager.instance.SetCenter(i.GetCenter()+deltaPos, i);
            }
            return this;
        }
        public CollisionController SetCenter(in TSVector2 center) {
            if (DestroyedChecker()) return this;//销毁后不允许再增加
            foreach (var i in Colliders) {
                CollisionManager.instance.SetCenter(center, i);
            }
            return this;
        }
        public CollisionController SetEnabled(bool enabled)
        {
            if (DestroyedChecker()) return this;//销毁后不允许再增加
            foreach (var item in Colliders)
            {
                item.enabled = enabled;
            }
            return this;
        }
        public void Destroy()
        {
            if (destroyed) {
                return;
            }
            destroyed = true;
            foreach (var i in Colliders)
            {
                CollisionManager.instance.RemoveShape(i);
                CollisionManager.instance.RemoveListener(i);//尝试停止所有的监听器
                CollisionManager.instance.RemoveReceiver(i);//尝试停止所有的监听器
            }
            HashSetPool<ColliderBase>.Release(hadCollider);
            ListPool<ColliderBase>.Release(Colliders);
            hadCollider = null;
            Colliders = null;
            if (!strictMode)
            {
                Colliders = emptyTmp;
                hadCollider = hashSetTmp;
                Colliders.Clear();
                hadCollider.Clear();
            }
        }
        public void Dispose() {
            Destroy();
        }
    }
    public class QuadTree<T> where T : ICollideShape
    {
        public int cellSize = 64;//最小格子宽高
        public FP OneDivideCellSize;
        public int depth = 6;//载入时的深度
        public int maxSize;
        public int groupCnt = 1;//碰撞组的个数
        FP maxSizeFP,OneHalfMaxSizeFP;
        FP[] log4Numbers, log2Numbers;
        public TreeNode root = new TreeNode();
        public TreeNode[] treeNodeArr = new TreeNode[0];//用数组优化四叉树的读取
        //储存每个collider所在的坐标格子位置，
        public Dictionary<T, positionInformation> colliderPositions = new Dictionary<T, positionInformation>();
        //public HashSet<T>[] unmanagedColliders;

        protected void __init__(int dp = 6, int cw = 64) {
            cellSize = cw;
            depth = dp;
            maxSize = cw << (dp);
            maxSizeFP = maxSize;
            OneHalfMaxSizeFP = maxSizeFP * 0.5;
            OneDivideCellSize = FP.One / (FP)cw;
            log4Numbers = new FP[depth + 1];
            log2Numbers = new FP[depth + 1];
            groupCnt = Enum.GetValues(typeof(CollisionManager.CollisionGroup)).Length;
            for (int i = 1; i <= depth; i++)
            {
                //0 2 4 6 8
                log4Numbers[i] = FP.One / (FP)(i << 1);
                log2Numbers[i] = FP.One / (FP)(i);
            }
            /*unmanagedColliders = new HashSet<T>[groupCnt];
        for (int i = 0; i < groupCnt; i++)
        {
            unmanagedColliders[i] = new HashSet<T>();
        }*/
            BuildTree();
        }
        public static int FastPow(int a, int n)
        {
            int ans = 1; // 赋值为乘法单位元，可能要根据构造函数修改
            while (n != 0)
            {
                if ((n & 1) != 0)
                {
                    ans *= a; // 这里就最好别用自乘了，不然重载完*还要重载*=，有点麻烦。
                }
                n >>= 1;
                a = a*a;
            }
            return ans;
        }
        /*public int CalcDepthCount(int dp) {
        return (FastPow(4, dp+1)-1) / 3;
    }*/
        public int CalcDepthCount(int dp)
        {
            return ((1<<((dp+1)<<1)) - 1) / 3;//等比数列求和
        }
        //按照
        /*
     root ,
        lt,
        rt,
        lb,
        rb,
            lt.lt, lt.rt,rt.lt,rt.rt, lt.lb, lt.rb,rt.lb,rt.rb , 4^n个
    初始位置：4^0+...+4^(n-1) = 1*(1-4^n)/(1-4)
    //以屏幕为左上角，坐标向右边和下面增加
    坐标：floor(当前层左上坐标/当前层高度)*(最大层宽度/当前层宽度)+floor(当前左上坐标/当前层宽度)
     */
        public int GetPosition(int curDepth,Vector2Int scaledPosition)
        {
            return CalcDepthCount(curDepth-1)+scaledPosition.y * (1 << (curDepth)) + scaledPosition.x;
        }
        //计算子四边形的缩放过的坐标
        /// <summary>
        /// 把原始的四个边界从中间分成四份，然后再把坐标*2
        /// </summary>
        /// <param name="r">原始的四个边界</param>
        /// <returns></returns>
        TSVector4[] splitRect(TSVector4 r) {
            //originalRect *= 2;
            /*return new TSVector4[] { 
            new TSVector4(originalRect.x,originalRect.y,(originalRect.z+originalRect.x)/2,(originalRect.w+originalRect.y)/2),
            new TSVector4((originalRect.x+originalRect.z)/2,originalRect.y,(originalRect.x+originalRect.z)/2+(originalRect.z-originalRect.x)/2,(originalRect.w+originalRect.y)/2),
        };*/
            /*originalRect.x += originalRect.x;
        originalRect.y += originalRect.y;
        originalRect.z += originalRect.x;
        originalRect.w += originalRect.y;*/
            /*
         x,y,x+(z-x)/2,y+(w-y)/2
         x+(z-x)/2,y,z,y+(w-y)/2
         x,y+(w-y)/2,x+(z-x)/2,w
         x+(z-x)/2,y+(w-y)/2,z,w
         
         */
            /*
         2x,2y,x+z,y+w
         x+z,2y,2z,y+w
         2x,y+w,x+z,2w
         x+z,y+w,2z,2w
         */
            /*return new TSVector4[] {
            new TSVector4(originalRect.x,originalRect.y,originalRect.z,originalRect.w),
            new TSVector4(originalRect.z,originalRect.y,originalRect.z+originalRect.z-originalRect.x,originalRect.w),
            new TSVector4(originalRect.x,originalRect.w,originalRect.z,originalRect.w+originalRect.w-originalRect.y),
            new TSVector4(originalRect.z,originalRect.w,originalRect.z+originalRect.z-originalRect.x,originalRect.w+originalRect.w-originalRect.y),
        };*/
            FP a = r.x + r.x;
            FP b = r.y + r.y;
            FP c = r.x + r.z;
            FP d = r.y + r.w;
            FP e = r.z + r.z;
            FP f = r.w + r.w;
            return new TSVector4[] {
                new TSVector4(a,b,c,d),
                new TSVector4(c,b,e,d),
                new TSVector4(a,d,c,f),
                new TSVector4(c,d,e,f)
            };
        }
        //应该不是，直接用大小存吧……
        public class TreeNode {
            public HashSet<T>[] Values;
            public TreeNode[] Children = new TreeNode[4];
            public TreeNode parent = null;
        }
        TSVector4 TransAxis(TSVector4 vec) {
            vec.y = OneHalfMaxSizeFP - vec.y;
            vec.w = OneHalfMaxSizeFP - vec.w;
            vec.x += OneHalfMaxSizeFP;
            vec.z += OneHalfMaxSizeFP;
            return vec;
        }
        int GetDepth(int width) { //如果是>=1的话也要放在上（更大的）一层比如64的大小（格子也是64）就应该放在128，不然可能会出界什么的。。。
            int layer = 0;//1 2 4 8
            while (width >> layer > 1)
            {
                layer++;
            }
            return depth - layer;
        }
        public struct positionInformation {
            public int layer;
            public Vector2Int mainPos;
            public Vector2Int rightBottom;

            public override bool Equals(object obj)
            {
                return obj is positionInformation other && Equals(other);
            }

            public bool Equals(positionInformation other)
            {
                return this.layer == other.layer &&
                       this.mainPos == other.mainPos &&
                       this.rightBottom == other.rightBottom;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(layer, mainPos, rightBottom);
            }

            public static bool operator ==(positionInformation left, positionInformation right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(positionInformation left, positionInformation right)
            {
                return !(left == right);
            }

            // New method to check if the current instance contains another instance's range
            public bool Contains(positionInformation other)
            {
                return this.layer == other.layer &&
                       this.mainPos.x <= other.mainPos.x &&
                       this.mainPos.y <= other.mainPos.y &&
                       this.rightBottom.x >= other.rightBottom.x &&
                       this.rightBottom.y >= other.rightBottom.y;
            }
            public bool TrueContains(positionInformation other)
            {
                return this.layer == other.layer &&
                       this.mainPos.x < other.mainPos.x &&
                       this.mainPos.y < other.mainPos.y &&
                       this.rightBottom.x > other.rightBottom.x &&
                       this.rightBottom.y > other.rightBottom.y;
            }

            // Overload >= operator to use the Contains method
            public static bool operator >=(positionInformation left, positionInformation right)
            {
                return left.Contains(right);
            }
            public static bool operator <=(positionInformation left, positionInformation right)
            {
                return right.Contains(left);
            }
            public static bool operator >(positionInformation left, positionInformation right)
            {
                return left.TrueContains(right);
            }
            public static bool operator <(positionInformation left, positionInformation right)
            {
                return right.TrueContains(left);
            }
        }
        //可能是一个或者四个坐标，插入一次或者四次
        public positionInformation GetCellIndex(TSVector4 rect) {
            rect = TransAxis(rect);//乘1/最小格子大小可以得到64-->1  128 --> 2这样
            if (rect.x<0||rect.y<0||rect.z>maxSize||rect.w>maxSize) {
                return new positionInformation { //出界了，这个应该放在第0层里面，不参与四叉树判断
                    mainPos = Vector2Int.zero,
                    rightBottom = Vector2Int.zero,
                    layer = 0
                };
            }
            rect *= OneDivideCellSize;
            //获取应该放在哪一层
            int layer = GetDepth(TSMath.Ceil(TSMath.Max(rect.w - rect.y, rect.z - rect.x)).AsInt());//TSMath.Ceil(TSMath.Log2(TSMath.Max(rect.w - rect.y, rect.z - rect.x))).AsInt();//这个Log很慢，因为是定点数，自己写一个
            if (layer < 0)
            {
                return new positionInformation
                { //出界了，这个应该放在第0层里面，不参与四叉树判断
                    mainPos = Vector2Int.zero,
                    rightBottom = Vector2Int.zero,
                    layer = 0
                };
            }
            int divide = depth - layer;
            Vector2Int mainPos = new Vector2Int(rect.x.AsInt()>> divide, rect.y.AsInt()>> divide);
            Vector2Int rightBottom = new Vector2Int((rect.z.AsInt() >> divide), (rect.w.AsInt() >> divide));
            return new positionInformation
            {
                mainPos = mainPos,
                rightBottom = rightBottom, layer = layer
            };
        }
        /// <summary>
        /// 根据坐标批量处理某个事 action传入的是 当前深度depth和坐标的参数
        /// </summary>
        /// <param name="action"></param>
        /// <param name="area"></param>
        public void Manufacture(Action<int,Vector2Int> action,positionInformation area)
        {
            //对应级别的坐标
            Vector2Int nowPos = area.mainPos;
            Vector2Int tar = area.rightBottom;
            for(int i = nowPos.x; i <= tar.x; i++)
            {
                for (int j = nowPos.y;j<=tar.y;j++) { 
                    Vector2Int pos = new Vector2Int(i,j);
                    action(area.layer,pos);
                }
            }
        }
        //带break的
        public bool Manufacture(Func<int, Vector2Int,bool> action, positionInformation area)
        {
            //对应级别的坐标
            Vector2Int nowPos = area.mainPos;
            Vector2Int tar = area.rightBottom;
            for (int i = nowPos.x; i <= tar.x; i++)
            {
                for (int j = nowPos.y; j <= tar.y; j++)
                {
                    Vector2Int pos = new Vector2Int(i, j);
                    if(!action(area.layer, pos)) { return false; }
                }
            }
            return true;
        }
        public void RemoveCollider(T obj,bool insert=false) {
            int grp = (int)obj.colliGroup;
            if (colliderPositions.ContainsKey(obj))
            {
                Manufacture((layer,posInArr) => {
                    //Debug.Log(layer+ " "+posInArr);
                    var objs = treeNodeArr[GetPosition(layer, posInArr)].Values[grp];
                    objs.Remove(obj);
                }, colliderPositions[obj]);
                colliderPositions.Remove(obj);
            }/*else
        {
            unmanagedColliders[grp].Remove(obj);
        }*/
        }
        public void ClearAllCollider()
        {
            foreach (var obj in colliderPositions)
            {
                RemoveCollider(obj.Key);
            }
            /*foreach (var obj in unmanagedColliders) { 
            obj.Clear();
        }*/
        }
        public void InsertCollider(T obj) {
            int grp = (int)obj.colliGroup;
            TSVector4 rect = obj.GetBoundingBox();//获取物体的包围盒
            positionInformation positions = GetCellIndex(rect),
                oldPosition;
            bool hasObj = colliderPositions.ContainsKey(obj),flag=true;
            if (hasObj) {//如果position和原position在是包含关系，这里不能替换成大于，因为 会比较图层是否相等
                oldPosition = colliderPositions[obj];
                if (!(oldPosition <= positions))
                {
                    RemoveCollider(obj,true);
                } else if (oldPosition==positions) {
                    flag = false;
                }
            }
            colliderPositions[obj] = positions;
            if (flag){
                Manufacture((layer, posInArr) => {
                    var objs = treeNodeArr[GetPosition(layer, posInArr)].Values[grp];
                    objs.Add(obj);
                }, positions);
            }
        }
        void BuildTree() {
            int curDepth = 0;
            int needCnt = CalcDepthCount(depth);
            root = new TreeNode();
            treeNodeArr = new TreeNode[needCnt];
            TSVector4 nowRect = new TSVector4(0,0,1,1);//四边形的边框
            Dictionary<int,Tuple<int,Vector2Int>> errorChecker = new Dictionary<int, Tuple<int, Vector2Int>>();
            void dfs(TreeNode node=null) {
                treeNodeArr[GetPosition(curDepth,new Vector2Int(nowRect.x.AsInt(), nowRect.y.AsInt()))] = node;//存入数组
                if(errorChecker.ContainsKey(GetPosition(curDepth, new Vector2Int(nowRect.x.AsInt(), nowRect.y.AsInt()))))
                {
                    var conf = errorChecker[GetPosition(curDepth, new Vector2Int(nowRect.x.AsInt(), nowRect.y.AsInt()))];
                    if(curDepth!=conf.Item1||conf.Item2!= new Vector2Int(nowRect.x.AsInt(), nowRect.y.AsInt()))
                    {
                        Debug.LogError("Big Append Error" + " " + curDepth + " " + new Vector2Int(nowRect.x.AsInt(), nowRect.y.AsInt()) + " conflict with " + conf.Item1 + " " + conf.Item2);

                    }
                }
                else
                {
                    errorChecker.Add(GetPosition(curDepth, new Vector2Int(nowRect.x.AsInt(), nowRect.y.AsInt())), new Tuple<int, Vector2Int>(curDepth, new Vector2Int(nowRect.x.AsInt(), nowRect.y.AsInt())));
                }
                /*if (curDepth == 2) {
                Debug.Log(curDepth+" "+nowRect);
            }*/
                //初始化碰撞组
                node.Values = new HashSet<T>[groupCnt];
                for (int j = 0; j < groupCnt; j++)
                {
                    node.Values[j] = new HashSet<T>();
                }

                if (curDepth>=depth)
                {
                    return;
                }
                TSVector4 tmpRect = nowRect;
                TSVector4[] splited = splitRect(tmpRect);
                /*StringBuilder sb = new StringBuilder();
            sb.Append(curDepth);
            for (int i = 0;i<splited.Length;i++) {
                sb.Append(" " + splited[i]);
            }
            Debug.Log(sb);*/
                curDepth++;
                for (int i = 0;i<splited.Length;i++) { 
                    nowRect = splited[i];
                    if (node.Children[i] == null)
                    {
                        node.Children[i] = new TreeNode();
                    }
                    node.Children[i].parent = node;
                    dfs(node.Children[i]);
                }
                curDepth--;
                nowRect = tmpRect;
            }
            dfs(root);
        }
    }
//碰撞对象管理器
    public class CollisionManager {
        public static void voidFunction(ColliderBase c) { }
        public static CollisionManager instance;
        public int groupCnt = Enum.GetValues(typeof(CollisionGroup)).Length;
        public LinkedHashSet<ColliderBase>[] groupedColliders;//这个到时候要改成LinkedHashSet之类的东西。。。
        public HashSet<ColliderBase> colliders = new HashSet<ColliderBase>();
        public LinkedDictionary<ColliderBase, LinkedPooledHashSet<__action_checkColli__>> listeners = new LinkedDictionary<ColliderBase, LinkedPooledHashSet<__action_checkColli__>>();
        public LinkedDictionary<ColliderBase, LinkedDictionary<CollisionGroup,__action_checkColli__>> receivers = new LinkedDictionary<ColliderBase, LinkedDictionary<CollisionGroup, __action_checkColli__>>();

        public HashSet<ColliderBase> tmpDrawingHasCheckedObjectsInCurFrame = new HashSet<ColliderBase>();//用来debug有哪些物体当前帧被检查碰撞
        //readonly bool multiCollisionOptimize = false;//先关掉多碰撞优化，测试功能
        private Material _shapeMaterial;//测试
        public CollisionManager() {
            groupedColliders = new LinkedHashSet<ColliderBase>[groupCnt];
            for (int i = 0; i < groupCnt; i++) { 
                groupedColliders[i] = new LinkedHashSet<ColliderBase>();
            }
        }
        public class __action_checkColli__ {
            public ColliderBase collider;
            public SortedSet<CollisionGroup> checkGroups;
            public Action<ColliderBase> callbackEnter, callback, callbackLeave;
            public ColliderBase triggeringObj = null;
            public CollisionGroup recieveGroup;
            public bool multiColli = false;
        }
        public enum CollisionGroup
        {
            Default,
            Hero,
            HeroBullet,
            Bullet,
            Enemy,
            EnemyCollideBullet,
            Item
        }
        //方便过后删除掉
        //添加碰撞监听器
        //有多碰撞需求再改吧……反正就改个list现在仅支持碰一个物体
        public __action_checkColli__ AddListener(ColliderBase collider,Action<ColliderBase> callbackEnter, Action<ColliderBase> callback, Action<ColliderBase> callbackLeave, params CollisionGroup[] checkGroups) {
            var obj = new __action_checkColli__
            {
                collider = collider,
                checkGroups = new SortedSet<CollisionGroup>(checkGroups),
                callbackEnter = callbackEnter,
                callbackLeave = callbackLeave,
                callback = callback
            };
            if (listeners.ContainsKey(collider))
            {
                listeners[collider].Add(obj);
            }
            else
            {
                var linkedSet = ObjectPool<LinkedPooledHashSet<__action_checkColli__>>.GetObject();
                linkedSet.Add(obj);
                listeners.Add(collider, linkedSet);
            }
            return obj;
        }
        public __action_checkColli__ AddListener(ColliderBase collider, Action<ColliderBase> callback, params CollisionGroup[] checkGroups)
        {
            return AddListener(collider, null, callback, null, checkGroups);
        }
        //移除一个碰撞监听器行为
        public void RemoveListener(__action_checkColli__ action)
        {
            if (!listeners.ContainsKey(action.collider))
            {
                return;
            }
            listeners[action.collider].Remove(action);
            if (listeners[action.collider].Count == 0)
            {
                RemoveListener(action.collider);
                return;
            }
        }
        //移除整个碰撞监听器
        public void RemoveListener(ColliderBase collider) {
            if (listeners.ContainsKey(collider)) {
                ObjectPool<LinkedPooledHashSet<__action_checkColli__>>.ReturnObject(listeners[collider]);
            }
            listeners.Remove(collider);
        }
        //receiver不支持enter和leave
        __action_checkColli__ AddReceiver(ColliderBase collider, Action<ColliderBase> callbackEnter, Action<ColliderBase> callback, Action<ColliderBase> callbackLeave, CollisionGroup recieveGroup)
        {
            var obj = new __action_checkColli__
            {
                collider = collider,
                callbackEnter = callbackEnter,
                callbackLeave = callbackLeave,
                callback = callback,
                recieveGroup = recieveGroup
            };
            if (receivers.ContainsKey(collider))
            {
                receivers[collider][recieveGroup] = obj;
            }
            else
            {
                receivers.Add(collider, new LinkedDictionary<CollisionGroup, __action_checkColli__> { { recieveGroup, obj } });
            }
            return obj;
        }
        public __action_checkColli__ AddReceiver(ColliderBase collider, Action<ColliderBase> callback, CollisionGroup recieveGroup)
        {
            return AddReceiver(collider, null, callback, null, recieveGroup);
        }
        //移除一个碰撞接受器行为
        public void RemoveReceiver(__action_checkColli__ action)
        {
            if (!receivers.ContainsKey(action.collider))
            {
                return;
            }
            receivers[action.collider].Remove(action.recieveGroup);
            if (receivers[action.collider].Count == 0)
            {
                RemoveReceiver(action.collider);
                return;
            }
        }
        //移除整个碰撞接受器
        public void RemoveReceiver(ColliderBase collider)
        {
            receivers.Remove(collider);
        }
        public CollisionManager AddShape(ColliderBase collider) {
            /*if (multiCollisionOptimize)
        {
            quadTreeOptmize.InsertCollider(collider);
        }
        else*/
            {
                int grp = (int)collider.colliGroup;
                groupedColliders[grp].Add(collider);
            }
            colliders.Add(collider);
            return this;
        }
        public void RemoveShape(ColliderBase collider) {
            /*if (multiCollisionOptimize)
        {
            quadTreeOptmize.RemoveCollider(collider);
        }
        else*/
            {
                int grp = (int)collider.colliGroup;
                groupedColliders[grp].Remove(collider);
            }
            colliders.Remove(collider);
        }
        public void SetCenter(in TSVector2 Pos,ColliderBase shape)
        {
            shape.SetCenter(Pos);
            /*if (multiCollisionOptimize)
        {
            quadTreeOptmize.InsertCollider(shape);
        }*/
        }

        /*public void SwitchMultiColli(bool swit) {
        if (multiCollisionOptimize == swit) { return; }
        if (swit)
        {
            foreach(var i in colliders)
            {
                quadTreeOptmize.InsertCollider(i);
            }
            foreach (var groupHashSet in groupedColliders) {
                groupHashSet.Clear();
            }
        }
        else
        {
            quadTreeOptmize.ClearAllCollider();
            foreach(var i in colliders)
            {
                AddShape(i);
            }
        }
        multiCollisionOptimize = swit;
    }*/
        public ColliderBase CheckCollision(ColliderBase obj,CollisionGroup group) {
            if (!obj.enabled)
            {
                return null;
            }
            /*if (multiCollisionOptimize)
        {
            return quadTreeOptmize.CheckCollision(obj, (int)group);
        }
        else*/
            {
                int grp = (int)group;
                foreach(var i in groupedColliders[grp])
                {
                    if (!i.enabled) { continue; }
                    if (obj.CheckCollide(i))
                    {
                        return i;
                    }
                }
                return null;
            }
        }
        public void CheckCollision(ColliderBase obj, CollisionGroup group, ref List<ColliderBase> colliObj, bool force = false) {
            if (!obj.enabled&&!force)
            {
                return;
            }
            {
                int grp = (int)group;
                foreach (var i in groupedColliders[grp])
                {
                    if (!i.enabled && !force) { continue; }
                    if (obj.CheckCollide(i))
                    {
                        colliObj.Add(i);
                    }
                }
            }
        }

        public void TraverseAllListener() {
            {
                tmpDrawingHasCheckedObjectsInCurFrame.Clear();
            }
            foreach(var kvPair in listeners)
            {
                var val = kvPair.Value;
                foreach (var obj in val)
                {
                    if (!obj.collider.enabled) { continue; }//如果自身不允许碰撞就不碰
                    foreach (var i in obj.checkGroups)
                    {
                        //多碰撞不支持接收器
                        if (obj.multiColli)
                        {
                            List<ColliderBase> collis = ListPool<ColliderBase>.Get();
                            CheckCollision(obj.collider, i, ref collis);
                            foreach(var retObj in collis)
                            {
                                callActionComponent(retObj, obj);
                            }
                            ListPool<ColliderBase>.Release(collis);
                        }else{
                            var retObj = CheckCollision(obj.collider, i);
                            callActionComponent(retObj, obj);
                            //如果对方有接收器
                            if (retObj != null && receivers.ContainsKey(retObj) && receivers[retObj].ContainsKey(obj.collider.colliGroup))
                            {
                                callActionComponent(obj.collider, receivers[retObj][obj.collider.colliGroup]);
                            }
                        }
                    }
                }
            }
        }
        void callActionComponent(ColliderBase beCollidedObj,__action_checkColli__ action) {
            if (beCollidedObj != null)
            {
                if (action.triggeringObj == null)
                {
                    action.triggeringObj = beCollidedObj;
                    if (action.callbackEnter != null)
                    {
                        action.callbackEnter(beCollidedObj);
                    }
                }
                action.callback(beCollidedObj);
            }
            else if (action.triggeringObj != null)
            {
                if (action.callbackLeave != null)
                {
                    action.callbackLeave(action.triggeringObj);
                }
                action.triggeringObj = null;
            }
        }
        public void DebugDisplayShape(Matrix4x4 matrixTransform) {
            if (_shapeMaterial == null)
            {
                _shapeMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
            }
            _shapeMaterial.SetPass(0);
            GL.PushMatrix();
            GL.MultMatrix(matrixTransform);
            /*if (!multiCollisionOptimize)
        {
            for (int i = 0; i < groupedColliders.Length; i++)
            {
                Color color = Color.Lerp(new Color(1, 0, 0, 0.2f), new Color(0, 0, 1, 0.2f), (float)i / groupCnt);
                foreach (var collider in groupedColliders[i])
                {
                    collider.DebugDisplayColliderShape(color);
                }
            }
        }
        else*/
            {
                foreach (var collider in colliders)
                {
                    if (!collider.enabled || tmpDrawingHasCheckedObjectsInCurFrame.Contains(collider)) { continue; }
                    Color color = Color.HSVToRGB((float)collider.colliGroup / groupCnt, 1, 1);
                    color.a = 0.2f;
                    //Color color = Color.Lerp(new Color(1, 0, 0, 0.2f), new Color(0, 0, 1, 0.2f), (float)collider.colliGroup / groupCnt);
                    collider.DebugDisplayColliderShape(color);
                }
                foreach (var collider in tmpDrawingHasCheckedObjectsInCurFrame)
                {
                    //Color color = Color.Lerp(new Color(1, 1, 0, 0.2f), new Color(0, 1, 1, 0.2f), (float)collider.colliGroup / groupCnt);
                    Color color = Color.HSVToRGB((float)collider.colliGroup / groupCnt, 2, 1);
                    color.a = 0.2f;
                    collider.DebugDisplayColliderShape(color);
                }
            }
            GL.PopMatrix();
        }
    }
/*
 def GJK(s1,s2)
#两个形状s1,s2相交则返回True。所有的向量/点都是三维的，例如（[x,y,0]）
#第一步：选择一个初始方向，这个初始方向可以是随机选择的，但通常来说是两个形状中心之间的向量，即：
    d= normalize(s2.center-s1.center)
#第二步：找到支撑点，即第一个支撑点
    simplex=[support(s1,s2,d)]
#第三步：找到第一个支撑点后，以第一个支撑点为起点指向原点O的方向为新方向d
     d=ORIGIN-simplex[0]
#第四步：开始循环，找下一个支撑点
    while True
        A=[support(s1,s2,d)]
#当新的支撑点A没有经过原点，那我们就返回False，即两个形状没有相交
        if dot(A,d) <0:
            return False
#否则，我们就将该点A加入到simplex中
        simplex.append(A)
#handleSimplex负责主要逻辑部分。主要负责处理寻找新方向和更新simplex的逻辑内容,当当前simplex包含原点，则返回Ture
        if handleSimplex(simplex,d):
            return Ture
 
def handleSimplex(simplex,d)
#如果当前的simplex为直线情况，则进入lineCase(simplex,d)函数,寻找下一个方向d,并返回False，即直线情况下的simplex不包含原点
    if len(simplex==2):
        return lineCase(simplex,d)
#如果当前的simplex为三角情况，则进入triangleCase(simplex,d,
    return triangleCase(simplex,d)
 
def  lineCase(simplex,d)
#构建向量AB与AO,并使用三重积得到下一个方向
    B,A = simplex
    AB,AO=B-A,ORIGIN-A
    ABprep= tripleProd(AB,AO,AB)
    d.set(ABprep)
#由于一条直线的情况下，原点不能包含在simplex中，所以返回False
    return False
 
def triangleCase(simplex,d)
#构建向量AB,AC与AO,并来检测原点在空间的哪个区域。
    C,B,A = simplex
    AB,AC,AO=B-A,C-A,ORIGIN-A
#通过三重积分别得到垂直于AB、AC的向量，检测区域Rab、Rac中是否包含原点。
    ABprep= tripleProd(AC,AB,AB)
    ACprep= tripleProd(AB,AC,AC)
#如果原点在AB区域中，我们移除点C以寻找更加完美的simplex，新的方向就是垂直于AB的向量
    if dot(ABprep,AO)>0:
       simplex.remove(C);d.set(ABprep) 
       return False
#如果原点在AC区域中，我们移除点B以寻找更加完美的simplex，新的方向就是垂直于AC的向量
    elif dot(ACprep,AO)>0:
       simplex.remove(Ba);d.set(ACprep) 
       return False
#如果这两种情况都不符合，那就说明当前的三角形中包含原点，两个形状相交
    return Ture
 
def support(s1,s2,d)
#取第一个形状上方向d上最远点并减去第二个形状上相反反向（-d）上最远的点
    return s1.furthestPoint(d)-s2.furthestPoint(-d)
 */
}