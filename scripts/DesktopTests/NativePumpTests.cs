using Avalonia.Threading;

static class NativePumpTests {
    // Short frames exercise idle native dispatcher exit through the same pump
    // as the 20ms condition waits, without extending the native budget.
    public static void Cancellation() {
        for(int iteration=0;iteration<2000;iteration++) {
            if(iteration%100==0) {
                Console.WriteLine($"NATIVE_TEST_PROGRESS pump-cancellation iteration={iteration}");Console.Out.Flush();
            }
            if(!TestUiPump.Slice(1))throw new Exception("Native pump returned before its cancellation");
        }
        Console.WriteLine("PASS native pump: 2000 short cancellable frames return");
    }
}
