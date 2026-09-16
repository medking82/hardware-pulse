using System;

public static class WindowSnapDpiTests {
    public static void Run() {
        Check(delegate{return 144;},2.0,1.5,"Per-monitor DPI takes priority");
        Check(delegate{throw new EntryPointNotFoundException();},1.5,1.5,"Win7 uses WPF system DPI");
        Check(delegate{throw new DllNotFoundException();},1.25,1.25,"Unavailable native DPI uses WPF");
        Check(delegate{return 0;},2.0,2.0,"Invalid HWND result uses WPF");
        Check(delegate{return 0;},Double.NaN,1.0,"Invalid fallback is bounded");
        Check(delegate{return 96;},2.0,1.0,"96 DPI keeps normal snap distances");
        int calls=0;bool available=true;
        Func<uint> missing=delegate{calls++;throw new EntryPointNotFoundException();};
        WindowSnap.ResolveDpiScale(missing,1.5,ref available);
        WindowSnap.ResolveDpiScale(missing,1.5,ref available);
        if(calls!=1||available)throw new Exception("Missing DPI export must not throw on every drag frame");
    }
    static void Check(Func<uint> native,double fallback,double expected,string message) {
        bool available=true;
        if(WindowSnap.ResolveDpiScale(native,fallback,ref available)!=expected)throw new Exception(message);
    }
}
