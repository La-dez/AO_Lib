﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
#if X86
using FT_HANDLE = System.UInt32;
#elif X64
using FT_HANDLE = System.UInt64;
#endif

using System.Linq;
//using aom; 

namespace AO_Lib
{
    public static partial class AO_Devices
    {
        
      
        abstract public class AO_Deflector : IDisposable
        {
            public abstract DeflectorTypes DeflectorType { get; }

            //Все о фильтре: дескриптор(имя), полное и краткое имя dev файла и управляющая dll
            protected abstract string _DeflectorName { set; get; }
            public virtual string DeflectorName { get { return (_DeflectorName); } }
            protected abstract string _DeflectorSerial { set; get; }
            public virtual string DeflectorSerial { get { return (_DeflectorSerial); } }
            protected abstract string DeflectorCfgName { set; get; }
            protected abstract string DeflectorCfgPath { set; get; }
            protected abstract string DllName { set; get; }

            protected abstract float[] HZs_1 { set; get; }
            protected abstract float[] HZs_2 { set; get; }
            protected abstract float[] Angles_1 { set; get; }
            protected abstract float[] Angles_2 { set; get; }
            protected abstract float[] Attenuation_1 { set; get; }
            protected abstract float[] Attenuation_2 { set; get; }
            //
            protected abstract bool AOD_Loaded_without_fails { set; get; }
            protected abstract bool sAOF_isPowered { set; get; }
            public virtual bool isPowered { get { return sAOF_isPowered; } }

            //базовые поля для получения диапазонов по перестройке
            public abstract float Angle_Max_1 { get; }
            public abstract float Angle_Max_2 { get; }
            public abstract float Angle_Min_1 { get; }
            public abstract float Angle_Min_2 { get; }
            public abstract float HZ_Max_1 { get; }
            public abstract float HZ_Min_1 { get; }
            public abstract float HZ_Max_2 { get; }
            public abstract float HZ_Min_2 { get; }

            protected virtual float sHZ_Current_1 { set; get; }
            protected virtual float sAngle_Current_1 { set; get; }
            protected virtual uint sAttenuation_1_current { set; get; }

            protected virtual float sHZ_Current_2 { set; get; }
            protected virtual float sAngle_Current_2 { set; get; }
            protected virtual uint sAttenuation_2_current { set; get; }


            public float Angle_Current_1 { get { return sAngle_Current_1; } }
            public float HZ_Current_1 { get { return sHZ_Current_1; } }
            public uint Attenuation_1_current { get { return sAttenuation_1_current; } }
            public float Angle_Current_2 { get { return sAngle_Current_2; } }
            public float HZ_Current_2 { get { return sHZ_Current_2; } }
            public uint Attenuation_2_current { get { return sAttenuation_2_current; } }


            protected const double Reference_frequency = 350e6;

            //все о свипе
            protected abstract bool sAO_Sweep_On { set; get; }
            public bool is_inSweepMode { get { return sAO_Sweep_On; } }

            public virtual float AO_ExchangeRate_Min { get { return 500; } } //[Гц]
            public virtual float AO_ExchangeRate_Max { get { return 4500; } } //[Гц]
            public virtual float AO_ProgrammMode_step { get { return 500; } } //[кГц/шаг]
            public virtual float AO_TimeDeviation_Min { get { return 5; } }   // [мс]     
            public virtual float AO_TimeDeviation_Max { get { return 40; } } // [мс]
            public virtual float AO_FreqDeviation_Min { get { return 0.5f; } } // [МГц]
            public virtual float AO_FreqDeviation_Max { get { return 5.0f; } }// [МГц]

            //все о программируемой перестройке
            protected abstract bool sAO_ProgrammMode_Ready { set; get; }
            public bool is_Programmed { get { return sAO_ProgrammMode_Ready; } }

            //поля для реализации специальной задержки между перестройками
            public virtual int MS_delay { get; protected set; }// [мс]
            public virtual int MS_delay_default { get { return 20; } }// [мс]
            public virtual int MS_delay_min { get { return 1; } }// [мс]
            public virtual int MS_delay_max { get { return 2000; } }// [мс]
            protected virtual System.Timers.Timer InnerTimer { set; get; }
            protected virtual float datavalue_2set { set; get; }
            protected virtual bool IsReady2set { set; get; }
            protected virtual bool WasLastSetting { set; get; }
            protected virtual Action<float> ActionOfSetting { set; get; }

            //события
            public delegate void SetNotifier(AO_Deflector sender, float Angle_now_1, float HZ_now_1, float Angle_now_2, float HZ_now_2);
            public abstract event SetNotifier onSetHz;

            ///функционал

            protected AO_Deflector()
            {
                //InitTimer(MS_delay_default);
            }

            public void InitTimer(int ms_delay)
            {
                // Create a timer with a two second interval.
                if ((ms_delay < MS_delay_min) || (ms_delay > MS_delay_max)) MS_delay = MS_delay_default;
                else MS_delay = ms_delay;
                InnerTimer?.Dispose();
                InnerTimer = null;
                InnerTimer = new System.Timers.Timer(ms_delay);
                // Hook up the Elapsed event for the timer. 
                InnerTimer.Elapsed += OnElapedEvent;
                InnerTimer.AutoReset = true;
                InnerTimer.Stop();

                IsReady2set = true;
                WasLastSetting = false;
            }
            public void DeinitTimer()
            {
                InnerTimer.Dispose();
                InnerTimer = null;
            }

            protected virtual void OnElapedEvent(Object source, System.Timers.ElapsedEventArgs e)
            {
                /*  Console.WriteLine("The Elapsed event was raised at {0:HH:mm:ss.fff}",
                                    e.SignalTime);*/
                IsReady2set = true;
                InnerTimer.Stop();
                if (!WasLastSetting)
                {
                    ActionOfSetting(datavalue_2set);
                    IsReady2set = false;
                    InnerTimer.Start();
                }
                else
                {
                    IsReady2set = true;
                }
                WasLastSetting = true;
            }

            public virtual byte[] Create_byteMass_forFastTuneTest(float[] pFreqs,int steps=300, uint pAttenuation = 0)
            {
                return new byte[0];
            }
            public virtual int Test_fast_tune(int pSteps)
            {
                        return (int)FTDIController.FT_STATUS.FT_OTHER_ERROR;
            }
            //перестройка угла
            public virtual int Set_Angle_1(float pAngle_1,uint AT1 = 0)
            {
                return Set_Angle_both(pAngle_1, Angle_Current_2,AT1,Attenuation_2_current);
            }
            public virtual int Set_Hz_1(float pfreq_1, uint AT1 = 0)
            {
                return Set_Hz_both(pfreq_1, HZ_Current_2,AT1, Attenuation_2_current);
            }
            public virtual int Set_Angle_2(float pAngle_2, uint AT2 = 0)
            {
                return Set_Angle_both(Angle_Current_1,pAngle_2, Attenuation_1_current, AT2);
            }
            public virtual int Set_Hz_2(float pfreq_2, uint AT2 = 0)
            {
                return Set_Hz_both(HZ_Current_1, pfreq_2, Attenuation_1_current, AT2);
            }

            public virtual int Set_Angle_both(float pAngle_1, float pAngle_2, uint AT_1 = 0, uint AT_2 = 0)
            {
                if ((pAngle_1>Angle_Max_1) ||(pAngle_1<Angle_Min_1))
                    throw new Exception(String.Format("Unable to set this angle. Please, enter the angle 1 value in {0} - {1} degrees range.", Angle_Min_1, Angle_Max_1));
                if ((pAngle_2 > Angle_Max_2) || (pAngle_2 < Angle_Min_2))
                    throw new Exception(String.Format("Unable to set this angle. Please, enter the angle 1 value in {0} - {1} degrees range.", Angle_Min_2, Angle_Max_2));

                float HZ_1 = Get_HZ_via_Angle(pAngle_1, 0);
                float HZ_2 = Get_HZ_via_Angle(pAngle_2, 1);
                return Set_Hz_both(HZ_1, HZ_2, AT_1, AT_2,true);
            }
            public virtual int Set_Hz_both(float pfreq_1,float pfreq_2, uint AT_1 = 0, uint AT_2 = 0,bool ignore_exc = false)
            {
                if(!ignore_exc)
                {
                    if ((pfreq_1 < HZ_Min_1) || (pfreq_1 > HZ_Max_1))
                        throw new Exception(String.Format("Unable to set this freq. Please, enter the freq 1 value in {0} - {1} MHz range.", HZ_Min_1, HZ_Max_1));
                    if ((pfreq_2 < HZ_Min_2) || (pfreq_2 > HZ_Max_2))
                        throw new Exception(String.Format("Unable to set this freq. Please, enter the freq 1 value in {0} - {1} MHz range.", HZ_Min_2, HZ_Max_2));
                }
                return 0;
            }


            public abstract int Set_OutputPower(byte percentage);

            public abstract int Set_Sweep_on(float MHz_start, float Sweep_range_MHz, double Period/*[мс с точностью до двух знаков]*/, bool OnRepeat);
            public abstract int Set_Sweep_off();

            public abstract string Ask_required_dev_file();
            public virtual string Ask_loaded_dev_file() { return DeflectorCfgName; }
            public virtual int Read_dev_file(string path)
            {
                //throw new Exception();
                var Data_from_dev = Helper.Files.Read_txt(path);
                DeflectorCfgPath = path;
                DeflectorCfgName = System.IO.Path.GetFileName(path);
                List<float[]> AllPars;
                Helper.Files.Get_Data_fromDevFile(Data_from_dev.ToArray(), out AllPars);

                var dHZs_1 = AllPars[0];
                var dAttenuation_1 = AllPars[2];
                var dHZs_2 = AllPars[0];
                var dAttenuation_2 = AllPars[4];

                float[] dAngles_1 = new float[AllPars[1].Length];
                AllPars[1].CopyTo(dAngles_1,0);
                float[] dAngles_2 = new float[AllPars[3].Length];
                AllPars[3].CopyTo(dAngles_2, 0);

                Helper.Math.Interpolate_curv(ref dAngles_1, ref dHZs_1,0.1f);
                Helper.Math.Interpolate_curv(ref dAngles_2, ref dHZs_2,0.1f);
                dAngles_1 = AllPars[1];
                dAngles_2 = AllPars[3];
                Helper.Math.Interpolate_curv(ref dAngles_1, ref dAttenuation_1, 0.1f);
                Helper.Math.Interpolate_curv(ref dAngles_2, ref dAttenuation_2, 0.1f);

                HZs_1 = dHZs_1; HZs_2 = dHZs_2;
                Angles_1 = dAngles_1; Angles_2 = dAngles_2;
                Attenuation_1 = dAttenuation_1; Attenuation_2 = dAttenuation_2;

                DeflectorCfgPath = path;
                DeflectorCfgName = System.IO.Path.GetFileName(path);

                return 0;
            }

