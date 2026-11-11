namespace RestSharp.Tests;

public class AsyncHelpersTests {
    [Fact]
    public void RunSync_returns_result_from_task() {
        var result = AsyncHelpers.RunSync(() => Task.FromResult(42));

        result.Should().Be(42);
    }

    [Fact]
    public void RunSync_propagates_exception_from_task() {
        var act = () => AsyncHelpers.RunSync<int>(() => Task.FromException<int>(new InvalidOperationException("boom")));

        act.Should().Throw<InvalidOperationException>().WithMessage("boom");
    }

    [Fact]
    public void RunSync_starts_task_on_calling_thread_when_no_synchronization_context() {
        // xUnit installs its own SynchronizationContext in test methods, so run
        // the scenario on a dedicated thread with no context installed. This is
        // the environment of issue #2375 (console apps, ASP.NET Core, thread
        // pool threads): the task must be started directly on the calling
        // thread instead of being queued to the thread pool via Task.Run.
        var callingThread = -1;
        var taskThread    = -1;
        var observed      = 0;
        var finished      = new ManualResetEventSlim();
        Exception? error  = null;

        var thread = new Thread(
            () => {
                try {
                    SynchronizationContext.Current.Should().BeNull();

                    callingThread = Environment.CurrentManagedThreadId;

                    observed = AsyncHelpers.RunSync(
                        () => {
                            // Executed synchronously when the returned task is
                            // created: RunSync must invoke func itself, not
                            // through Task.Run.
                            taskThread = Environment.CurrentManagedThreadId;
                            return Task.FromResult(42);
                        }
                    );
                }
                catch (Exception ex) {
                    error = ex;
                }
                finally {
                    finished.Set();
                }
            }
        ) { IsBackground = true };
        thread.Start();

        finished.Wait(TimeSpan.FromSeconds(30)).Should().BeTrue();
        error.Should().BeNull();
        observed.Should().Be(42);
        taskThread.Should().Be(callingThread, "the task must start on the calling thread when no synchronization context is installed");
    }

    [Fact]
    public void RunSync_offloads_to_thread_pool_and_does_not_deadlock_when_synchronization_context_present() {
        // Simulates a UI thread: a synchronization context is installed and the
        // calling thread blocks inside RunSync. The task must be offloaded to the
        // thread pool so that continuations cannot be marshalled back to the
        // blocked caller (the deadlock fixed in #2371).
        var callingThread = Environment.CurrentManagedThreadId;
        var workerThread  = -1;
        var observed      = 0;
        var finished      = new ManualResetEventSlim();

        var uiThread = new Thread(
            () => {
                var original = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(new NonDispatchingSynchronizationContext());

                try {
                    observed = AsyncHelpers.RunSync(
                        () => {
                            workerThread = Environment.CurrentManagedThreadId;
                            return DoAsync();
                        }
                    );
                }
                finally {
                    SynchronizationContext.SetSynchronizationContext(original);
                    finished.Set();
                }
            }
        ) { IsBackground = true };
        uiThread.Start();

        finished.Wait(TimeSpan.FromSeconds(30)).Should().BeTrue("RunSync must not deadlock when a synchronization context is installed");

        observed.Should().Be(42);
        workerThread.Should().NotBe(callingThread, "the task must be offloaded to the thread pool when a synchronization context is installed");
        uiThread.Join(TimeSpan.FromSeconds(1));

        return;

        static async Task<int> DoAsync() {
            // A plain await inside the offloaded work. If the work ran with the
            // caller's context installed, the continuation would be posted to
            // the non-dispatching context and the caller would deadlock.
            await Task.Yield();
            return 42;
        }
    }

    /// <summary>
    /// Captures continuations like a UI thread would, but never dispatches them.
    /// </summary>
    class NonDispatchingSynchronizationContext : SynchronizationContext {
        public override void Post(SendOrPostCallback callback, object? state) { }
    }
}
