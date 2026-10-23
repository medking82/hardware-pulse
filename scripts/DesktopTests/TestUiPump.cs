using Avalonia.Threading;

static class TestUiPump {
    public static bool Slice(int milliseconds) {
        using var stop=new CancellationTokenSource();
        // A native GetMessage/epoll wait need not wake for a ThreadPool token timer.
        // The dispatcher timer wakes the backend and cancels from inside this frame.
        using var wake=DispatcherTimer.RunOnce(stop.Cancel,TimeSpan.FromMilliseconds(milliseconds),DispatcherPriority.Send);
        Dispatcher.UIThread.MainLoop(stop.Token);
        return stop.IsCancellationRequested;
    }
}