            protected abstract int Init_device(uint number);
            protected abstract int Deinit_device();

            public abstract int PowerOn();
            public abstract int PowerOff();
            public abstract string Implement_Error(int pCode_of_error);

            public abstract void Dispose();

            public virtual float Get_HZ_via_Angle(float pAngle,int Cell_Ind)
            {
                float Angle_Min;
                float[] Angles, HZs;
                if (Cell_Ind ==0)
                {
                    Angle_Min = Angle_Min_1;
                    Angles = Angles_1;
                    HZs = HZs_1;
                }
                else
                {
                    Angle_Min = Angle_Min_2;
                    Angles = Angles_2;
                    HZs = HZs_2;
                }
                int remind = -1;
                for (int i = Angles.Length-1; i >=0 ;i--)
                {
                    if (pAngle >= Angles[i]) { remind = i; break; }
                }
                if (remind == Angles.Length - 1) return HZs[remind];

                return (float)Helper.Math.Interpolate_value(Angles[remind], HZs[remind], Angles[remind + 1], HZs[remind + 1], pAngle);              

            }
            public virtual float Get_Angle_via_HZ(float pHZ, int Cell_Ind)
            {
                float Angle_Min;
                float[] Angles, HZs;

                if (Cell_Ind == 1)
                {
                    Angle_Min = Angle_Min_1;
                    Angles = Angles_1;
                    HZs = HZs_1;
                }
                else
                {
                    Angle_Min = Angle_Min_2;
                    Angles = Angles_2;
                    HZs = HZs_2;
                }

                int num = HZs.Length;
                int rem_pos = -1;
                for (int i = 0; i < num - 1; i++)
                {
                    if ((HZs[i] >= pHZ) && (HZs[i + 1] <= pHZ)) { rem_pos = i; break; }
                }
                if (rem_pos != -1)
                {
                    if (pHZ == HZs[rem_pos]) return Angles[rem_pos];
                    else if (pHZ == HZs[rem_pos + 1]) return Angles[rem_pos + 1];
                    else
                    {
                        return (float)Helper.Math.Interpolate_value(HZs[rem_pos], Angles[rem_pos], HZs[rem_pos + 1], Angles[rem_pos + 1], pHZ);
                    }
                }
                else
                {
                    return Angles[0];
                }

            }
            protected virtual float Get_Intensity_via_WL(int pWL)
            {
                /* float distance = (pWL - WL_Min);
                 if ((distance < (WLs.Length)) && (distance >= 0))
                 {
                     int a = (int)distance;
                     if ((distance - a) < 1e6f) { return Intensity[a]; }
                     else { return (float)LDZ_Code.ServiceFunctions.Math.Interpolate_value(WLs[a], Intensity[a], WLs[a + 1], Intensity[a + 1], pWL); }
                 }
                 else
                 {
                     if (distance < 0) return Intensity[0];
                     else return Intensity[Intensity.Length - 1];
                 }*/
                return 0;
            }
            protected virtual float Get_Intensity_via_HZ(float pHZ, int Cell_Ind)
            {
                float[] HZs = Cell_Ind == 1 ? HZs_1 : HZs_2;
                float[] Attenuation = Cell_Ind == 1 ? Attenuation_1 : Attenuation_2;
                int num = HZs.Length;
                int rem_pos = -1;
                float result = 3000;
                for (int i = 0; i < num - 1; i++)
                {
                    if ((HZs[i] >= pHZ) && (HZs[i + 1] <= pHZ)) { rem_pos = i; break; }
                }
                if (rem_pos != -1)
                {
                    if (pHZ == HZs[rem_pos]) return Attenuation[rem_pos];
                    else if (pHZ == HZs[rem_pos + 1]) return Attenuation[rem_pos + 1];
                    else
                    {
                        result = (float)Helper.Math.Interpolate_value(HZs[rem_pos], Attenuation[rem_pos], HZs[rem_pos + 1], Attenuation[rem_pos + 1], pHZ);
                    }
                }
                else
                {
                    result = Attenuation[0];
                }

                if (result < 2)
                    result = 3000;
                return result;
            }
            public virtual System.Drawing.PointF Sweep_Recalculate_borders(float pHZ_needed, float pHZ_Radius,int CellInd)
            {
                float HZ_Max = (CellInd == 1 ? HZ_Max_1 : HZ_Max_2);
                float HZ_Min = (CellInd == 1 ? HZ_Min_1 : HZ_Min_2);

                var data_list = new List<float> { pHZ_needed };
                List<System.Drawing.PointF> result = new List<System.Drawing.PointF>(); // for each point: X is LeftBorder, Y is Width
                int max_count = data_list.Count;
                float preMin = 0, preMax = 0;
                for (int i = 0; i < max_count; i++)//Search freq by WL
                {
                    preMin = data_list[i] - pHZ_Radius;
                    preMax = data_list[i] + pHZ_Radius;
                    preMin = preMin < HZ_Min ? HZ_Min : preMin;
                    preMax = preMax > HZ_Max ? HZ_Max : preMax;
                    result.Add(new System.Drawing.PointF(preMin, (float)((double)preMax - (double)preMin)));
                }
                return result[0];
            }
            [Obsolete]
            public static AO_Deflector Find_and_connect_any_Deflector()
            {
                int NumberOfTypes = 1;
                int[] Devices_per_type = new int[NumberOfTypes];

                string[] Descriptor_forSTCDeflector;
                string[] Serial_forSTCDeflector;
                Devices_per_type[0] = STC_Deflector.Search_Devices(out Descriptor_forSTCDeflector, out Serial_forSTCDeflector);
                if (Devices_per_type[0] != 0) return (new STC_Deflector(Descriptor_forSTCDeflector.Last(), Serial_forSTCDeflector.Last()));
                else return (new Emulator_of_Deflector());
            }
            public static List<AO_Deflector> Find_all_deflectors()
            {
                var FinalList = new List<AO_Deflector>();
                //search of deflectors of any types
                var l1 = List_STC_Deflectors();
                FinalList = ConcatLists_ofDeflectors(l1);
                // if (FinalList.Count == 0) FinalList.Add(new Emulator());
                return FinalList;
            }
            private static List<AO_Deflector> List_STC_Deflectors()
            {
                var FinalList = new List<AO_Deflector>();
                string[] DeflectorNames; string[] DeflectorSerials;
                var NumOfDev = STC_Deflector.Search_Devices(out DeflectorNames, out DeflectorSerials);
                for (int i = 0; i < NumOfDev; i++)
                {
                    FinalList.Add(new STC_Deflector(DeflectorNames[i], DeflectorSerials[i]));
                }
                return FinalList;
            }
            private static List<AO_Deflector> ConcatLists_ofDeflectors(params List<AO_Deflector>[] deflectors)
            {
                List<AO_Deflector> datalist = new List<AO_Deflector>();
                foreach (List<AO_Deflector> list in deflectors)
                { datalist.AddRange(list); }
                return datalist;
            }
            
        }
        public class STC_Deflector : AO_Deflector
        {
            public override DeflectorTypes DeflectorType { get { return DeflectorTypes.STC_Deflector; } }
            protected override string _DeflectorName { set; get; }
            protected override string _DeflectorSerial { set; get; }
            protected override string DeflectorCfgName { set; get; }
            protected override string DeflectorCfgPath { set; get; }
            protected override string DllName { set; get; }

            protected override float[] HZs_1 { set; get; }
            protected override float[] Angles_1 { set; get; }
            protected override float[] Attenuation_1 { set; get; }
            protected override float[] HZs_2 { set; get; }
            protected override float[] Angles_2 { set; get; }
            protected override float[] Attenuation_2 { set; get; }

            protected override bool AOD_Loaded_without_fails { set; get; }
            protected override bool sAOF_isPowered { set; get; }

            public override float Angle_Max_1 { get { return Angles_1[Angles_1.Length - 1]; } }
            public override float Angle_Min_1 { get { return Angles_1[0]; } }
            public override float HZ_Max_1 { get { return HZs_1[0]; } }
            public override float HZ_Min_1 { get { return HZs_1[HZs_1.Length - 1]; } }
            public override float Angle_Max_2 { get { return Angles_2[Angles_2.Length - 1]; } }
            public override float Angle_Min_2 { get { return Angles_2[0]; } }
            public override float HZ_Max_2 { get { return HZs_2[0]; } }
            public override float HZ_Min_2 { get { return HZs_2[HZs_2.Length - 1]; } }

            protected override bool sAO_Sweep_On { set; get; }
            protected override bool sAO_ProgrammMode_Ready { set; get; }

            private byte[] Own_UsbBuf = new byte[5000];
            private byte[] Own_ProgrammBuf;
#if X86
            private UInt32 Own_m_hPort = 0;
#elif X64
            private UInt64 Own_m_hPort = 0;
#endif


            public override event SetNotifier onSetHz;

            public STC_Deflector(string Descriptor, uint number)
            {
                _DeflectorName = Descriptor;
                _DeflectorSerial = number.ToString() + " - number in the list of STC Deflectors";
                try
                {

                    Init_device(number);
                    AOD_Loaded_without_fails = true;

                    sAO_ProgrammMode_Ready = false;
                }
                catch
                {
                    AOD_Loaded_without_fails = false;
                }
            }
            public STC_Deflector(string Descriptor, string Serial) : base()
            {
                _DeflectorName = Descriptor;
                _DeflectorSerial = Serial;
                try
                {
                    Init_device(Serial);
                    AOD_Loaded_without_fails = true;

                    sAO_ProgrammMode_Ready = false;
                }
                catch
                {
                    AOD_Loaded_without_fails = false;
                }
            }

