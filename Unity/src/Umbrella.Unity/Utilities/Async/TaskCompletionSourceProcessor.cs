﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Umbrella.Unity.Utilities.Async
{
    public class TaskCompletionSourceProcessor : MonoBehaviour, ITaskCompletionSourceProcessor
    {
        public static TaskCompletionSourceProcessor Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            var go = new GameObject(nameof(TaskCompletionSourceProcessor));
            Instance = go.AddComponent<TaskCompletionSourceProcessor>();
        }

        private bool m_IsRunning;
        private readonly object m_Result = new object();
        private readonly List<(TaskCompletionSource<object> Tcs, Func<bool> CompletionTestFunc)> m_TaskCompletionSourceList = new List<(TaskCompletionSource<object> Tcs, Func<bool> CompletionTestFunc)>();

        private void Start()
            => DontDestroyOnLoad(gameObject);

        public void Enqueue(TaskCompletionSource<object> source, Func<bool> completionTestFunc)
        {
            m_TaskCompletionSourceList.Add((source, completionTestFunc));

            if (!m_IsRunning)
            {
                InvokeRepeating(nameof(ProcessOutstandingItems), 0.25f, 0.25f);
                m_IsRunning = true;
            }
        }

        private void ProcessOutstandingItems()
        {
            for (int i = 0; i < m_TaskCompletionSourceList.Count; i++)
            {
                var (tcs, completedFunc) = m_TaskCompletionSourceList[i];

                if (completedFunc != null && completedFunc())
                {
                    tcs.SetResult(m_Result);
                    m_TaskCompletionSourceList.RemoveAt(i--);
                }
            }

            if (m_TaskCompletionSourceList.Count == 0)
            {
                CancelInvoke(nameof(ProcessOutstandingItems));
                m_IsRunning = false;
            }
        }
    }
}