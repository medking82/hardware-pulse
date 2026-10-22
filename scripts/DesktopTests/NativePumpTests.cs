using Avalonia.Threading;

static class NativePumpTests {
    // Short cancellable frames exercise the same off-thread cancellation/native-entry
    // boundary used by the 20ms condition pumps, without extending the native budget.
    public static void Cancellation() {
        for(int iteration=0;iteration<2000;iteration++) {
            if(iteration%100==0) {
                Console.WriteLine($"NATIVE_TEST_PROGRESS pump-cancellation iteration={iteration}");Console.Out.Flush();
            }
            using var stop=new CancellationTokenSource(TimeSpan.FromMilliseconds(1));
            Dispatcher.UIThread.MainLoop(stop.Token);
            if(!stop.IsCancellationRequested)throw new Exception("Native pump returned before its cancellation");
        }
        Console.WriteLine("PASS native pump: 2000 short cancellable frames return");
    }
}