            ~STC_Deflector()
            {
                this.PowerOff();
                this.Dispose();
            }
            public int ClearBuffer()
            {
                var status = FTDIController.FT_ResetDevice(Own_m_hPort); //ResetDevice();
                status = FTDIController.FT_Purge(Own_m_hPort, FTDIController.FT_PURGE_RX | FTDIController.FT_PURGE_TX); // Purge(FT_PURGE_RX || FT_PURGE_TX);
                status = FTDIController.FT_ResetDevice(Own_m_hPort); //ResetDevice();
                return (int)status;
            }

            public override int Set_Hz_both(float pfreq_1, float pfreq_2, uint AT_1=0, uint AT_2 = 0, bool ignore_exc = false)
            {
                base.Set_Hz_both(pfreq_1, pfreq_2, AT_1, AT_2, ignore_exc);
                if (AOD_Loaded_without_fails)
                {
                    try
                    {
                        Own_UsbBuf = Create_byteMass_forHzTune(pfreq_1, pfreq_2, AT_1, AT_2);

                        ClearBuffer();
                        WriteUsb(7+6);
                        sAngle_Current_1 = Get_Angle_via_HZ(pfreq_1, 0);
                        sAngle_Current_2 = Get_Angle_via_HZ(pfreq_2, 1);
                        sHZ_Current_1 = pfreq_1;
                        sHZ_Current_2 = pfreq_2;
                        sAttenuation_1_current = (AT_1 == 0) ? (uint)Get_Intensity_via_HZ(pfreq_1, 0) : AT_1;
                        sAttenuation_2_current = (AT_2 == 0) ? (uint)Get_Intensity_via_HZ(pfreq_2, 1) : AT_2;
                        onSetHz?.Invoke(this, Angle_Current_1, HZ_Current_1, Angle_Current_2, HZ_Current_2);
                        return 0;
                    }
                    catch (Exception exc)
                    {
                        return (int)FTDIController.FT_STATUS.FT_OTHER_ERROR;
                    }
                }
                else
                {
                    return (int)FTDIController.FT_STATUS.FT_DEVICE_NOT_FOUND;
                }
            }
            public int Set_Hz_both_viaByteMass(byte[] pBM,bool Is_InnerVaribles_changed=false, float pfreq_1=0, float pfreq_2=0, uint AT_1 = 0, uint AT_2 = 0)
            {
                if (AOD_Loaded_without_fails)
                {
                    try
                    {
                        ClearBuffer();
                        WriteUsb(pBM, 7 + 6);
                        if (Is_InnerVaribles_changed)
                        {
                            sAngle_Current_1 = Get_Angle_via_HZ(pfreq_1, 0);
                            sAngle_Current_2 = Get_Angle_via_HZ(pfreq_2, 1);
                            sHZ_Current_1 = pfreq_1;
                            sHZ_Current_2 = pfreq_2;
                            sAttenuation_1_current = AT_1;
                            sAttenuation_2_current = AT_2;
                            onSetHz?.Invoke(this, Angle_Current_1, HZ_Current_1, Angle_Current_2, HZ_Current_2);
                        }
                        return 0;
                    }
                    catch (Exception exc)
                    {
                        return (int)FTDIController.FT_STATUS.FT_OTHER_ERROR;
                    }
                }
                else
                {
                    return (int)FTDIController.FT_STATUS.FT_DEVICE_NOT_FOUND;
                }
            }
           
            public void Create_byteMass_forProgramm_mode(float[,] pAO_All_CurveSweep_Params)
            {
                int i_max = pAO_All_CurveSweep_Params.GetLength(0);
                float[,] Mass_of_params = new float[i_max, 7];
                int i = 0;
                byte[] Start_mass = new byte[4] { (byte)FTDIController.Bit_reverse(0x14), (byte)FTDIController.Bit_reverse(0x11), (byte)FTDIController.Bit_reverse(0x12), (byte)FTDIController.Bit_reverse(0xff) };
                byte[] Separ_mass = new byte[3] { 0x13, 0x13, 0x13 };
                byte[] Finish_mass = new byte[3] { (byte)FTDIController.Bit_reverse(0x15), (byte)FTDIController.Bit_reverse(0x15), (byte)FTDIController.Bit_reverse(0x15) };
                for (i = 0; i < i_max; i++)
                {
                    Mass_of_params[i, 0] = pAO_All_CurveSweep_Params[i, 0]; //ДВ (для отображения)
                    if (pAO_All_CurveSweep_Params[i, 3] != 0) //строка со свипом
                    {
                        System.Drawing.PointF data_for_sweep = this.Sweep_Recalculate_borders(pAO_All_CurveSweep_Params[i, 2], pAO_All_CurveSweep_Params[i, 3],0);
                        Mass_of_params[i, 1] = data_for_sweep.X;//Частота Синтезатора
                        Mass_of_params[i, 2] = data_for_sweep.Y;//пересчитанная девиация 
                    }
                    else//строка с обычной перестройкой
                    {
                        Mass_of_params[i, 1] = pAO_All_CurveSweep_Params[i, 2];//Частота Синтезатора
                        Mass_of_params[i, 2] = 0;//пересчитанная девиация
                    }
                    Mass_of_params[i, 3] = pAO_All_CurveSweep_Params[i, 4]; //время одной девиации
                    Mass_of_params[i, 4] = pAO_All_CurveSweep_Params[i, 5]; //количество девиаций
                }


                List<byte[]> pre_DataList = new List<byte[]>();
                int Lenght = 0;
                for (i = 0; i < i_max; i++)
                {
                    if (Mass_of_params[i, 2] == 0)//стандартная перестройка
                    {
                        int pCount = 0;
                        int time_ms = (int)(Mass_of_params[i, 3]);
                        pre_DataList.Add(Create_byteMass_forProgrammMode_HZTune(Mass_of_params[i, 1], time_ms, ref pCount));
                        /* pre_DataList.Add(Separ_mass);
                         pre_DataList.Add(new byte[1] { (byte)Mass_of_params[i, 4] });*/
                        Lenght += (pCount/*+ Separ_mass.Length+1*/);
                    }
                    else//свип
                    {
                        int pCount = 0;
                        int time_ms = (int)(Mass_of_params[i, 3]);
                        pre_DataList.Add(Create_byteMass_forProgrammMode_Sweep(Mass_of_params[i, 1], Mass_of_params[i, 2], time_ms, ref pCount));
                        /*  pre_DataList.Add(Separ_mass);
                          pre_DataList.Add(new byte[1] { (byte)Mass_of_params[i, 4] });*/
                        Lenght += (pCount/* + Separ_mass.Length + 1*/);
                    }
                }

                //Переписываем все данные в массив на пересылку
                byte[] Result_mass = new byte[Start_mass.Length + Lenght + Finish_mass.Length];
                int k = 0;
                for (i = 0; i < Start_mass.Length; i++)
                {
                    Result_mass[k] = Start_mass[i]; k++;
                }

                for (i = 0; i < i_max; i++)
                {
                    for (int j = 0; j < pre_DataList[i].Length; j++)
                    {
                        Result_mass[k] = pre_DataList[i][j]; k++;
                    }
                }

                for (i = 0; i < Finish_mass.Length; i++)
                {
                    Result_mass[k] = Finish_mass[i]; k++;
                }

                /* byte[] ownbuf2 = new byte[5000];
                 for (i = 0; i < 5000; i++) ownbuf2[i] = 0;
                 for (i = 0; i < Result_mass.Length; i++)
                     ownbuf2[i] = Result_mass[i];*/

                Own_ProgrammBuf = Result_mass;
                sAO_ProgrammMode_Ready = true;
            }

            private byte[] Create_byteMass_forProgrammMode_Sweep(float pMHz_start, float pSweep_range_MHz /*не менее 1МГц*/, double pPeriod/*[мс с точностью до двух знаков,минимум 1]*/, ref int pcount)
            {
                float freq, fvspom;
                short MSB, LSB;
                ulong lvspom;
                uint ivspom;
                float delta;
                int steps;
                int i;
                float freqMCU = 74.99e6f;
                float inp_freq = 20 * 1000.0f / (float)pPeriod; //in Hz, max 4000 hz //дефолт от Алексея
                double New_Freq_byTime = (pSweep_range_MHz * 1e3f / pPeriod); // [kHz/ms] , 57.4 и более //375
                double Step_kHZs = pSweep_range_MHz * 1e3f / 20.0f;                                     //   было 200, [kHz] // В новом режиме 500 kHz - дефолт от Алексея 
                double Steps_by1MHz = 1e3f / Step_kHZs;                     //      [шагов/МГц]   

                /*   if ((float)(Steps_by1MHz * New_Freq_byTime) < AO_ExchangeRate_Min) //если менее 287, то пересчитываем размер шага, чтобы было более
                   {
                       Steps_by1MHz = AO_ExchangeRate_Min / New_Freq_byTime;
                       Step_kHZs = 1e3 / Steps_by1MHz;
                   }
                   inp_freq = (int)(float)(Steps_by1MHz * New_Freq_byTime);//500-4500*/

                pcount = 2 - 1;
                //   steps = (int)Math.Floor(pSweep_range_MHz * Steps_by1MHz); // number of the steps
                steps = 20;
                double Step_HZs = Step_kHZs * 1000;

                int total_count = pcount + 1 + 4 * steps;
                byte[] data_Own_UsbBuf = new byte[total_count];

                for (i = 1; i < total_count; i++)
                {
                    data_Own_UsbBuf[i] = 0;
                }
                for (i = 0; i < steps; i++)//перепроверить цикл
                {

                    freq = (float)(((pMHz_start) * 1e6f + i * Step_HZs)/*/ 1.17f*/);//1.17 коррекция частоты
                                                                                    //fvspom=freq/300e6;
                    lvspom = (ulong)((freq) * (Math.Pow(2.0, 32.0) / Reference_frequency)); //расчет управляющего слова для частоты
                    MSB = (short)(0x0000ffFF & (lvspom >> 16));
                    LSB = (short)lvspom;
                    data_Own_UsbBuf[pcount + 1] = (byte)(0x00ff & (MSB >> 8));
                    data_Own_UsbBuf[pcount + 2] = (byte)(MSB);
                    data_Own_UsbBuf[pcount + 3] = (byte)(0x00ff & (LSB >> 8));
                    data_Own_UsbBuf[pcount + 4] = (byte)(LSB);
                    pcount += 4;
                }

                pcount -= 4;//компенсация последнего смещения

                //timer calculations
                ivspom = (uint)(65536 - freqMCU / (2 * 2 * inp_freq));

                /*data_Own_UsbBuf[0] = (byte)(0x00ff & (pcount >> 8));
                  data_Own_UsbBuf[1] = (byte)(pcount);
                  data_Own_UsbBuf[2] = (byte)(0x00ff & (ivspom >> 8)); ;
                  data_Own_UsbBuf[3] = (byte)ivspom;*/
                data_Own_UsbBuf[0] = (byte)(0x00ff & (ivspom >> 8)); ;
                data_Own_UsbBuf[1] = (byte)ivspom;
                pcount = total_count;
                //
                for (i = 0; i < total_count; i++)
                {
                    data_Own_UsbBuf[i] = (byte)FTDIController.Bit_reverse(data_Own_UsbBuf[i]);
                }
                return data_Own_UsbBuf;
            }

