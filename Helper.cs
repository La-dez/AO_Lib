using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

namespace AO_Lib
{
    public class Helper
    {
        public class Math
        {
            public static double Interpolate_value(double x1, double y1, double x2, double y2, double x_tofind)
            {
                return ((y2 - y1) / (x2 - x1)) * (x_tofind - x1) + y1;
            }
            public static List<PointF> Interpolate_curv(float[] Wls, float[] Hzs)
            {
                List<PointF> result = new List<PointF>();
                int count_of_gaps = Wls.Length - 1;

                for (int i = 0; i < count_of_gaps; i++)
                {
                    result.Add(new PointF(Wls[i], Hzs[i]));
                    float count_of_wls_to_restruct = (Wls[i + 1] - Wls[i] - 1);
                    for (int j = 1; j <= count_of_wls_to_restruct; j++)
                    {
                        float new_hz_val = Hzs[i] + j * ((float)(Hzs[i + 1] - Hzs[i])) / (Wls[i + 1] - Wls[i]);// = (x- Hzs[i] )/ (float)j;
                        result.Add(new PointF(Wls[i] + j, new_hz_val));
                    }
                }
                result.Add(new PointF(Wls[count_of_gaps], Hzs[count_of_gaps]));
                return result;
            }
            public static void Interpolate_curv(ref float[] Wls, ref float[] Hzs, float precision = 1f)
            {
                List<PointF> result = new List<PointF>();
                if (precision != 1)
                {
                    for (int i = 0; i < Wls.Length; i++)
                    {
                        Wls[i] /= precision;
                    }
                }
                int count_of_gaps = Wls.Length - 1;

                for (int i = 0; i < count_of_gaps; i++)
                {
                    result.Add(new PointF(Wls[i], Hzs[i]));
                    float count_of_wls_to_restruct = (Wls[i + 1] - Wls[i] - 1);
                    for (int j = 1; j <= count_of_wls_to_restruct; j++)
                    {
                        float new_hz_val = Hzs[i] + j * ((float)(Hzs[i + 1] - Hzs[i])) / (Wls[i + 1] - Wls[i]);// = (x- Hzs[i] )/ (float)j;
                        result.Add(new PointF(Wls[i] + j, new_hz_val));
                    }
                }
                result.Add(new PointF(Wls[count_of_gaps], Hzs[count_of_gaps]));
                int data_count = result.Count;
                Wls = new float[data_count];
                Hzs = new float[data_count];
                for (int i = 0; i < data_count; i++)
                {
                    Wls[i] = result[i].X;
                    Hzs[i] = result[i].Y;
                }
                if (precision != 1)
                {
                    for (int i = 0; i < Wls.Length; i++)
                    {
                        Wls[i] *= precision;
                    }
                }
            }
        }
        public class Files
        {
            public static List<string> Read_txt(string path)
            {
                string[] AllLines = System.IO.File.ReadAllLines(path);
                List<string> result = new List<string>(AllLines);
                return result;
            }
            public static bool Write_txt(string path, List<string> data)
            {
                bool result = false;
                try
                {
                    string[] AllLines = new string[data.Count];
                    data.CopyTo(AllLines);
                    System.IO.File.WriteAllLines(path, AllLines);
                    result = true;
                }
                catch
                {
                    result = false;
                }
                return result;
            }

            public static void AddString_2file(string FileName, string str)
            {
                List<string> data = new List<string>();
                try { data = Read_txt(FileName); } catch { }
                data.Add(str);
                Write_txt(FileName, data);
            }

            public static void Get_WLData_byKnownCountofNumbers(int countofnumbers, string[] AllStrings,
           out float[] pWls, out float[] pHzs, out float[] pCoefs)
            {

                float CurWl = 0, CurHz = 0, CurCoef = 0;
                List<float> dWls = new List<float>();
                List<float> dHzs = new List<float>();
                List<float> dCoefs = new List<float>();

                float[] Params = new float[countofnumbers];
                for (int i = 0; i < AllStrings.Length; ++i)
                {
                    try
                    {
                        Get_WLData_fromDevString(AllStrings[i], countofnumbers, Params);
                        dHzs.Add(Params[0]); dWls.Add(Params[1]); dCoefs.Add(Params[2]);
                    }
                    catch
                    {
                        continue;
                    }
                }
                dWls.Reverse();
                dHzs.Reverse();
                dCoefs.Reverse();

                pWls = dWls.ToArray();
                pHzs = dHzs.ToArray();
                pCoefs = dCoefs.ToArray();
            }



