using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>Cooperatively drives at most five independent download coroutines on Unity's main thread.
/// Jobs may yield nested enumerators, request operations or null; no playback state is shared.</summary>
public sealed class VoiceDownloadBatch : IDisposable
{
    public const int Concurrency = 5;
    readonly Queue<IEnumerator> pending;
    readonly List<Slot> active = new List<Slot>();
    public bool Cancelled { get; private set; }
    public Exception Error { get; private set; }
    public int Completed { get; private set; }

    public VoiceDownloadBatch(IEnumerable<IEnumerator> jobs) { pending = new Queue<IEnumerator>(jobs); }

    public IEnumerator Run()
    {
        try
        {
            while (!Cancelled && (pending.Count > 0 || active.Count > 0))
            {
                while (active.Count < Concurrency && pending.Count > 0) active.Add(new Slot(pending.Dequeue()));
                for (int i = active.Count - 1; i >= 0; i--)
                {
                    bool running;
                    try { running = active[i].Step(); }
                    catch (Exception e) { Error = e; yield break; }
                    if (!running) { active[i].Dispose(); active.RemoveAt(i); Completed++; }
                }
                yield return null;
            }
        }
        finally { Release(); }
    }

    public void Dispose() { Cancelled = true; Release(); }
    void Release()
    {
        foreach (var slot in active) slot.Dispose();
        active.Clear();
        while (pending.Count > 0) (pending.Dequeue() as IDisposable)?.Dispose();
    }

    sealed class Slot : IDisposable
    {
        readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
        AsyncOperation waiting;
        public Slot(IEnumerator job) { stack.Push(job); }
        public bool Step()
        {
            if (waiting != null && !waiting.isDone) return true;
            waiting = null;
            while (stack.Count > 0)
            {
                var current = stack.Peek();
                if (!current.MoveNext()) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                if (current.Current is IEnumerator nested) { stack.Push(nested); continue; }
                if (current.Current is AsyncOperation operation) waiting = operation;
                else if (current.Current != null) throw new InvalidOperationException("Unsupported download yield.");
                return true;
            }
            return false;
        }
        public void Dispose()
        {
            if (waiting is UnityWebRequestAsyncOperation request && !request.isDone) request.webRequest.Abort();
            waiting = null;
            while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
        }
    }
}
