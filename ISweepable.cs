using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AO_Lib
{
    public interface ISweepable
    {
        int Set_Sweep_on(float MHz_start, float Sweep_range_MHz, int steps, double time_up, double time_down);

        int Set_Sweep_on(float MHz_start, float Sweep_range_MHz, double Period/*[мкс с точностью до двух знаков,минимум 1]*/, bool OnRepeat);

        int SetHz_KeepSweep(float MHz, bool keep = true);
    }

    public struct SweepParameters
    {
        public float f0;
        public float Sweep_range;
        public int steps;
        public double time_up;
        public double time_down;
    }
}