            private byte[] Create_byteMass_forProgrammMode_HZTune(float pfreq, double pPeriod/*[мс с точностью до двух знаков,минимум 1]*/, ref int pcount)
            {
                float freq, fvspom;
                short MSB, LSB;
                ulong lvspom;
                uint ivspom;
                float delta;
                int steps;
                int i;
                float freqMCU = 74.99e6f;
                float inp_freq = 20 * 1000.0f / (float)pPeriod;  //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

                pcount = 2 - 1;
                steps = 20; // number of the steps

                int total_count = pcount + 1 + 4 * steps;
                byte[] data_Own_UsbBuf = new byte[total_count];

                for (i = 1; i < total_count; i++)
                {
                    data_Own_UsbBuf[i] = 0;
                }
                for (i = 0; i < steps; i++)//перепроверить цикл
                {
                    // if(i<10)
                    //      freq = (float)(((127.287) * 1e6f)/* / 1.17f*/);//1.17 коррекция частоты
                    //  else
                    //     freq = (float)(((77.852) * 1e6f)/* / 1.17f*/);//1.17 коррекция частоты

                    if (i < 10)
                        freq = (float)(((pfreq) * 1e6f)/* / 1.17f*/);//1.17 коррекция частоты
                    else
                        freq = (float)(((pfreq) * 1e6f)/* / 1.17f*/);//1.17 коррекция частоты


                    lvspom = (ulong)((freq) * (Math.Pow(2.0, 32.0) / Reference_frequency)); //расчет управляющего слова для частоты
                    MSB = (short)(0x0000ffFF & (lvspom >> 16));
                    LSB = (short)lvspom;
                    data_Own_UsbBuf[pcount + 1] = (byte)(0x00ff & (MSB >> 8));
                    data_Own_UsbBuf[pcount + 2] = (byte)(MSB);
                    data_Own_UsbBuf[pcount + 3] = (byte)(0x00ff & (LSB >> 8));
                    data_Own_UsbBuf[pcount + 4] = (byte)(LSB);
                    pcount += 4;
                }

                pcount -= 4;//компенсация последнего смещения

                //timer calculations
                ivspom = (uint)(65536 - freqMCU / (2 * 2 * inp_freq));

                /*data_Own_UsbBuf[0] = (byte)(0x00ff & (pcount >> 8));
                  data_Own_UsbBuf[1] = (byte)(pcount);
                  data_Own_UsbBuf[2] = (byte)(0x00ff & (ivspom >> 8)); ;
                  data_Own_UsbBuf[3] = (byte)ivspom;*/

                data_Own_UsbBuf[0] = (byte)(0x00ff & (ivspom >> 8)); ;
                data_Own_UsbBuf[1] = (byte)ivspom;
                pcount = total_count;
                //
                for (i = 0; i < total_count; i++)
                {
                    data_Own_UsbBuf[i] = (byte)FTDIController.Bit_reverse(data_Own_UsbBuf[i]);
                }
                return data_Own_UsbBuf;
            }

            private byte[] Create_byteMass_forSweep_old(float pMHz_start, float pSweep_range_MHz, double pPeriod/*[мс с точностью до двух знаков,минимум 1]*/, bool pOnRepeat, ref int pcount)
            {
                float freq, fvspom;
                short MSB, LSB;
                ulong lvspom;
                uint ivspom;
                float delta;
                int steps;
                int i;
                float freqMCU = 74.99e6f;
                float inp_freq = 20 * 1000.0f / (float)pPeriod; //in Hz, max 4500 hz //дефолт от Алексея
                double New_Freq_byTime = (pSweep_range_MHz * 1e3f / pPeriod); // [kHz/ms] , 57.4 и более //375
                double Step_kHZs = pSweep_range_MHz * 1e3f / 20.0f;                                     //   было 200, [kHz] // В новом режиме 500 kHz - дефолт от Алексея 
                double Steps_by1MHz = 1e3f / Step_kHZs;                     //      [шагов/МГц]   

                pcount = 6 - 1;
                //   steps = (int)Math.Floor(pSweep_range_MHz * Steps_by1MHz); // number of the steps
                steps = 20;
                double Step_HZs = Step_kHZs * 1000;

                int total_count = pcount + 1 + 4 * steps + 3;
                byte[] data_Own_UsbBuf = new byte[total_count];

                for (i = 1; i < total_count; i++)
                {
                    data_Own_UsbBuf[i] = 0;
                }
                for (i = 0; i < steps; i++)//перепроверить цикл
                {

                    freq = (float)(((pMHz_start) * 1e6f + i * Step_HZs) /*/ 1.17f*/);//1.17 коррекция частоты
                                                                                     //fvspom=freq/300e6;
                    lvspom = (ulong)((freq) * (Math.Pow(2.0, 32.0) / Reference_frequency)); //расчет управляющего слова для частоты
                    MSB = (short)(0x0000ffFF & (lvspom >> 16));
                    LSB = (short)lvspom;
                    data_Own_UsbBuf[pcount + 1] = (byte)(0x00ff & (MSB >> 8));
                    data_Own_UsbBuf[pcount + 2] = (byte)(MSB);
                    data_Own_UsbBuf[pcount + 3] = (byte)(0x00ff & (LSB >> 8));
                    data_Own_UsbBuf[pcount + 4] = (byte)(LSB);
                    pcount += 4;
                }

                pcount -= 4;//компенсация последнего смещения

                //timer calculations
                ivspom = (uint)(65536 - freqMCU / (2 * 2 * inp_freq));

                /*data_Own_UsbBuf[0] = (byte)(0x00ff & (pcount >> 8));
                  data_Own_UsbBuf[1] = (byte)(pcount);
                  data_Own_UsbBuf[2] = (byte)(0x00ff & (ivspom >> 8)); ;
                  data_Own_UsbBuf[3] = (byte)ivspom;*/
                data_Own_UsbBuf[0] = (0x14);
                data_Own_UsbBuf[1] = (0x11);
                data_Own_UsbBuf[2] = (0x12);
                data_Own_UsbBuf[3] = (0x0);
                data_Own_UsbBuf[4] = (byte)(0x00ff & (ivspom >> 8)); ;
                data_Own_UsbBuf[5] = (byte)ivspom;
                data_Own_UsbBuf[5 + 1 + 4 * steps + 0] = (0x15);
                data_Own_UsbBuf[5 + 1 + 4 * steps + 1] = (0x15);
                data_Own_UsbBuf[5 + 1 + 4 * steps + 2] = (0x15);

                pcount = total_count;
                //
                for (i = 0; i < total_count; i++)
                {
                    data_Own_UsbBuf[i] = (byte)FTDIController.Bit_reverse(data_Own_UsbBuf[i]);
                }
                return data_Own_UsbBuf;
            }

            private byte[] Create_byteMass_forSweep(float pMHz_start, float pSweep_range_MHz, double pPeriod/*[мс с точностью до двух знаков,минимум 1]*/, ref int pcount)
            {
                float freq, fvspom;
                short MSB, LSB;
                ulong lvspom;
                uint ivspom;
                float delta;
                int steps;
                int i;
                float freqMCU = 74.99e6f;
                float inp_freq = 20 * 1000.0f / (float)pPeriod; 
                double New_Freq_byTime = (pSweep_range_MHz * 1e3f / pPeriod); // [kHz/ms] , 57.4 и более //375
                double Step_kHZs = pSweep_range_MHz * 1e3f / 20.0f;                                     //   было 200, [kHz] // В новом режиме 500 kHz - дефолт от Алексея 
                double Steps_by1MHz = 1e3f / Step_kHZs;                     //      [шагов/МГц]   
                pcount = 4 + 2 - 1;//4 бита на начальный массив, 2 на таймер
                steps = 20;
                double Step_HZs = Step_kHZs * 1000;

                int total_count = pcount + 1 + 4 * steps + 3;
                byte[] data_Own_UsbBuf = new byte[total_count];
                byte[] Start_mass = new byte[4] { (byte)FTDIController.Bit_reverse(0x14), (byte)FTDIController.Bit_reverse(0x11), (byte)FTDIController.Bit_reverse(0x12), (byte)FTDIController.Bit_reverse(0xff) };
                byte[] Finish_mass = new byte[3] { (byte)FTDIController.Bit_reverse(0x15), (byte)FTDIController.Bit_reverse(0x15), (byte)FTDIController.Bit_reverse(0x15) };

                for (i = 1; i < total_count; i++)
                {
                    data_Own_UsbBuf[i] = 0;
                }
                for (i = 0; i < steps; i++)//перепроверить цикл
                {

                    freq = (float)(((pMHz_start) * 1e6f + i * Step_HZs)/*/ 1.17f*/);//1.17 коррекция частоты
                                                                                    //fvspom=freq/300e6;
                    lvspom = (ulong)((freq) * (Math.Pow(2.0, 32.0) / Reference_frequency)); //расчет управляющего слова для частоты
                    MSB = (short)(0x0000ffFF & (lvspom >> 16));
                    LSB = (short)lvspom;
                    data_Own_UsbBuf[pcount + 1] = (byte)(0x00ff & (MSB >> 8));
                    data_Own_UsbBuf[pcount + 2] = (byte)(MSB);
                    data_Own_UsbBuf[pcount + 3] = (byte)(0x00ff & (LSB >> 8));
                    data_Own_UsbBuf[pcount + 4] = (byte)(LSB);
                    pcount += 4;
                }

                pcount -= 4;//компенсация последнего смещения

                //timer calculations
                ivspom = (uint)(65536 - freqMCU / (2 * 2 * inp_freq));

                data_Own_UsbBuf[4] = (byte)(0x00ff & (ivspom >> 8)); ;
                data_Own_UsbBuf[5] = (byte)ivspom;
                pcount = total_count;
                //
                for (i = 0; i < total_count; i++)
                {
                    data_Own_UsbBuf[i] = (byte)FTDIController.Bit_reverse(data_Own_UsbBuf[i]);
                }
                for (i = 0; i < 4; i++)
                {
                    data_Own_UsbBuf[i] = Start_mass[i];
                }
                for (i = 0; i < 3; i++)
                {
                    data_Own_UsbBuf[i + total_count - 3] = Finish_mass[i];
                }
                
                return data_Own_UsbBuf;
            }

