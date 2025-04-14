using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Core.Algorithm;
using TrueSync;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using ZeroAs.DOTS.Colliders;

namespace ZeroAs.DOTS.Colliders.Jobs
{
    
    

    [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
    public struct CollisionGroupHashSetToArrayManager:IDisposable
    {

        private NativeReference<byte> _destroyFlag;
        private byte _isCreated;

        private bool convertIsCreateBool
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _isCreated != 0;
        }
        public bool IsCreated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                #if UNITY_EDITOR
                if (_destroyFlag.IsCreated!=convertIsCreateBool)
                {
                    throw new InvalidOperationException("忘记释放CollisionGroupHashSetToArrayManager内存");
                }
                #endif

                return convertIsCreateBool;
            }
        }

        public bool Available
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
#if UNITY_EDITOR
                if (_destroyFlag.IsCreated!=convertIsCreateBool)
                {
                    throw new InvalidOperationException("忘记释放CollisionGroupHashSetToArrayManager内存");
                }
#endif

                return _destroyFlag.IsCreated;
            }
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct ColliGroupJobData
        {
            public byte created;
            public JobHandle handle;
        }
        //可以在其他的代码中向这里Complete来保证所有任务完成
        public NativeList<JobHandle> _allHandles;
        //这里是
        public NativeArray<ColliGroupJobData> _collisionGroupInfos;
        public NativeArray<ConvertCheckGroupToArrayJob> _collisionGroupObjs;

        private NativeArray<NativeHashSet<int>>.ReadOnly useOutSideHashSets;
        public NativeArray<NativeHashSet<int>>.ReadOnly GroupedColliders => useOutSideHashSets;
        // 初始化方法
        public CollisionGroupHashSetToArrayManager(NativeArray<NativeHashSet<int>>.ReadOnly hashset)
        {
            _isCreated = 1;
            _allHandles = new NativeList<JobHandle>(CollisionManager.groupCnt, Allocator.TempJob);
            _collisionGroupInfos = new NativeArray<ColliGroupJobData>(CollisionManager.groupCnt, Allocator.TempJob);
            _collisionGroupObjs = new NativeArray<ConvertCheckGroupToArrayJob>(CollisionManager.groupCnt, Allocator.TempJob);
            useOutSideHashSets = hashset;
            _destroyFlag=new NativeReference<byte>(Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining),BurstCompile(OptimizeFor = OptimizeFor.Performance)]
        public static void GetJobData(
            ref NativeArray<ColliGroupJobData> _collisionGroupInfos,
            ref NativeArray<ConvertCheckGroupToArrayJob> _collisionGroupObjs,
            in NativeArray<NativeHashSet<int>>.ReadOnly useOutSideHashSets,
            ref NativeList<JobHandle> _allHandles,
            in int index,
            out ColliGroupJobData data
            )
        {
            //避免重复获取job，如果已经有正在执行中的复制job则获取那个就行
            if (_collisionGroupInfos[index].created != 0)
            {
                data = _collisionGroupInfos[index];
                return;
            }
            var job = new ColliGroupJobData();
            job.created = (byte)1;
            var getCollisionArrayJob = new ConvertCheckGroupToArrayJob();
            //自动创建Hashset转数组的job并添加到资源里面
            getCollisionArrayJob.checkGroupArray = new NativeArray<int>(useOutSideHashSets[index].Count, Allocator.TempJob);
            getCollisionArrayJob.checkGroup = useOutSideHashSets[index].AsReadOnly();
            job.handle = getCollisionArrayJob.Schedule();
            _allHandles.Add(job.handle);
            _collisionGroupObjs[index] = getCollisionArrayJob;
            data = _collisionGroupInfos[index] = job;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ColliGroupJobData EnsureJobData(int index)
        {
            GetJobData(
                ref _collisionGroupInfos,
                ref _collisionGroupObjs,
                in useOutSideHashSets,
                ref _allHandles,
                index, 
                out ColliGroupJobData data
            );
            return data;
        }
        
        // 清理方法
        public void Dispose()
        {
            _isCreated = 0;
            _collisionGroupInfos.Dispose();
            _allHandles.Dispose();
            for (int i = 0; i < _collisionGroupObjs.Length; i++)
            {
                _collisionGroupObjs[i].checkGroupArray.Dispose();
            }
            _collisionGroupObjs.Dispose();
            if(_destroyFlag.IsCreated) _destroyFlag.Dispose();
            //hashset是在外面控制的
        }
    }

    [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
    public struct CollisionChecker
    {
        private const int batchSingleCount = 64;
        #region 一对多，并且返回最小的id那个
        /// <summary>
        /// 查找一对多的碰撞体，并尝试获取最小的ID，请在主线程里面调用，之后统一获取到结果之后请调用PostProcess结果来获取最终结果
        /// </summary>
        /// <param name="minResults_ret"></param>
        /// <param name="waitHandle"></param>
        /// <param name="toArrayManager"></param>
        /// <param name="groupIndex"></param>
        /// <param name="targetCollider"></param>
        /// <param name="allColliders"></param>
        /// <param name="vertexBuffer"></param>
        /// <param name="allocator"></param>
        [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
        public static void FindMinCollidingId(
            out NativeArray<int> minResults_ret,
            out JobHandle waitHandle,
            
            ref CollisionGroupHashSetToArrayManager toArrayManager,
            in int groupIndex,
            in ColliderStructure targetCollider,
            in NativeHashMap<int, ColliderStructure>.ReadOnly allColliders,
            in NativeArray<TSVector2>.ReadOnly vertexBuffer,
            Allocator allocator = Allocator.TempJob)
        {
            // 第一步：转换HashSet为数组
            toArrayManager.EnsureJobData(groupIndex);
            // 第二步：准备分块处理
            int elementCount = toArrayManager.GroupedColliders[groupIndex].Count;
            int batchCount = batchSingleCount;//Unity.Mathematics.math.max(1, elementCount / 32);
            int batchSize = nextPow2(elementCount / batchCount);
            batchCount = (elementCount+batchSize-1) / batchSize;
            var minResults = new NativeArray<int>(
                batchCount,
                allocator,
                NativeArrayOptions.UninitializedMemory);

            var handles = new NativeList<JobHandle>(batchCount, allocator);

            // 分块调度作业
            for (int i = 0; i < batchCount; i++)
            {
                int start = i * batchSize;
                int end = Unity.Mathematics.math.min((start+batchSize),elementCount);

                var checkJob = new CollisionCheckJob
                {
                    checkGroupArray = toArrayManager._collisionGroupObjs[groupIndex].checkGroupArray,
                    allColliders = allColliders,
                    vertexBuffer = vertexBuffer,
                    targetCollider = targetCollider,
                    startIndex = start,
                    endIndex = end,
                    resultIndex = i,
                    minResults = minResults
                };
                
                handles.Add(checkJob.Schedule(toArrayManager._collisionGroupInfos[groupIndex].handle));
            }

            minResults_ret = minResults;
            waitHandle = JobHandle.CombineDependencies(handles.AsArray());
            handles.Dispose();
        }
        /// <summary>
        /// 请在主线程中调用
        /// </summary>
        /// <param name="minResults"></param>
        /// <returns></returns>
        [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
        public static int PostProcessMinResults(ref NativeArray<int> minResults)
        {
            // 第三步：收集最终结果
            int finalResult = int.MaxValue;
            int len = minResults.Length;
            //Debug.Log("Length: "+len);
            for (int i = 0; i < len; i++)
            {
                //Debug.Log("WhatIsMin: "+minResults[i]);
                finalResult = Unity.Mathematics.math.min(finalResult, minResults[i]);
            }
            minResults.Dispose();
            return finalResult;
        }
        #endregion
        [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
        static int nextPow2(int n)
        {
            n--;
            n |= n >> 1;
            n |= n >> 2;
            n |= n >> 4;
            n |= n >> 8;
            n |= n >> 16;
            n++;
            return n == 0 ? 1 : n;
        }
        #region 一对多，并且返回排完序后的数组
        [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
        public static void FindAllCollidingIds(
            out NativeQueue<int> resultsQueue,
            out JobHandle waitHandle,
            ref CollisionGroupHashSetToArrayManager toArrayManager,
            in int groupIndex,
            in ColliderStructure targetCollider,
            in NativeHashMap<int, ColliderStructure>.ReadOnly allColliders,
            in NativeArray<TSVector2>.ReadOnly vertexBuffer,
            Allocator allocator = Allocator.TempJob)
        {
            // 确保HashSet转Array任务完成
            var groupData = toArrayManager.EnsureJobData(groupIndex);
        
            // 创建结果队列
            resultsQueue = new NativeQueue<int>(allocator);
        
            // 准备Job参数
            var job = new SingleToManyCollisionJob
            {
                targetCollider = targetCollider,
                CheckGroup = toArrayManager._collisionGroupObjs[groupIndex].checkGroupArray,
                AllColliders = allColliders,
                VertexBuffer = vertexBuffer,
                Results = resultsQueue.AsParallelWriter()
            };
            int totalLen = toArrayManager._collisionGroupObjs[groupIndex].checkGroup.Count;
            // 调度并行任务
            waitHandle = job.ScheduleParallel(
                totalLen, 
                batchSingleCount,//nextPow2(totalLen/32+1), 
                groupData.handle);
        }

        [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
        public static void PostProcessAllResults(
            out NativeArray<int> resArray,
            ref NativeQueue<int> resultsQueue,
            Allocator allocator = Allocator.Temp,
            bool sort = true)
        {
            // 转换队列为数组
            NativeArray<int> resultArray = resultsQueue.ToArray(allocator);
            resultsQueue.Dispose();

            // 排序（可选）
            if (sort && resultArray.Length > 1)
            {
                resultArray.Sort();
            }

            resArray = resultArray;
        }
        

        #endregion
    }

    [BurstCompile]
    public struct ConvertCheckGroupToArrayJob : IJob
    {
        [ReadOnly] public NativeHashSet<int>.ReadOnly checkGroup;
        [WriteOnly] public NativeArray<int> checkGroupArray;

        public void Execute()
        {
            checkGroupArray.CopyFrom(checkGroup.ToNativeArray(Allocator.Temp));
        }
    }

    [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
    public struct CollisionCheckJob : IJob
    {
        [ReadOnly] public NativeArray<int> checkGroupArray;
        [ReadOnly] public NativeHashMap<int, ColliderStructure>.ReadOnly allColliders;
        [ReadOnly] public NativeArray<TSVector2>.ReadOnly vertexBuffer;
        [ReadOnly] public ColliderStructure targetCollider;

        [ReadOnly]public int startIndex;
        [ReadOnly]public int endIndex;
        [ReadOnly]public int resultIndex;

        [NativeDisableContainerSafetyRestriction] public NativeArray<int> minResults;

        public void Execute()
        {
            int localMin = int.MaxValue;
            for (int i = startIndex; i < endIndex; i++)
            {
                int otherId = checkGroupArray[i];
                if (!allColliders.TryGetValue(otherId, out var otherCollider))
                {
                    continue;
                }
                if (otherCollider.enabled == 0)
                {
                    continue;
                }

                if (CollideExtensions.CheckCollide(
                    targetCollider, 
                    otherCollider, 
                    vertexBuffer))
                {
                    localMin = Unity.Mathematics.math.min(localMin, otherId);
                }
            }

            minResults[resultIndex] = localMin;
        }
    }
    [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
    public struct SingleToManyCollisionJob : IJobFor
    {
        [ReadOnly] public ColliderStructure targetCollider;
        [ReadOnly] public NativeArray<int> CheckGroup;
        [ReadOnly] public NativeHashMap<int, ColliderStructure>.ReadOnly AllColliders;
        [ReadOnly] public NativeArray<TSVector2>.ReadOnly VertexBuffer;
    
        [WriteOnly] 
        public NativeQueue<int>.ParallelWriter Results;

        public void Execute(int index)
        {
            int otherID = CheckGroup[index];
            if (!AllColliders.TryGetValue(otherID, out ColliderStructure otherCollider))
                return;
            if (otherCollider.enabled == 0)
            {
                return;
            }
            if (CollideExtensions.CheckCollide(targetCollider, otherCollider, VertexBuffer))
            {
                Results.Enqueue(otherID);
            }
        }
    }
}