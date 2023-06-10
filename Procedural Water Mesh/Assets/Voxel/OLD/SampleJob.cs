using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;

public class SampleJob : MonoBehaviour
{
    [SerializeField] private bool useJobs;
    [SerializeField] private float lastTime;

    private void Update()
    {
        var timer = new System.Diagnostics.Stopwatch();
        timer.Start();

        float startTime = Time.realtimeSinceStartup;

        if (useJobs)
        {
            NativeList<JobHandle> handles = new NativeList<JobHandle>(Allocator.Temp);

            for (int i = 0; i < 10; i++)
            {
                var job = new TaskJob();
                handles.Add(job.Schedule());
            }

            JobHandle.CompleteAll(handles);

            handles.Dispose();
        }
        else
        {
            for (int i = 0; i < 10; i++)
            {
                Task();
            }
        }

        //print("Task took " + timer.ElapsedMilliseconds + " mms");
        //lastTime = timer.ElapsedMilliseconds * 100;
        lastTime = Time.realtimeSinceStartup - startTime;
        lastTime *= 1000;

        NativeList<int> indices = new NativeList<int>(Allocator.TempJob);

        var listJob = new ListJob()
        {
            output = indices.AsParallelWriter(),
        };

        listJob.Schedule(10, 2).Complete();

        print(indices.Length);

        if (Input.GetKey(KeyCode.U))
        {
            for (int i = 0; i < indices.Length; i++)
            {
                print(indices[i]);
            }
        }     

        indices.Dispose();
    }

    private void Task()
    {
        float value = 0f;

        for (int i = 0; i < 1000; i++)
        {
            value = Mathf.Exp(Mathf.Sqrt(value));
        }
    }

    [BurstCompile]
    public struct TaskJob : IJob
    {
        public void Execute()
        {
            float value = 0f;

            for (int i = 0; i < 1000; i++)
            {
                value = Mathf.Exp(Mathf.Sqrt(value));
            }
        }
    }

    [BurstCompile]
    public struct ListJob : IJobParallelFor
    {

        public NativeList<int>.ParallelWriter output;
        public void Execute(int index)
        {
            output.AddNoResize(index);

            //output.Add(index);
        }
    }
}
