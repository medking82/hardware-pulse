using System;

namespace HardwarePulse {
    // Serially polled byte counters. Source IO and clock ownership stay in adapters.
    public sealed class NetworkInterval {
        ulong received,sent;
        double timestamp;
        bool baseline;
        static bool Finite(double value){return !double.IsNaN(value)&&!double.IsInfinity(value);}
        public void Reset(){baseline=false;}
        public bool Update(ulong rx,ulong tx,double seconds,out double down,out double up){
            down=up=0;
            if(!Finite(seconds)){Reset();throw new ArgumentException("Invalid monotonic clock","seconds");}
            double elapsed=seconds-timestamp;
            bool valid=baseline&&elapsed>0&&Finite(elapsed)&&rx>=received&&tx>=sent;
            if(valid){
                down=(rx-received)/elapsed;up=(tx-sent)/elapsed;
                valid=Finite(down)&&Finite(up);
            }
            received=rx;sent=tx;timestamp=seconds;baseline=true;
            if(!valid)down=up=0;
            return valid;
        }
    }
}