            public byte[] Create_byteMass_forHzTune(float pfreq_1,float pfreq_2, uint pCoef_PowerDecrease_1 = 0, uint pCoef_PowerDecrease_2 = 0)
            {
                float fvspom_1, fvspom_2;
                short MSB_1, MSB_2, LSB_1, LSB_2;
                ulong lvspom_1, lvspom_2;

                float freq_was_1 = pfreq_1;
                float freq_was_2 = pfreq_2;
                byte[] data_Own_UsbBuf = new byte[5000];
                uint ivspom_1 = 3600, ivspom_2 = 3600;
                if (pCoef_PowerDecrease_1 == 0)
                {
                    ivspom_1 = (uint)Get_Intensity_via_HZ(pfreq_1, 0);
                }
                else
                {
                    ivspom_1 = (uint)pCoef_PowerDecrease_1;
                }
                if (pCoef_PowerDecrease_2 == 0)
                {
                    ivspom_2 = (uint)Get_Intensity_via_HZ(pfreq_2, 2);
                }
                else
                {
                    ivspom_2 = (uint)pCoef_PowerDecrease_2;
                }
                //check this
                if (ivspom_1 < 2500) ivspom_1 = 2500;
                if (ivspom_1 > 4000) ivspom_1 = 4000;
                if (ivspom_2 < 2500) ivspom_2 = 2500;
                if (ivspom_2 > 4000) ivspom_2 = 4000;


                pfreq_1 = ((freq_was_1) * 1e6f); //in Hz
                pfreq_2 = ((freq_was_2) * 1e6f); //in Hz

                fvspom_1 = (float)(pfreq_1 / Reference_frequency);
                fvspom_2 = (float)(pfreq_2 / Reference_frequency);

                lvspom_1 = (ulong)((pfreq_1) * (Math.Pow(2.0, 32.0) / Reference_frequency));//правый
                lvspom_2 = (ulong)((pfreq_2) * (Math.Pow(2.0, 32.0) / Reference_frequency));//левый

                MSB_1 = (short)(0x0000ffFF & (lvspom_1 >> 16));
                LSB_1 = (short)lvspom_1;
                MSB_2 = (short)(0x0000ffFF & (lvspom_2 >> 16));
                LSB_2 = (short)lvspom_2;

                data_Own_UsbBuf[0] = 0xDE; //222 в десятичной системе

                data_Own_UsbBuf[1] = (byte)(0x00ff & (MSB_1 >> 8));
                data_Own_UsbBuf[2] = (byte)MSB_1;
                data_Own_UsbBuf[3] = (byte)(0x00ff & (LSB_1 >> 8));
                data_Own_UsbBuf[4] = (byte)LSB_1;
                data_Own_UsbBuf[5] = (byte)(0x00ff & (ivspom_1 >> 8));
                data_Own_UsbBuf[6] = (byte)ivspom_1;

                data_Own_UsbBuf[7] = (byte)(0x00ff & (MSB_2 >> 8));
                data_Own_UsbBuf[8] = (byte)MSB_2;
                data_Own_UsbBuf[9] = (byte)(0x00ff & (LSB_2 >> 8));
                data_Own_UsbBuf[10] = (byte)LSB_2;
                data_Own_UsbBuf[11] = (byte)(0x00ff & (ivspom_2 >> 8));
                data_Own_UsbBuf[12] = (byte)ivspom_2;

                //int b2w = 7+6;

                for (int i = 0; i < 13; i++)
                {
                    data_Own_UsbBuf[i] = (byte)AO_Devices.FTDIController.Bit_reverse(data_Own_UsbBuf[i]);
                }
                return data_Own_UsbBuf;
            }
            
            public override byte[] Create_byteMass_forFastTuneTest(float[] pFreqs,int steps=300,uint pAttenuation = 0)
            {
                ulong lvspom_1;
                float pfreq_1;
                uint ivspom_1 = 3600;

                byte[] data_Buf = new byte[7];
                byte[] data_Own_UsbBuf = new byte[7* steps];

                for (int i =0;i< steps; i++)
                {
                    data_Buf = new byte[7];

                    pfreq_1 = pFreqs[i];
                    ivspom_1 = pAttenuation;
                    
                    if (ivspom_1 == 0)  ivspom_1 = (uint)Get_Intensity_via_HZ(pfreq_1, 0); //Attenuation calculating
                    //check this
                   /* if (ivspom_1 < 2500) ivspom_1 = 2500;
                    if (ivspom_1 > 4000) ivspom_1 = 4000;*/


                    pfreq_1 = ((pfreq_1) * 1e6f); //in Hz //Frequency calculating
                    lvspom_1 = (ulong)((pfreq_1) * (Math.Pow(2.0, 32.0) / Reference_frequency));

                    data_Buf[0] = (byte)0x03; 

                    var datamass_hz = Helper.Processing.uLong_to_4bytes(lvspom_1);
                    for (int j = 0; j < 4; j++) data_Buf[j + 1] = datamass_hz[j];

                    var datamass_at = Helper.Processing.uInt_to_2bytes(ivspom_1);
                    for (int j = 0; j < 2; j++) data_Buf[j + 5] = datamass_at[j];

                    for (int j = 0; j < 7; j++)
                        data_Buf[j] = (byte)AO_Devices.FTDIController.Bit_reverse(data_Buf[j]);

                    for (int j = 0;j<7;j++)
                        data_Own_UsbBuf[7 * i + j] = data_Buf[j];
                }
                return data_Own_UsbBuf;
            }
            public override int Test_fast_tune(int pSteps)
            {
                if (AOD_Loaded_without_fails)
                {
                    try
                    {
                        
                        int steps = pSteps;
                        float[] freq_mass = new float[steps];
                        float delta = (HZ_Max_1 - HZ_Min_1) / (steps == 1 ? steps : (steps - 1));
                        for (int i=0;i<steps;i++)
                        {
                            freq_mass[i] = HZ_Min_1 + delta * i;
                        }
                        Own_UsbBuf = new byte[steps * 7];
                        Own_UsbBuf = Create_byteMass_forFastTuneTest(freq_mass,pSteps,1900);

                        ClearBuffer();
                        WriteUsb(steps * 7);
                        return (int)FTDIController.FT_STATUS.FT_OK;
                    }
                    catch (Exception exc)
                    {
                        return (int)FTDIController.FT_STATUS.FT_OTHER_ERROR;
                    }
                }
                else
                {
                    return (int)FTDIController.FT_STATUS.FT_DEVICE_NOT_FOUND;
                }
            }

