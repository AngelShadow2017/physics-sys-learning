using System;
using System.Collections.Generic;
using TrueSync;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
[BurstCompile(DisableDirectCall = true)]
public struct testJob : IJobParallelFor
{
    public NativeArray<someObjectsToStruct> values;
    public void Execute(int index)
    {
        var tmp = values[index];
            tmp.center += TSVector2.left;
            values[index] = tmp;
    }
}

public class someObjects
{
    public TSVector2 center;

    public someObjectsToStruct ToStruct()
    {
        return new someObjectsToStruct()
        {
            center = center
        };
    }
}

public struct someObjectsToStruct
{
    public TSVector2 center;
}
public class JobsTesting : MonoBehaviour
{
    List<someObjects> objs = new List<someObjects>();

    private void Start()
    {
        for (int i = 0; i < 500000; i++)
        {
            var o = new someObjects();
            o.center = Mathz.UnitVector(360*((FP)UnityEngine.Random.value))*(FP)UnityEngine.Random.value*100;
            objs.Add(o);
        }
    }

    void BurstUpdate()
    {
        int len = objs.Count;
        var job = new testJob();
        var values = new NativeArray<someObjectsToStruct>(len, Allocator.TempJob);
        for (int i = 0; i < len; i++)
        {
            values[i] = objs[i].ToStruct();
        }
        job.values = values;
        JobHandle handle = job.Schedule(values.Length, 64);
        // 等待作业完成
        handle.Complete();
        for (int i = 0; i < len; i++)
        {
            objs[i].center = values[i].center;
        }
        values.Dispose();
    }

    void NormalUpdate()
    {
        int len = objs.Count;
        for (int i = 0; i < len; i++)
        {
            objs[i].center += TSVector2.left;
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Update()
    {
        BurstUpdate();
    }

}