            public static void Get_WLData_fromDevString(string datastring, int NumberOfParamsInString, float[] pPars)
            {
                int startindex = 0; bool startfound = false;
                int finishindex = 0; bool finishfound = false;
                List<float> datavalues = new List<float>();

                for (int i = 0; i < datastring.Length; i++)
                {
                    if ((datastring[i] != ' ') && ((Char.IsDigit(datastring[i])) || (datastring[i] == '.') || (datastring[i] == ',') || (datastring[i] == '-')))
                    {
                        if (startfound)
                        {
                            finishindex++;
                        }
                        else
                        {
                            startindex = i;
                            startfound = true;
                        }
                    }
                    else
                    {
                        if (startfound)
                        {
                            finishindex = i;
                            finishfound = true;
                        }
                    }
                    if (startfound && finishfound)
                    {
                        string data = datastring.Substring(startindex, finishindex - startindex);
                        double result = 0;
                        double.TryParse((data.Replace(',', '.')), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out result);
                        datavalues.Add((float)result);
                        startfound = finishfound = false;
                    }
                }
                if (startfound && !finishfound)
                {
                    string data = datastring.Substring(startindex);
                    double result = 0;
                    double.TryParse((data.Replace(',', '.')), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out result);
                    datavalues.Add((float)result);
                }
                for (int i = 0; i < NumberOfParamsInString; i++) { pPars[i] = datavalues[i]; }
            }

            public static void Get_Data_fromDevFile(string[] AllStrings, out List<float[]> Colomns)
            {
                List<float[]> DataRows = new List<float[]>();
                List<float> Params = new List<float>();
                for (int i = 0; i < AllStrings.Length; ++i)
                {
                    try
                    {
                        Get_Data_fromString(AllStrings[i], ref Params);
                        DataRows.Add(Params.ToArray());
                    }
                    catch
                    {
                        continue;
                    }
                }
                if (DataRows[0][0] < DataRows[DataRows.Count - 1][0]) DataRows.Reverse();
                Colomns = new List<float[]>();
                for (int i = 0; i < DataRows[0].Length; i++)
                {
                    float[] Colomn_of_vals = new float[DataRows.Count];
                    for (int j = 0; j < Colomn_of_vals.Length; j++)
                    {
                        Colomn_of_vals[j] = DataRows[j][i];
                    }
                    Colomns.Add(Colomn_of_vals);
                }
            }
            public static void Get_Data_fromString(string datastring, ref List<float> pPars)
            {
                int startindex = 0; bool startfound = false;
                int finishindex = 0; bool finishfound = false;
                List<float> datavalues = new List<float>();

                //Оказывается, не во всех системах все читается корректно. Делаем проверку
                bool isDotNeeded = false;
                double datastr = 0;
                try { datastr = Convert.ToDouble(("0.1155").Replace('.', ',')); isDotNeeded = false; }
                catch { isDotNeeded = true; }
                try { datastr = Convert.ToDouble(("0,1155").Replace(',', '.')); isDotNeeded = true; }
                catch { isDotNeeded = false; }


                for (int i = 0; i < datastring.Length; i++)
                {
                    if (((datastring[i] != ' ') || (datastring[i] != '\t'))
                        && ((Char.IsDigit(datastring[i])) || (datastring[i] == '.') || (datastring[i] == ',') || (datastring[i] == '-')))
                    {
                        if (startfound)
                        {
                            finishindex++;
                        }
                        else
                        {
                            startindex = i;
                            startfound = true;
                        }
                    }
                    else
                    {
                        if (startfound)
                        {
                            finishindex = i;
                            finishfound = true;
                        }
                    }
                    if (startfound && finishfound)
                    {
                        double result = 0;
                        string data = datastring.Substring(startindex, finishindex - startindex);
                        double.TryParse((data.Replace(',', '.')), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out result);
                        double.TryParse((data.Replace(',', '.')), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out result);
                        datavalues.Add((float)result);
                        startfound = finishfound = false;
                    }
                }
                if (startfound && !finishfound)
                {
                    string data = datastring.Substring(startindex);
                    double result = 0;
                    double.TryParse((data.Replace(',', '.')), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out result);
                    datavalues.Add((float)result);
                }
                pPars = datavalues;

            }
        }
        public class Processing
        {
            public static string RemoveZeroBytesFromString(string param)
            {
                string result = param;
                while (result.Length != 0)
                {
                    if (result.Last() == '\0') result = RemoveZeroByteFromTheEnd(result);
                    else break;
                }
                if (result.Length == 0) result = "undefined";
                result += '\0';
                return result;
            }

            public static string RemoveZeroByteFromTheEnd(string param)
            {
                return ((param.Last() == '\0') ? param.Remove(param.Length - 1) : param);
            }

            public static byte[] uLong_to_4bytes(ulong lvspom)
            {
                short MSB, LSB;
                byte[] result = new byte[4];
                MSB = (short)(0x0000ffFF & (lvspom >> 16));
                LSB = (short)lvspom;
                result[0] = (byte)(0x00ff & (MSB >> 8));
                result[1] = (byte)(MSB);
                result[2] = (byte)(0x00ff & (LSB >> 8));
                result[3] = (byte)(LSB);
                return result;
            }
            public static byte[] uInt_to_2bytes(ulong ivspom)
            {
                byte[] result = new byte[2];
                result[0] = (byte)(0x00ff & (ivspom >> 8)); ;
                result[1] = (byte)ivspom;
                return result;
            }
        }
    }
}
