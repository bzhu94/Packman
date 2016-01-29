﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace Packman
{
    // Inspried by Hanselman's post http://www.hanselman.com/blog/ComparingTwoTechniquesInNETAsynchronousCoordinationPrimitives.aspx
    // based on Toub's series: http://blogs.msdn.com/b/pfxteam/archive/2012/02/12/10266988.aspx
    public sealed class AsyncLock : IDisposable
    {
        private readonly SemaphoreSlim m_semaphore = new SemaphoreSlim(1, 1);
        private readonly Task<IDisposable> m_releaser;

        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        public AsyncLock()
        {
            m_releaser = Task.FromResult(new Releaser(this) as IDisposable);
        }

        public Task<IDisposable> LockAsync()
        {
            var wait = m_semaphore.WaitAsync();
            return wait.IsCompleted ?
                        m_releaser :
                        wait.ContinueWith((_, state) => (IDisposable)state,
                            m_releaser.Result, CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        void Dispose(bool disposing)
        {
            if (disposing)
                m_semaphore.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        sealed class Releaser : IDisposable
        {
            readonly AsyncLock m_toRelease;
            internal Releaser(AsyncLock toRelease) { m_toRelease = toRelease; }
            public void Dispose() { m_toRelease.m_semaphore.Release(); }
        }
    }
}
