using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using TrueSync;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Pool;
using ZeroAs.DOTS.Colliders;

namespace Core.Algorithm
{
    public interface ICollideShape {
        public bool Registered { get; set; }
        public ref ColliderStructure Collider { get; }
        //获取在某个方向上的最远点
        public TSVector2 GetFurthestPoint(in TSVector2 direction);
        //设置该形状的旋转角
        public void SetRotation(FP rotRad);
        public void SetCenter(in TSVector2 center);
        public TSVector2 GetCenter();
        //左上点和右下点！！！
        public TSVector4 GetBoundingBox();
        public CollisionGroup colliGroup { get; set; }
        public bool enabled { get; set; }
        public void DebugDisplayColliderShape(Color color);

        public void Destroy()
        {
        }
    }
    public interface IMasteredCollider<T>
    {
        public T Master { get; set; }
    }
    [Serializable]
    public abstract class ColliderBase : ICollideShape
    {
        public bool Registered { get; set; } = false;
        public ColliderStructure collider;

        public ref ColliderStructure Collider => ref collider;
        //protected CollisionGroup __colli__ = CollisionGroup.Default;
        protected bool __enabled__  = true;
        public int tag = -1;//用来识别特定的tag
        public CollisionGroup colliGroup {
            get => collider.collisionGroup;
            set => collider.collisionGroup = value;//记得sync
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
            CheckEssentialValues();
            GetFurthestPointExtensions.GetFurthestPoint(out var result, collider, direction,ColliderNativeHelper.instancedBuffer);
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
            int interateTimeMax = 10;//最大迭代次数
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
        /*public bool CheckCollide(ColliderBase shape2) {
            return CheckCollide(this, shape2);
        }*/
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual ColliderStructure GetRealCollider()
        {
            return Collider;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void CheckEssentialValues()
        {
            
#if UNITY_EDITOR
            if (!Registered)
            {
                throw new NotSupportedException("没有把当前物体注册入控制管理器中");
            }
            var judg = CollisionManager.instance?.nativeCollisionManager.colliders.IsCreated;
            if (!judg.HasValue||!judg.Value)
            {
                throw new NotSupportedException("控制器单例不存在或buffer已被销毁");
            }
#endif
        }

        /// <summary>
        /// 设置完属性后必须调用这个函数
        /// </summary>
        public virtual void SaveState()
        {
            CheckEssentialValues();
            CollisionManager.instance.nativeCollisionManager.SyncCollider(ref collider);
        }

        public bool CheckCollide(ColliderBase shape2)
        {
            CheckEssentialValues();
            return CollideExtensions.CheckCollide(this.GetRealCollider(), shape2.GetRealCollider(),ColliderNativeHelper.instancedBuffer);
        }

        public virtual void DebugDisplayColliderShape(Color color) { }

        public virtual void Destroy()
        {
        }
    }
    [Serializable]
    public abstract class ColliderBase<T> : ColliderBase
    {
        public T Master;
    }
    [Serializable]
    public class CircleCollider : ColliderBase
    {
        public CircleCollider(FP R,TSVector2 center,CollisionGroup group = CollisionGroup.Default)
        {
            collider = ColliderStructure.CreateInstance();
            collider.colliderType = ColliderType.Circle;
            collider.radius = R;
            collider.center = center;
            collider.collisionGroup = group;
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
        /*public override bool CheckCollide(ColliderBase shape2)
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
        }*/
        public override void DebugDisplayColliderShape(Color color)
        {
            float r = (float)collider.radius;
            int circleCount = Mathf.FloorToInt(Mathf.Lerp(5,8,((float)r)/72f)*10);
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

        public CircleCollider(T master,FP R, TSVector2 center, CollisionGroup group = CollisionGroup.Default) : base(R, center, group)
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
        public OvalCollider(TSVector2 axis, TSVector2 center,in FP rotation, CollisionGroup group = CollisionGroup.Default)
        {
            collider = ColliderStructure.CreateInstance();
            collider.colliderType = ColliderType.Oval;
            collider.rot = rotation;
            collider.center = center;
            SetAxis(axis);
            collider.collisionGroup = group;
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

        public OvalCollider(T master, TSVector2 axis, TSVector2 center, FP rotation, CollisionGroup group = CollisionGroup.Default) : base(axis, center,rotation, group)
        {
            this._master_ = master;
        }

    }
    [Serializable]
    public class PolygonCollider : ColliderBase,IDisposable
    {
        public TSVector2[] vertexs;
        public NativeArray<TSVector2> movedVertexs;
        TSVector4 _boundingBox_=TSVector4.zero;
        public PolygonCollider() {

            //throw new System.NotImplementedException("必须为顶点赋值");
        }

        public void Dispose()
        {
            if (movedVertexs.IsCreated)
            {
                movedVertexs.Dispose();
            }
        }

        public PolygonCollider(TSVector2[] vertex, TSVector2 center, FP rotation, CollisionGroup group = CollisionGroup.Default)
        {
            collider = ColliderStructure.CreateInstance();
            collider.colliderType = ColliderType.Polygon;
            collider.rot = rotation;
            collider.center = center;
            collider.collisionGroup = group;
            collider.vertexCount = vertex.Length;
            movedVertexs = new NativeArray<TSVector2>(vertex.Length,Allocator.Persistent,NativeArrayOptions.UninitializedMemory); //边数是不能变的
            RotateVertexs(false);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override ColliderStructure GetRealCollider()
        {
            return CollisionManager.instance.nativeCollisionManager.colliders[collider.uniqueID];
        }

        public sealed override void SaveState()
        {
            base.SaveState();
            CollisionManager.instance.nativeCollisionManager.ModifyCollider(collider,movedVertexs);
        }

        public override void SetRotation(FP rotRad)
        {
            collider.rot = rotRad;
            RotateVertexs();
        }//没错，圆形没有旋转
        public override void SetCenter(in TSVector2 center)
        {
            MoveDeltaPos(center-collider.center);
            collider.center = center;
        }
        public override TSVector2 GetCenter()
        {
            return collider.center;
        }
        protected void RotateVertexs(bool saveState=true)
        {
            TSVector4 tSVector4 = new TSVector4(FP.MaxValue, FP.MinValue, FP.MinValue, FP.MaxValue);
            for (int i = movedVertexs.Length - 1; i >= 0; --i)
            {
                movedVertexs[i] = vertexs[i].RotateRad(collider.rot) + collider.center;
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
            if (saveState)
            {
                SaveState();
            }
        }
        void MoveDeltaPos(TSVector2 pos)
        {
            for (int i = movedVertexs.Length - 1; i >= 0; --i)
            {
                movedVertexs[i] += pos;
            }
            //标注顶点缓冲区的移动
            SaveState();
            _boundingBox_.x += pos.x;
            _boundingBox_.y += pos.y;
            _boundingBox_.z += pos.x;
            _boundingBox_.w += pos.y;
        }
        /*public override TSVector2 GetFurthestPoint(in TSVector2 _direction)
        {
            TSVector2 result = base.GetFurthestPoint(_direction);
            var direction = _direction.normalized;
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
            if (result != pos)
            {
                var arr = ColliderNativeHelper.instancedBuffer.ToArray();
                StringBuilder sb = new StringBuilder();
                sb.AppendJoin(", ", arr);
                Debug.Log(result+" "+pos+" "+sb);
            }
            return pos;
        }*/
        public override void Destroy()
        {
            CollisionManager.instance.nativeCollisionManager.DeleteCollider(collider);
            if(movedVertexs.IsCreated)
                movedVertexs.Dispose();
        }

        public override TSVector4 GetBoundingBox()
        {
            return _boundingBox_;
        }
    }
    [Serializable]
    public class BoxCollider : PolygonCollider
    {
        public BoxCollider(TSVector2 widthHeight,TSVector2 center,FP rotation, CollisionGroup group = CollisionGroup.Default)
        {
            TSVector2 a = widthHeight * 0.5;//左下，左上，右上，右下
            TSVector2 b = new TSVector2(a.x,-a.y);
            vertexs = new TSVector2[4] {-a,-b, a, b}; //边数是不能变的
            movedVertexs = new NativeArray<TSVector2>(4,Allocator.Persistent,NativeArrayOptions.UninitializedMemory); //边数是不能变的
            collider = ColliderStructure.CreateInstance();
            collider.colliderType = ColliderType.Polygon;
            collider.rot = rotation;
            collider.center = center;
            collider.vertexCount = vertexs.Length;
            collider.collisionGroup = group;
            RotateVertexs(false);
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

        public BoxCollider(T master, TSVector2 widthHeight, TSVector2 center, FP rotation, CollisionGroup group = CollisionGroup.Default) : base(widthHeight, center, rotation, group)
        {
            this._master_ = master;
        }

    }
    [Serializable]
    public class DiamondCollider : PolygonCollider
    {
        public DiamondCollider(TSVector2 widthHeight, TSVector2 center, FP rotation, CollisionGroup group = CollisionGroup.Default)
        {
            TSVector2 b = new TSVector2(widthHeight.x * 0.5, 0);
            TSVector2 c = new TSVector2(0, widthHeight.y * 0.5);
            vertexs = new TSVector2[4] { -b, c, b, -c }; //边数是不能变的
            movedVertexs = new NativeArray<TSVector2>(4,Allocator.Persistent,NativeArrayOptions.UninitializedMemory); //边数是不能变的
            collider = ColliderStructure.CreateInstance();
            collider.colliderType = ColliderType.Polygon;
            collider.rot = rotation;
            collider.center = center;
            collider.vertexCount = vertexs.Length;
            collider.collisionGroup = group;
            RotateVertexs(false);
        }

    }
    [Serializable]
//两个相同的圆中间连线
    public class DoubleCircleCollider : ColliderBase
    {
        public DoubleCircleCollider(FP R, TSVector2 center1,TSVector2 center2, CollisionGroup group = CollisionGroup.Default)
        {
            collider = ColliderStructure.CreateInstance();
            collider.colliderType = ColliderType.DoubleCircle;
            collider.radius = R;
            collider.collisionGroup = group;
            SetCircleCenters(center1,center2);
        }
        public override void SetRotation(FP rotRad) { }//没错，圆形没有旋转
        public override void SetCenter(in TSVector2 center)
        {
            TSVector2 delta = center - collider.center;
            collider.circleCenter1 += delta;
            collider.circleCenter2 += delta;
            collider.center = center;
        }
        public void SetCircleCenters(in TSVector2 center1,in TSVector2 center2)
        {
            collider.circleCenter1 = center1;
            collider.circleCenter2 = center2;
            collider.center = (center1 + center2) * 0.5;
        }
        public void SetCircleCenter2(in TSVector2 center2)
        {
            collider.circleCenter2 = center2;
            collider.center = (collider.circleCenter1 + center2) * 0.5;
        }
        public void SetCircleCenter1(in TSVector2 center1)
        {
            collider.circleCenter1 = center1;
            collider.center = (collider.circleCenter2 + center1) * 0.5;
        }
        public override TSVector2 GetCenter()
        {
            return collider.center;
        }
        /*public override TSVector2 GetFurthestPoint(TSVector2 direction)
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
        }*/
        public override TSVector4 GetBoundingBox()
        {
            FP minX;
            FP minY;
            FP maxX;
            FP maxY;
            if (collider.circleCenter1.x<collider.circleCenter2.x) {
                minX = collider.circleCenter1.x;
                maxX = collider.circleCenter2.x;
            }
            else
            {
                maxX = collider.circleCenter1.x;
                minX = collider.circleCenter2.x;
            }
            if (collider.circleCenter1.y < collider.circleCenter2.y)
            {
                minY = collider.circleCenter1.y;
                maxY = collider.circleCenter2.y;
            }
            else
            {
                maxY = collider.circleCenter1.y;
                minY = collider.circleCenter2.y;
            }

            var r = collider.radius;
            return new TSVector4(minX - r, maxY + r, maxX + r, minY - r);
        }
        /*public bool CircleCollideWithCircle(CircleCollider shape2)
        {
            TSVector2 centerShape = shape2.GetCenter();
            TSVector2 deltaPos = centerShape-centerPos;
            TSVector2 delta = (collider.circleCenter2 - centerCircle1);
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
        }*/
        public bool CircleCollideWithCircle(CircleCollider shape2)
        {
            return CollideExtensions.CircleCollideWithDoubleCircle(this.collider, shape2.collider);
        }

        /*public override bool CheckCollide(ColliderBase shape2)
        {
            if (shape2 is CircleCollider sh)
            {
                return CircleCollideWithCircle(sh);
            }
            return CheckCollide(this, shape2);
        }*/
        public override void DebugDisplayColliderShape(Color color)
        {
            int circleCount = Mathf.FloorToInt(Mathf.Lerp(50, 80, ((float)collider.radius) / 72f));
            float angleDelta = 2 * Mathf.PI / circleCount;

            GL.Begin(GL.TRIANGLE_STRIP);
            GL.Color(color);

            for (int i = 0; i < circleCount + 1; i++)
            {
                float angle = angleDelta * i;
                float angleNext = angle + angleDelta;
                Vector3 cent = collider.circleCenter1.ToVector();
                Vector3 cent2 = new Vector3(Mathf.Cos(angle) * (float)collider.radius, Mathf.Sin(angle) * (float)collider.radius, 0) + cent;
                GL.Vertex3(cent2.x, cent2.y, cent2.z);
                GL.Vertex3(cent.x, cent.y, cent.z);
            }
            for (int i = 0; i < circleCount + 1; i++)
            {
                float angle = angleDelta * i;
                float angleNext = angle + angleDelta;
                Vector3 cent = collider.circleCenter2.ToVector();
                Vector3 cent2 = new Vector3(Mathf.Cos(angle) * (float)collider.radius, Mathf.Sin(angle) * (float)collider.radius, 0) + cent;
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

        public DoubleCircleCollider(T master, FP R, TSVector2 center1,TSVector2 center2, CollisionGroup group = CollisionGroup.Default) : base(R, center1,center2, group)
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
        public CollisionController AddListener(bool multiColli,Action<ColliderBase> collideEnter, Action<ColliderBase> collide, Action<ColliderBase> collideLeave, params CollisionGroup[] collisionGroups)
        {
            if (DestroyedChecker()) return this;//销毁后不允许再增加
            foreach (var item in Colliders)
            {
                CollisionManager.instance.AddListener(item, collideEnter, collide, collideLeave, collisionGroups).multiColli=multiColli;
            }
            return this;
        }
        public CollisionController AddListener(Action<ColliderBase> collideEnter, Action<ColliderBase> collide, Action<ColliderBase> collideLeave,params CollisionGroup[] collisionGroups) {
            if (DestroyedChecker()) return this;//销毁后不允许再增加
            foreach (var item in Colliders)
            {
                CollisionManager.instance.AddListener(item, collideEnter, collide, collideLeave, collisionGroups);
            }
            return this;
        }
        public CollisionController AddListener(Action<ColliderBase> collide, params CollisionGroup[] groups) { 
            return AddListener(null,collide,null,groups);
        }
        public CollisionController AddListener(Action<ColliderBase> collide, bool multiColli=false, params CollisionGroup[] groups)
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
    public class CollisionManager:IDisposable {
        public static void voidFunction(ColliderBase c) { }
        private static CollisionManager _instance;

        public static CollisionManager instance
        {
            get => _instance;
            set
            {
                if (_instance != null&&_instance!=value)
                {
                    _instance.Dispose();
                }
                _instance = value;
            }
        }

        public void Dispose()
        {
            foreach (var colliderBase in colliders)
            {
                if (colliderBase is IDisposable c)
                {
                    c.Dispose();
                }
            }
            nativeCollisionManager.Dispose();
        }
        public int groupCnt = Enum.GetValues(typeof(CollisionGroup)).Length;
        public LinkedHashSet<ColliderBase>[] groupedColliders;//这个到时候要改成LinkedHashSet之类的东西。。。
        public HashSet<ColliderBase> colliders = new HashSet<ColliderBase>();
        public LinkedDictionary<ColliderBase, LinkedPooledHashSet<__action_checkColli__>> listeners = new LinkedDictionary<ColliderBase, LinkedPooledHashSet<__action_checkColli__>>();
        public LinkedDictionary<ColliderBase, LinkedDictionary<CollisionGroup,__action_checkColli__>> receivers = new LinkedDictionary<ColliderBase, LinkedDictionary<CollisionGroup, __action_checkColli__>>();

        public HashSet<ColliderBase> tmpDrawingHasCheckedObjectsInCurFrame = new HashSet<ColliderBase>();//用来debug有哪些物体当前帧被检查碰撞
        //readonly bool multiCollisionOptimize = false;//先关掉多碰撞优化，测试功能
        private Material _shapeMaterial;//测试
        public ColliderNativeHelper nativeCollisionManager = new ColliderNativeHelper();
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
            if (collider.collider.colliderType == ColliderType.Polygon)
            {
                nativeCollisionManager.RegisterCollider(ref collider.collider,collider.collider.vertexCount);
            }
            else
            {
                nativeCollisionManager.RegisterCollider(ref collider.collider);
            }
            collider.Registered = true;
            collider.SaveState();
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
            nativeCollisionManager.DeleteCollider(collider.collider);
            colliders.Remove(collider);
            //调用销毁清理函数
            collider.Destroy();
            collider.Registered = false;
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