            public int Set_ProgrammMode_on()
            {
                try
                {
                    ClearBuffer();
                    WriteUsb(Own_ProgrammBuf, Own_ProgrammBuf.Length);
                }
                catch { return (int)FTDIController.FT_STATUS.FT_IO_ERROR; }
                return (int)FTDIController.FT_STATUS.FT_OK;
            }
            public int Set_ProgrammMode_off()
            {

                //  is_Programmed = false;
                return Set_Hz_both((HZ_Max_1 + HZ_Min_1) / 2.0f, (HZ_Max_2 + HZ_Min_2) / 2.0f);
            }
            public override int Set_Sweep_on(float MHz_start, float Sweep_range_MHz, double Period/*[мс с точностью до двух знаков,минимум 1]*/, bool OnRepeat)
            {
                //здесь MHz_start = m_f0 - начальна частота в МГц    
                //Sweep_range_MHz = m_deltaf - девиация частоты в МГц
                try
                {

                    Own_UsbBuf = new byte[5000];
                    int count = 0;
                    Own_UsbBuf = Create_byteMass_forSweep(MHz_start, Sweep_range_MHz, Period, ref count);
                    
                    try
                    {
                        ClearBuffer();
                        WriteUsb(Own_UsbBuf, Own_UsbBuf.Length);
                        
                    }
                    catch { return (int)FTDIController.FT_STATUS.FT_IO_ERROR; }
                    sAO_Sweep_On = true;
                    return (int)FTDIController.FT_STATUS.FT_OK;
                }
                catch { return (int)FTDIController.FT_STATUS.FT_OTHER_ERROR; }
            }
            public override int Set_Sweep_off()
            {
                return Set_Hz_both(HZ_Current_1,HZ_Current_2);
            }
            public override int Set_OutputPower(byte Percentage)
            {
                try
                {
                    Own_UsbBuf[0] = 0x09; //it means, we will send off command
                    Own_UsbBuf[1] = Percentage;
                    try
                    {
                        ClearBuffer();
                        WriteUsb(2);
                    }
                    catch { return (int)FTDIController.FT_STATUS.FT_IO_ERROR; }
                }
                catch { return (int)FTDIController.FT_STATUS.FT_OTHER_ERROR; }
                return 0;
            }
            protected override int Init_device(uint number)
            {
                AO_Devices.FTDIController.FT_STATUS ftStatus = AO_Devices.FTDIController.FT_STATUS.FT_OTHER_ERROR;

                if (Own_m_hPort == 0)
                {
                    ftStatus = AO_Devices.FTDIController.FT_Open((uint)number, ref Own_m_hPort);
                }

                if (ftStatus == AO_Devices.FTDIController.FT_STATUS.FT_OK)
                {
                    // Set up the port
                    ClearBuffer();
                }
                else
                {
                    return (int)ftStatus;
                }
                Own_UsbBuf[0] = 0x66;
                try
                {
                    ClearBuffer();
                    WriteUsb(1);
                }
                catch { return (int)FTDIController.FT_STATUS.FT_IO_ERROR; }
                return 0;
            }
            protected unsafe int Init_device(string SerialNum)
            {
                AO_Devices.FTDIController_lib.FT_STATUS ftStatus = AO_Devices.FTDIController_lib.FT_STATUS.FT_OTHER_ERROR;

                if (Own_m_hPort == 0)
                {
                    System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();

                    var a = enc.GetBytes(SerialNum);
                    fixed (byte* SerNumBytePointer = a)
                        ftStatus = AO_Devices.FTDIController_lib.FT_OpenEx(SerNumBytePointer, FTDIController.FT_OPEN_BY_SERIAL_NUMBER, ref Own_m_hPort);
                }

                if (ftStatus == AO_Devices.FTDIController_lib.FT_STATUS.FT_OK)
                {
                    // Set up the port
                    ClearBuffer();
                }
                else
                {
                    return (int)ftStatus;
                }
                Own_UsbBuf[0] = 0x66;//пересылаем тестовый байт
                try
                {
                    ClearBuffer();
                    WriteUsb(1); 
                }
                catch { return (int)FTDIController_lib.FT_STATUS.FT_IO_ERROR; }
                return 0;
            }
            protected override int Deinit_device()
            {
                System.Threading.Thread.Sleep(100);
                int result = Convert.ToInt16(AO_Devices.FTDIController.FT_Close(Own_m_hPort));
                return result;
            }
            public override string Ask_required_dev_file()
            {
                return ("(special *.dev file)");
            }
            public static unsafe int Search_Devices(out string data_DeflectorDescriptor_or_name, out uint data_dwListDescFlags)
            {
                FTDIController.FT_STATUS ftStatus = FTDIController.FT_STATUS.FT_OTHER_ERROR;
                UInt32 numDevs;
                int countofdevs_to_return = 0;
                int i;
                byte[] sDevName = new byte[64];
                void* p1;

                p1 = (void*)&numDevs;
                ftStatus = FTDIController.FT_ListDevices(p1, null, FTDIController.FT_LIST_NUMBER_ONLY);
                countofdevs_to_return = (int)numDevs;
                data_dwListDescFlags = FTDIController.FT_LIST_BY_INDEX_OPEN_BY_DESCRIPTION;
                string datastring = "";
                if (ftStatus == FTDIController.FT_STATUS.FT_OK)
                {
                    if (data_dwListDescFlags == FTDIController.FT_LIST_ALL)
                    {
                        for (i = 0; i < numDevs; i++)
                        {
                            //cmbDevList.Items.Add(i);
                        }
                    }
                    else
                    {

                        for (i = 0; i < numDevs; i++) // пройдемся по девайсам и спросим у них дескрипторы
                        {
                            sDevName = new byte[64];
                            fixed (byte* pBuf = sDevName)
                            {
                                ftStatus = FTDIController.FT_ListDevices((UInt32)i, pBuf, data_dwListDescFlags);
                                if (ftStatus == FTDIController.FT_STATUS.FT_OK)
                                {
                                    //string str;
                                    System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();
                                    datastring = enc.GetString(sDevName, 0, sDevName.Length);
                                    //cmbDevList.Items.Add(str);
                                }
                                else
                                {
                                    data_DeflectorDescriptor_or_name = null;
                                    return (int)ftStatus;
                                }
                            }
                        }
                    }
                }
                data_DeflectorDescriptor_or_name = datastring;
                return countofdevs_to_return;
            }
            public static unsafe int Search_Devices(out string[] DeflectorNames, out string[] DeflectorSerials)
            {
                FTDIController_lib.FT_STATUS ftStatus = FTDIController_lib.FT_STATUS.FT_OTHER_ERROR;
                UInt32 numDevs;
                int countofdevs_to_return = 0;
                int i; int NumberOfSym_max = 64;
                void* p1 = (void*)&numDevs;

                ftStatus = FTDIController_lib.FT_ListDevices(p1, null, FTDIController_lib.FT_LIST_NUMBER_ONLY);
                countofdevs_to_return = (int)numDevs;

                var ListDescFlag = FTDIController_lib.FT_LIST_BY_INDEX_OPEN_BY_DESCRIPTION;
                var ListSerialFlag = FTDIController_lib.FT_LIST_BY_INDEX_OPEN_BY_SERIAL;

                DeflectorNames = new string[numDevs];
                DeflectorSerials = new string[numDevs];
                List<string> DeflectorNames_real = new List<string>();
                List<string> DeflectorSerials_real = new List<string>();

                List<byte[]> sDevNames = new List<byte[]>();
                List<byte[]> sDevSerials = new List<byte[]>();

                if (ftStatus == FTDIController_lib.FT_STATUS.FT_OK)
                {
                    for (i = 0; i < numDevs; i++) // пройдемся по девайсам и спросим у них дескрипторы
                    {
                        sDevNames.Add(new byte[NumberOfSym_max]);
                        sDevSerials.Add(new byte[NumberOfSym_max]);

                        fixed (byte* pBuf_name = sDevNames[i])
                        {
                            fixed (byte* pBuf_serial = sDevSerials[i])
                            {
                                ftStatus = FTDIController_lib.FT_ListDevices((UInt32)i, pBuf_name, ListDescFlag);
                                ftStatus = FTDIController_lib.FT_ListDevices((UInt32)i, pBuf_serial, ListSerialFlag);
                                if (ftStatus == FTDIController_lib.FT_STATUS.FT_OK)
                                {
                                    System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();
                                    DeflectorNames[i] = enc.GetString(sDevNames[i], 0, NumberOfSym_max);
                                    DeflectorSerials[i] = enc.GetString(sDevSerials[i], 0, NumberOfSym_max);
                                    if (!DeflectorNames[i].Contains("Filter"))
                                    {
                                        DeflectorNames_real.Add(Helper.Processing.RemoveZeroBytesFromString(DeflectorNames[i]));
                                        DeflectorSerials_real.Add(Helper.Processing.RemoveZeroBytesFromString(DeflectorSerials[i]));
                                    }
                                    else countofdevs_to_return--;
                                }
                                else
                                {
                                    DeflectorNames = null;
                                    return (int)ftStatus;
                                }
                            }
                        }
                    }
                }
                DeflectorNames = DeflectorNames_real.ToArray();
                DeflectorSerials = DeflectorSerials_real.ToArray();
                return countofdevs_to_return;
            }
            public override string Implement_Error(int pCode_of_error)
            {
                return ((FTDIController.FT_STATUS)pCode_of_error).ToString();
            }
            public override int PowerOn()
            {
                var state = Set_Hz_both((HZ_Max_1 + HZ_Min_1) / 2.0f, (HZ_Max_2 + HZ_Min_2) / 2.0f);
                sAOF_isPowered = true;
                return state;
            }
            public override int PowerOff()
            {
                try
                {
                    System.Threading.Thread.Sleep(300);
                    Own_UsbBuf[0] = 0x05; //it means, we will send off command

                    for (int i = 1; i < 2; i++) Own_UsbBuf[i] = 0;
                    Own_UsbBuf[0] = (byte)FTDIController.Bit_reverse(Own_UsbBuf[0]);
                    try {
                        ClearBuffer();
                        WriteUsb(1);
                    }
                    catch { return (int)FTDIController.FT_STATUS.FT_IO_ERROR; }
                }
                catch { return (int)FTDIController.FT_STATUS.FT_OTHER_ERROR; }
                sAOF_isPowered = false;
                return 0;
            }
            public override int Read_dev_file(string path)
            {
                try
                {
                    base.Read_dev_file(path);
                }
                catch
                {
                    return (int)FTDIController.FT_STATUS.FT_OTHER_ERROR;
                }
                return (int)FTDIController.FT_STATUS.FT_OK;
            }
            public override void Dispose()
            {
                Deinit_device();
            }

            #region Перегрузки WriteUsb
            //Перегрузки, которую можно юзать
            public unsafe bool WriteUsb()
            {
                int count_in = Own_UsbBuf.Length;
                return AO_Devices.FTDIController.WriteUsb(Own_m_hPort, count_in, Own_UsbBuf);
            }

            //Перегрузка, которую юзаем везде
            public unsafe bool WriteUsb(int count)
            { return AO_Devices.FTDIController.WriteUsb(Own_m_hPort, count, Own_UsbBuf); }
            public unsafe bool WriteUsb(byte[] ByteMass, int count)
            { return AO_Devices.FTDIController.WriteUsb(Own_m_hPort, count, ByteMass); }
            #endregion
        }
        public class Emulator_of_Deflector : AO_Deflector
        {
            public override DeflectorTypes DeflectorType { get { return DeflectorTypes.Emulator; } }
            protected override string _DeflectorName { set; get; }
            protected override string _DeflectorSerial { set; get; }
            protected override string DeflectorCfgName { set; get; }
            protected override string DeflectorCfgPath { set; get; }
            protected override string DllName { set; get; }

            protected override float[] HZs_1 { set; get; }
            protected override float[] Angles_1 { set; get; }
            protected override float[] Attenuation_1 { set; get; }
            protected override float[] HZs_2 { set; get; }
            protected override float[] Angles_2 { set; get; }
            protected override float[] Attenuation_2 { set; get; }

            protected override bool AOD_Loaded_without_fails { set; get; }
            protected override bool sAOF_isPowered { set; get; }

            public override float Angle_Max_1 { get { return Angles_1[Angles_1.Length - 1]; } }
            public override float Angle_Min_1 { get { return Angles_1[0]; } }
            public override float HZ_Max_1 { get { return HZs_1[0]; } }
            public override float HZ_Min_1 { get { return HZs_1[HZs_1.Length - 1]; } }
            public override float Angle_Max_2 { get { return Angles_2[Angles_2.Length - 1]; } }
            public override float Angle_Min_2 { get { return Angles_2[0]; } }
            public override float HZ_Max_2 { get { return HZs_2[0]; } }
            public override float HZ_Min_2 { get { return HZs_2[HZs_2.Length - 1]; } }

           
            protected override bool sAO_Sweep_On { set; get; }
            protected override bool sAO_ProgrammMode_Ready { set; get; }

            private byte[] Own_UsbBuf = new byte[5000];
            private byte[] Own_ProgrammBuf;
            private UInt32 Own_dwListDescFlags = 0;
            private UInt32 Own_m_hPort = 0;

            public override event SetNotifier onSetHz;

