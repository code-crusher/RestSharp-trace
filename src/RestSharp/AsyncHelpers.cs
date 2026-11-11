//  Copyright (c) .NET Foundation and Contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

namespace RestSharp;

static class AsyncHelpers {
    /// <summary>
    /// Executes a task synchronously and returns the result.
    /// </summary>
    /// <param name="func">Callback for asynchronous task to run</param>
    /// <typeparam name="T">Return type for the task</typeparam>
    /// <returns>Return value from the task</returns>
    /// <remarks>
    /// When the current thread has no synchronization context (console apps, ASP.NET Core,
    /// thread pool threads), the task is started directly on the calling thread. Starting the
    /// task directly means that no extra thread pool work item is queued, which is important
    /// because blocking callers that also queue work items can starve the thread pool when
    /// many sync calls run in parallel (see issue #2375).
    /// When a synchronization context is present (WinForms, WPF), the task is offloaded to the
    /// thread pool so that continuations cannot be marshalled back to the blocked caller and deadlock.
    /// </remarks>
    public static T RunSync<T>(Func<Task<T>> func) {
        var task = SynchronizationContext.Current == null ? func() : Task.Run(func);
        return task.GetAwaiter().GetResult();
    }
}
