namespace HardwarePulse {
    // Resolves temporary presentation from saved preferences. Never persists state.
    public sealed class MaterialPolicy {
        readonly double opacity;
        readonly bool lockedMonitor;
        public bool Solid {get;private set;}
        public bool Clear {get;private set;}

        public MaterialPolicy(double opacityPercent,bool locked,bool settingsVisible,bool solid,bool highContrast){
            opacity=opacityPercent/100;
            lockedMonitor=locked&&!settingsVisible;
            Solid=solid||highContrast;
            Clear=lockedMonitor||opacityPercent==0;
        }

        public bool CanAdjustOpacity(bool supported){return !Solid&&supported;}
        public double EffectiveOpacity(bool supported){
            if(!CanAdjustOpacity(supported))return 1;
            return lockedMonitor?opacity*.25:opacity;
        }
    }
}