            public Emulator_of_Deflector() : base()
            {
                var Random = new Random();
                var i_max = Random.Next(4, 10); var num = 0;
                for (int i = 0; i < i_max; i++) num = Random.Next(100000, 999999);

                _DeflectorName = "Deflector "+DeflectorType.ToString();
                _DeflectorSerial = "D" + num.ToString();

                AOD_Loaded_without_fails = true;
                sAO_ProgrammMode_Ready = false;
            }
            ~Emulator_of_Deflector()
            {
                this.PowerOff();
                this.Dispose();
            }

            public override int Set_Hz_both(float pfreq_1, float pfreq_2,uint AT_1=0,uint AT_2 = 0, bool ignore_exc = false)
            {
                base.Set_Hz_both(pfreq_1, pfreq_2, AT_1, AT_2, ignore_exc);
                if (AOD_Loaded_without_fails)
                {
                    try
                    {
                        Own_UsbBuf = Create_byteMass_forHzTune(pfreq_1, pfreq_2,AT_1,AT_2);
                        sAttenuation_1_current = (AT_1 == 0) ? (uint)Get_Intensity_via_HZ(pfreq_1, 0): AT_1;
                        sAttenuation_2_current = (AT_2 == 0) ? (uint)Get_Intensity_via_HZ(pfreq_2, 1) : AT_2;
                        sAngle_Current_1 = Get_Angle_via_HZ(pfreq_1, 0);
                        sAngle_Current_2 = Get_Angle_via_HZ(pfreq_2, 1);
                        sHZ_Current_1 = pfreq_1;
                        sHZ_Current_2 = pfreq_2;
                        onSetHz?.Invoke(this, Angle_Current_1, HZ_Current_1, Angle_Current_2, HZ_Current_2);
                        return 0;
                    }
                    catch (Exception exc)
                    {
                        return (int)FTDIController.FT_STATUS.FT_OTHER_ERROR;
                    }
                }
                else
                {
                    return (int)FTDIController.FT_STATUS.FT_DEVICE_NOT_FOUND;
                }
            }

    
            private byte[] Create_byteMass_forSweep(float pMHz_start, float pSweep_range_MHz, double pPeriod/*[мс с точностью до двух знаков,минимум 1]*/, ref int pcount)
            {
                float freq, fvspom;
                short MSB, LSB;
                ulong lvspom;
                uint ivspom;
                float delta;
                int steps;
                int i;
                float freqMCU = 74.99e6f;
                float inp_freq = 20 * 1000.0f / (float)pPeriod; //in Hz, max 4500 hz //дефолт от Алексея
                double New_Freq_byTime = (pSweep_range_MHz * 1e3f / pPeriod); // [kHz/ms] , 57.4 и более //375
                double Step_kHZs = pSweep_range_MHz * 1e3f / 20.0f;                                     //   было 200, [kHz] // В новом режиме 500 kHz - дефолт от Алексея 
                double Steps_by1MHz = 1e3f / Step_kHZs;                     //      [шагов/МГц]   
                pcount = 4 + 2 - 1;//4 бита на начальный массив, 2 на таймер
                steps = 20;
                double Step_HZs = Step_kHZs * 1000;

                int total_count = pcount + 1 + 4 * steps + 3;
                byte[] data_Own_UsbBuf = new byte[total_count];
                byte[] Start_mass = new byte[4] { (byte)FTDIController.Bit_reverse(0x14), (byte)FTDIController.Bit_reverse(0x11), (byte)FTDIController.Bit_reverse(0x12), (byte)FTDIController.Bit_reverse(0xff) };
                byte[] Finish_mass = new byte[3] { (byte)FTDIController.Bit_reverse(0x15), (byte)FTDIController.Bit_reverse(0x15), (byte)FTDIController.Bit_reverse(0x15) };

                for (i = 1; i < total_count; i++)
                {
                    data_Own_UsbBuf[i] = 0;
                }
                for (i = 0; i < steps; i++)//перепроверить цикл
                {

                    freq = (float)(((pMHz_start) * 1e6f + i * Step_HZs)/*/ 1.17f*/);//1.17 коррекция частоты
                                                                                    //fvspom=freq/300e6;
                    lvspom = (ulong)((freq) * (Math.Pow(2.0, 32.0) / Reference_frequency)); //расчет управляющего слова для частоты
                    MSB = (short)(0x0000ffFF & (lvspom >> 16));
                    LSB = (short)lvspom;
                    data_Own_UsbBuf[pcount + 1] = (byte)(0x00ff & (MSB >> 8));
                    data_Own_UsbBuf[pcount + 2] = (byte)(MSB);
                    data_Own_UsbBuf[pcount + 3] = (byte)(0x00ff & (LSB >> 8));
                    data_Own_UsbBuf[pcount + 4] = (byte)(LSB);
                    pcount += 4;
                }

                pcount -= 4;//компенсация последнего смещения

                //timer calculations
                ivspom = (uint)(65536 - freqMCU / (2 * 2 * inp_freq));

                data_Own_UsbBuf[4] = (byte)(0x00ff & (ivspom >> 8)); ;
                data_Own_UsbBuf[5] = (byte)ivspom;
                pcount = total_count;
                //
                for (i = 0; i < total_count; i++)
                {
                    data_Own_UsbBuf[i] = (byte)FTDIController.Bit_reverse(data_Own_UsbBuf[i]);
                }
                for (i = 0; i < 4; i++)
                {
                    data_Own_UsbBuf[i] = Start_mass[i];
                }
                for (i = 0; i < 3; i++)
                {
                    data_Own_UsbBuf[i + total_count - 3] = Finish_mass[i];
                }
                return data_Own_UsbBuf;
            }

            private byte[] Create_byteMass_forHzTune(float pfreq_1, float pfreq_2, uint pCoef_PowerDecrease_1 = 0, uint pCoef_PowerDecrease_2 = 0)
            {
                float fvspom_1, fvspom_2;
                short MSB_1, MSB_2, LSB_1, LSB_2;
                ulong lvspom_1, lvspom_2;

                float freq_was_1 = pfreq_1;
                float freq_was_2 = pfreq_2;
                byte[] data_Own_UsbBuf = new byte[5000];
                uint ivspom_1 = 2700, ivspom_2 = 2700;
                if (pCoef_PowerDecrease_1 == 0)
                {
                    ivspom_1 = (uint)Get_Intensity_via_HZ(pfreq_1, 0);
                }
                else
                {
                    ivspom_1 = (uint)pCoef_PowerDecrease_1;
                }
                if (pCoef_PowerDecrease_2 == 0)
                {
                    ivspom_2 = (uint)Get_Intensity_via_HZ(pfreq_2, 2);
                }
                else
                {
                    ivspom_2 = (uint)pCoef_PowerDecrease_2;
                }
                pfreq_1 = ((freq_was_1) * 1e6f); //in Hz
                pfreq_2 = ((freq_was_2) * 1e6f); //in Hz

                fvspom_1 = (float)(pfreq_1 / Reference_frequency);
                fvspom_2 = (float)(pfreq_2 / Reference_frequency);

                lvspom_1 = (ulong)((pfreq_1) * (Math.Pow(2.0, 32.0) / Reference_frequency));
                lvspom_2 = (ulong)((pfreq_2) * (Math.Pow(2.0, 32.0) / Reference_frequency));

                MSB_1 = (short)(0x0000ffFF & (lvspom_1 >> 16));
                LSB_1 = (short)lvspom_1;
                MSB_2 = (short)(0x0000ffFF & (lvspom_2 >> 16));
                LSB_2 = (short)lvspom_2;

                data_Own_UsbBuf[0] = 0xDE; //222 в десятичной системе

                data_Own_UsbBuf[1] = (byte)(0x00ff & (MSB_1 >> 8));
                data_Own_UsbBuf[2] = (byte)MSB_1;
                data_Own_UsbBuf[3] = (byte)(0x00ff & (LSB_1 >> 8));
                data_Own_UsbBuf[4] = (byte)LSB_1;
                data_Own_UsbBuf[5] = (byte)(0x00ff & (ivspom_1 >> 8));
                data_Own_UsbBuf[6] = (byte)ivspom_1;

                data_Own_UsbBuf[7] = (byte)(0x00ff & (MSB_2 >> 8));
                data_Own_UsbBuf[8] = (byte)MSB_2;
                data_Own_UsbBuf[9] = (byte)(0x00ff & (LSB_2 >> 8));
                data_Own_UsbBuf[10] = (byte)LSB_2;
                data_Own_UsbBuf[11] = (byte)(0x00ff & (ivspom_2 >> 8));
                data_Own_UsbBuf[12] = (byte)ivspom_2;

                int b2w = 7 + 6;

                for (int i = 0; i < b2w; i++)
                {
                    data_Own_UsbBuf[i] = (byte)AO_Devices.FTDIController.Bit_reverse(data_Own_UsbBuf[i]);
                }
                return data_Own_UsbBuf;
            }
            public int Set_ProgrammMode_on()
            {
                return 0;
            }
            public int Set_ProgrammMode_off()
            {
                return Set_Hz_both((HZ_Max_1 + HZ_Min_1) / 2.0f, (HZ_Max_2 + HZ_Min_2) / 2.0f);
            }
            public override int Set_Sweep_on(float MHz_start, float Sweep_range_MHz, double Period/*[мс с точностью до двух знаков,минимум 1]*/, bool OnRepeat)
            {
                //здесь MHz_start = m_f0 - начальна частота в МГц    
                //Sweep_range_MHz = m_deltaf - девиация частоты в МГц
                try
                {
                    Own_UsbBuf = new byte[5000];
                    int count = 0;
                    Own_UsbBuf = Create_byteMass_forSweep(MHz_start, Sweep_range_MHz, Period, ref count);
                    sAO_Sweep_On = true;
                    return 0;
                }
                catch { return (int)FTDIController.FT_STATUS.FT_OTHER_ERROR; }
            }
            public override int Set_Sweep_off()
            {
                return Set_Hz_both(HZ_Current_1, HZ_Current_2);
            }
            public override int Set_OutputPower(byte Percentage)
            {
                return 0;
            }
            protected override int Init_device(uint number)
            {
                return 0;
            }
            protected override int Deinit_device()
            {
                return 0;
            }
            public override string Ask_required_dev_file()
            {
                return ("(any *.dev file)");
            }
            public static unsafe int Search_Devices()
            {
                return 1;
            }
            public override string Implement_Error(int pCode_of_error)
            {
                if (pCode_of_error != 0) return "Тип дефлектора - эмулятор. Программная ошибка";
                else return "Операция прошла успешно";
            }
            public override int PowerOn()
            {
                var state = Set_Hz_both((HZ_Max_1 + HZ_Min_1) / 2.0f, (HZ_Max_2 + HZ_Min_2) / 2.0f);
                sAOF_isPowered = true;
                return state;
            }
            public override int PowerOff()
            {
                sAOF_isPowered = false;
                return 0;
            }
            public override int Read_dev_file(string path)
            {
                try
                {
                    base.Read_dev_file(path);
                }
                catch
                {
                    return (int)FTDIController.FT_STATUS.FT_OTHER_ERROR;
                }
                return (int)FTDIController.FT_STATUS.FT_OK;
            }
            public override void Dispose()
            {
                Deinit_device();
            }                   
        }

