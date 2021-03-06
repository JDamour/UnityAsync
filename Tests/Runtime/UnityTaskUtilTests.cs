﻿using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gameframe.Async.Tests
{
    public class UnityTaskUtilTests
    {
        [Test]
        public void CurrentThreadIsUnityThread_True()
        {
            //Should be running on UnityThread
            Assert.IsTrue(UnityTaskUtil.CurrentThreadIsUnityThread);
        }

        [Test]
        public void CurrentThreadIsUnityThread_False()
        {
            //Make sure that we start on the Unity thread 
            Assert.IsTrue(UnityTaskUtil.CurrentThreadIsUnityThread,"Needs to start on the UnitySyncContext");
            
            //Task.Run should run off the Unity thread
            var task = Task.Run(() => UnityTaskUtil.CurrentThreadIsUnityThread);
            Assert.IsFalse(task.Result);
        }

        [UnityTest]
        public IEnumerator RunOnUnityThread()
        {
            bool checkOne = false;
            bool checkTwo = false;
            
            //Post the first check to the context
            UnityTaskUtil.RunOnUnityThread(() => { checkOne = UnityTaskUtil.CurrentThreadIsUnityThread; });
            //Wait a frame so it has a chance to run
            yield return null;

            var task = Task.Run(() =>
            {
                UnityTaskUtil.RunOnUnityThread(() => { checkTwo = UnityTaskUtil.CurrentThreadIsUnityThread; });
            });
            
            //Wait for the task to run and post the function to the context
            task.Wait();
            //Wait a frame for the posted function to get called on the unity context
            yield return null;
            
            Assert.IsTrue(checkOne);
            Assert.IsTrue(checkTwo);
        }
        
        [UnityTest]
        public IEnumerator RunOnUnityThreadAsync()
        {
            var task1 = UnityTaskUtil.RunOnUnityThreadAsync(() => UnityTaskUtil.CurrentThreadIsUnityThread);
            Assert.IsTrue(task1.Result);

            var task2 = Task.Run(() =>
            {
                return UnityTaskUtil.RunOnUnityThreadAsync(() => UnityTaskUtil.CurrentThreadIsUnityThread ).Result;
            });
            //Need to do a coroutine here because task2 waits on the result of the thing running on the main thread
            //so we can't block the main thread till it's done
            yield return new WaitUntil(()=>task2.IsCompleted);
            Assert.IsTrue(task2.Result);
        }

        [UnityTest]
        public IEnumerator InstantiateAsync()
        {
            var prefab = new GameObject();
            GameObject clone = null;
            
            //Call instantiate from background thread
            var task = Task.Run(() =>
            {
                clone = UnityTaskUtil.InstantiateAsync(prefab).Result; 
            });

            yield return new WaitUntil(() => task.IsCompleted);

            Assert.IsTrue(clone != null);
        }
        
        [UnityTest]
        public IEnumerator InstantiateAsync_WithParent()
        {
            var prefab = new GameObject();
            var parent = new GameObject().transform;
            GameObject clone = null;
            
            //Call instantiate from background thread
            var task = Task.Run(() =>
            {
                clone = UnityTaskUtil.InstantiateAsync(prefab,parent).Result; 
            });

            yield return new WaitUntil(() => task.IsCompleted);

            Assert.IsTrue(clone != null && clone.transform.parent == parent);
        }

        [UnityTest]
        public IEnumerator StartCoroutineAsync()
        {
            var testHost = new GameObject().AddComponent<TestHostBehaviour>();
            int count = 0;
            int countTo = 3;
            
            var task = Task.Run(() =>
            {
                var coroutine = UnityTaskUtil.StartCoroutineAsync(TestCoroutine(() =>
                {
                    count++;
                    return count < countTo;
                }),testHost);
                //Wait for coroutine to finish
                coroutine.GetAwaiter().GetResult();
            });

            yield return new WaitUntil(() => task.IsCompleted);
            Assert.IsTrue(count >= countTo);
        }

        [UnityTest]
        public IEnumerator StartCancelableCoroutine()
        {
            var testHost = new GameObject().AddComponent<TestHostBehaviour>();
            var cancelTokenSource = new CancellationTokenSource();
            var token = cancelTokenSource.Token;
            
            var task = Task.Run(() =>
            {
                var coroutine = UnityTaskUtil.StartCancelableCoroutineAsync(TestForeverCoroutine(), testHost, token);
                coroutine.GetAwaiter().GetResult();
            });

            yield return null;
            
            Assert.IsFalse(task.IsCompleted);

            cancelTokenSource.Cancel();

            yield return null;

            Assert.IsTrue(task.IsCompleted);
        }

        [Test]
        public void UnityTaskFactory_NotNull()
        {
            Assert.IsTrue(UnityTaskUtil.UnityTaskFactory != null);
        }

        [Test]
        public void UnityThreadId_IsMainThreadId()
        {
            Assert.IsTrue(UnityTaskUtil.UnityThreadId == System.Threading.Thread.CurrentThread.ManagedThreadId);
        }

        [Test]
        public void UnitySynchronizationContext_NotNull()
        {
            Assert.IsTrue(UnityTaskUtil.UnitySynchronizationContext != null);
        }

        [Test]
        public void UnityTaskScheduler_NotNull()
        {
            Assert.IsTrue(UnityTaskUtil.UnityTaskScheduler != null);
        }
        
        private static IEnumerator TestCoroutine(Func<bool> action)
        {
            while (action.Invoke())
            {
                yield return null;
            }
        }

        private static IEnumerator TestForeverCoroutine()
        {
            while (true)
            {
                yield return null;
            }
        }
        
        public class TestHostBehaviour : MonoBehaviour
        {
        }
    }
}
