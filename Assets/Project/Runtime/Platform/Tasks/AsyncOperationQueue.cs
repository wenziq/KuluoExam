using System;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace Sokoban.Runtime.Platform.Tasks
{
    internal static class AsyncOperationQueue
    {
        static readonly object Gate=new object();
        static readonly Dictionary<string,TaskCompletionSource<bool>> Tails=new Dictionary<string,TaskCompletionSource<bool>>(StringComparer.OrdinalIgnoreCase);
        public static Task<T> Run<T>(string key,Func<Task<T>> operation)
        {
            Task predecessor;var completion=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock(Gate){predecessor=Tails.TryGetValue(key,out var tail)?tail.Task:Task.CompletedTask;Tails[key]=completion;}
            return Execute();
            async Task<T> Execute()
            {
                await predecessor.ConfigureAwait(false);
                try{return await operation().ConfigureAwait(false);}
                finally
                {
                    lock(Gate){if(Tails.TryGetValue(key,out var tail)&&ReferenceEquals(tail,completion))Tails.Remove(key);completion.TrySetResult(true);}
                }
            }
        }
    }
}
