using System;
using System.Collections.Generic;
using System.Threading;
namespace Sokoban.Runtime.Platform.Tasks
{
    public sealed class MainThreadDispatcher
    {
        readonly int ownerThread = Thread.CurrentThread.ManagedThreadId;
        readonly object gate = new object();
        readonly Dictionary<object, Action> pending = new Dictionary<object, Action>();
        public int PendingCount { get { lock (gate) return pending.Count; } }
        public void AssertOwner()
        { if (Thread.CurrentThread.ManagedThreadId != ownerThread) throw new InvalidOperationException("界面任务只能在所属主线程提交。"); }
        public void PostLatest(object owner, Action action)
        {
            if (owner == null || action == null) throw new ArgumentNullException();
            lock (gate)
            {
                if (!pending.ContainsKey(owner) && pending.Count >= 64) throw new InvalidOperationException("主线程待处理任务已达到上限。");
                pending[owner] = action;
            }
        }
        public void Remove(object owner) { lock (gate) pending.Remove(owner); }
        public void Drain()
        {
            AssertOwner(); Action[] work;
            lock (gate) { work = new Action[pending.Count]; pending.Values.CopyTo(work, 0); pending.Clear(); }
            foreach (var action in work) action();
        }
    }
}