        private static class FTDIController
        {
            const string ftdi_dllname = "FTD2XX.dll";

            public enum FT_STATUS//:Uint32
            {
                FT_OK = 0,
                FT_INVALID_HANDLE,
                FT_DEVICE_NOT_FOUND,
                FT_DEVICE_NOT_OPENED,
                FT_IO_ERROR,
                FT_INSUFFICIENT_RESOURCES,
                FT_INVALID_PARAMETER,
                FT_INVALID_BAUD_RATE,
                FT_DEVICE_NOT_OPENED_FOR_ERASE,
                FT_DEVICE_NOT_OPENED_FOR_WRITE,
                FT_FAILED_TO_WRITE_DEVICE,
                FT_EEPROM_READ_FAILED,
                FT_EEPROM_WRITE_FAILED,
                FT_EEPROM_ERASE_FAILED,
                FT_EEPROM_NOT_PRESENT,
                FT_EEPROM_NOT_PROGRAMMED,
                FT_INVALID_ARGS,
                FT_OTHER_ERROR,
                FT_POWER_PROBLEM // 08.02.2021. In case of problems with connection of the power supply or smth else
            };

            public const UInt32 FT_BAUD_300 = 300;
            public const UInt32 FT_BAUD_600 = 600;
            public const UInt32 FT_BAUD_1200 = 1200;
            public const UInt32 FT_BAUD_2400 = 2400;
            public const UInt32 FT_BAUD_4800 = 4800;
            public const UInt32 FT_BAUD_9600 = 9600;
            public const UInt32 FT_BAUD_14400 = 14400;
            public const UInt32 FT_BAUD_19200 = 19200;
            public const UInt32 FT_BAUD_38400 = 38400;
            public const UInt32 FT_BAUD_57600 = 57600;
            public const UInt32 FT_BAUD_115200 = 115200;
            public const UInt32 FT_BAUD_230400 = 230400;
            public const UInt32 FT_BAUD_460800 = 460800;
            public const UInt32 FT_BAUD_921600 = 921600;

            public const UInt32 FT_LIST_NUMBER_ONLY = 0x80000000;
            public const UInt32 FT_LIST_BY_INDEX = 0x40000000;
            public const UInt32 FT_LIST_ALL = 0x20000000;
            public const UInt32 FT_OPEN_BY_SERIAL_NUMBER = 1;
            public const UInt32 FT_OPEN_BY_DESCRIPTION = 2;

            public const UInt32 FT_LIST_BY_INDEX_OPEN_BY_DESCRIPTION = FT_LIST_BY_INDEX | FT_OPEN_BY_DESCRIPTION;
            public const UInt32 FT_LIST_BY_INDEX_OPEN_BY_SERIAL = FT_LIST_BY_INDEX | FT_OPEN_BY_SERIAL_NUMBER;

            // Word Lengths
            public const byte FT_BITS_8 = 8;
            public const byte FT_BITS_7 = 7;
            public const byte FT_BITS_6 = 6;
            public const byte FT_BITS_5 = 5;

            // Stop Bits
            public const byte FT_STOP_BITS_1 = 0;
            public const byte FT_STOP_BITS_1_5 = 1;
            public const byte FT_STOP_BITS_2 = 2;

            // Parity
            public const byte FT_PARITY_NONE = 0;
            public const byte FT_PARITY_ODD = 1;
            public const byte FT_PARITY_EVEN = 2;
            public const byte FT_PARITY_MARK = 3;
            public const byte FT_PARITY_SPACE = 4;

            // Flow Control
            public const UInt16 FT_FLOW_NONE = 0;
            public const UInt16 FT_FLOW_RTS_CTS = 0x0100;
            public const UInt16 FT_FLOW_DTR_DSR = 0x0200;
            public const UInt16 FT_FLOW_XON_XOFF = 0x0400;

            // Purge rx and tx buffers
            public const byte FT_PURGE_RX = 1;
            public const byte FT_PURGE_TX = 2;

            // Events
            public const UInt32 FT_EVENT_RXCHAR = 1;
            public const UInt32 FT_EVENT_MODEM_STATUS = 2;


            //public static byte* pBuf;
            [DllImport(ftdi_dllname)]
            public static extern unsafe FT_STATUS FT_ListDevices(void* pvArg1, void* pvArg2, UInt32 dwFlags);  // FT_ListDevices by number only
            [DllImport(ftdi_dllname)]
            public static extern unsafe FT_STATUS FT_ListDevices(UInt32 pvArg1, void* pvArg2, UInt32 dwFlags); // FT_ListDevcies by serial number or description by index only
            [DllImport(ftdi_dllname)]
            public static extern FT_STATUS FT_Open(UInt32 uiPort, ref FT_HANDLE ftHandle);
            [DllImport(ftdi_dllname)]
            public static extern unsafe FT_STATUS FT_OpenEx(void* pvArg1, UInt32 dwFlags, ref FT_HANDLE ftHandle);
            [DllImport(ftdi_dllname)]
            public static extern FT_STATUS FT_Close(FT_HANDLE ftHandle);
            [DllImport(ftdi_dllname)]
            public static extern unsafe FT_STATUS FT_Read(FT_HANDLE ftHandle, void* lpBuffer, UInt32 dwBytesToRead, ref UInt32 lpdwBytesReturned);
            [DllImport(ftdi_dllname)]
            public static extern unsafe FT_STATUS FT_Write(FT_HANDLE ftHandle, void* lpBuffer, UInt32 dwBytesToRead, ref UInt32 lpdwBytesWritten);
            [DllImport(ftdi_dllname)]
            public static extern unsafe FT_STATUS FT_SetBaudRate(FT_HANDLE ftHandle, UInt32 dwBaudRate);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_SetDataCharacteristics(FT_HANDLE ftHandle, byte uWordLength, byte uStopBits, byte uParity);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_SetFlowControl(FT_HANDLE ftHandle, char usFlowControl, byte uXon, byte uXoff);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_SetDtr(FT_HANDLE ftHandle);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_ClrDtr(FT_HANDLE ftHandle);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_SetRts(FT_HANDLE ftHandle);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_ClrRts(FT_HANDLE ftHandle);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_GetModemStatus(FT_HANDLE ftHandle, ref UInt32 lpdwModemStatus);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_SetChars(FT_HANDLE ftHandle, byte uEventCh, byte uEventChEn, byte uErrorCh, byte uErrorChEn);
            [DllImport(ftdi_dllname)]
            public static extern unsafe FT_STATUS FT_Purge(FT_HANDLE ftHandle, UInt32 dwMask);
            [DllImport(ftdi_dllname)]
            public static extern unsafe FT_STATUS FT_SetTimeouts(FT_HANDLE ftHandle, UInt32 dwReadTimeout, UInt32 dwWriteTimeout);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_GetQueueStatus(FT_HANDLE ftHandle, ref UInt32 lpdwAmountInRxQueue);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_SetBreakOn(FT_HANDLE ftHandle);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_SetBreakOff(FT_HANDLE ftHandle);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_GetStatus(FT_HANDLE ftHandle, ref UInt32 lpdwAmountInRxQueue, ref UInt32 lpdwAmountInTxQueue, ref UInt32 lpdwEventStatus);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_SetEventNotification(FT_HANDLE ftHandle, UInt32 dwEventMask, void* pvArg);
            [DllImport(ftdi_dllname)]
            public static extern unsafe FT_STATUS FT_ResetDevice(FT_HANDLE ftHandle);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_SetDivisor(FT_HANDLE ftHandle, char usDivisor);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_GetLatencyTimer(FT_HANDLE ftHandle, ref byte pucTimer);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_SetLatencyTimer(FT_HANDLE ftHandle, byte ucTimer);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_GetBitMode(FT_HANDLE ftHandle, ref byte pucMode);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_SetBitMode(FT_HANDLE ftHandle, byte ucMask, byte ucEnable);
            [DllImport(ftdi_dllname)]
            static extern unsafe FT_STATUS FT_SetUSBParameters(FT_HANDLE ftHandle, UInt32 dwInTransferSize, UInt32 dwOutTransferSize);

            //Сама функция
#if X86
            public static unsafe bool WriteUsb(uint pm_hPort, int count, byte[] pUsbBuf)
#elif X64
            public static unsafe bool WriteUsb(UInt64 pm_hPort, int count, byte[] pUsbBuf)
#endif
            {
                UInt32 dwRet = 0;
                FTDIController.FT_STATUS ftStatus = FTDIController.FT_STATUS.FT_OTHER_ERROR;
                byte[] cBuf = new Byte[count + 1];

                fixed (byte* pBuf = pUsbBuf)
                {
                    ftStatus = FTDIController.FT_Write(pm_hPort, pBuf, (uint)(count + 1), ref dwRet);
                }
                if (ftStatus != FTDIController.FT_STATUS.FT_OK)
                {
                    throw new Exception("Failed To Write " + Convert.ToString(ftStatus));
                }
                return false;
            }
            public static unsafe bool WriteUsb(uint pm_hPort, byte[] pUsbBuf)
            {
                int count_in = pUsbBuf.Length;
                return AO_Devices.FTDIController.WriteUsb(pm_hPort, count_in, pUsbBuf);
            }

            public static int Bit_reverse(int input)
            {
                int output = 0;
               /* const int uchar_size = 8;

                for (int i = 0; i != uchar_size; ++i)
                {
                    output |= ((input >> i) & 1) << (uchar_size - 1 - i);
                }*/
                output = input;
                return output;
            }


        }

        public enum DeflectorTypes
        {
            Emulator = 0,
            STC_Deflector
        }

    }
}
