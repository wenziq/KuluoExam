using System;
using System.Threading;
using System.Threading.Tasks;
namespace Sokoban.Runtime.Platform.Tasks
{
    /// <summary>All searches, reference replays and production analysis share one worker slot.</summary>
    public static class AnalysisWorker
    {
        static readonly SemaphoreSlim Slot=new SemaphoreSlim(1,1);
        public static Task<T> RunAsync<T>(Func<CancellationToken,T> work,CancellationToken token)
        {
            return Task.Run(async()=>
            {
                await Slot.WaitAsync(token).ConfigureAwait(false);
                try{token.ThrowIfCancellationRequested();return work(token);}
                finally{Slot.Release();}
            },token);
        }
    }
}
