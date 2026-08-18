namespace Bridge.Services;

internal static class TaskExtensions
{
    /// <summary>
    /// Observes a fire-and-forget task so exceptions reach errors.log instead of
    /// staying unobserved on the thread pool.
    /// </summary>
    internal static void FireAndForget(this Task task, string context)
    {
        if (task.IsCompleted)
        {
            if (task.IsFaulted && task.Exception is not null)
                App.LogException(new AggregateException($"[{context}] {task.Exception.InnerException?.Message}", task.Exception));
            return;
        }

        _ = task.ContinueWith(
            t =>
            {
                if (t.IsFaulted && t.Exception is not null)
                    App.LogException(new AggregateException($"[{context}] {t.Exception.InnerException?.Message}", t.Exception));
            },
            TaskScheduler.Default);
    }
}
