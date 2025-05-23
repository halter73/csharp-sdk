#if NETSTANDARD2_0
using System.Reflection;

namespace System.Threading.Tasks;

internal static class TaskExtensions
{
    /// <summary>
    /// Gets whether the task has completed successfully.
    /// </summary>
    /// <param name="task">The task.</param>
    /// <returns>true if the task has completed successfully; otherwise, false.</returns>
    public static bool IsCompletedSuccessfully(this Task task)
    {
        return task.Status == TaskStatus.RanToCompletion;
    }
}
#endif