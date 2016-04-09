using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Collections;
//using NewScp.DataModel;

namespace DicomViewer
{
    public enum ByteOrder
    {
        LittleEndian = 2,
        BigEndian = 4,
    }
    sealed public class  Helpers
    {
        public static string ApplicationRoot()
        {
            return Application.StartupPath;
        }

        public static string Trim(string str)
        {
            return str.Trim('\n', '\r', '\0',' ');
        }

     
        public static uint String2Int(string str, ByteOrder sourceOrder, ByteOrder destOrder)
        {

            byte[] buf = Encoding.GetEncoding("ISO-8859-1").GetBytes(str);
            byte[] buf2 = new byte[4];
            uint ret = 0;

            if (sourceOrder == ByteOrder.BigEndian)
            {
                switch (buf.Length)
                {
                    case 1:
                        buf2[0] = buf[0];
                        break;
                    case 2:
                        buf2[0] = buf[0];
                         buf2[1] = buf[1];
                         break;
                    case 3:
                        buf2[0] = buf[0];
                        buf2[1] = buf[1];
                        buf[2] = buf[2];
                        break;
                    case 4:
                        buf2 = buf;
                        break;
                }
            }
            else
            {
                switch (buf.Length)
                {
                    case 1:
                        buf2[3] = buf[0];
                        break;
                    case 2:
                        buf2[3] = buf[0];
                        buf2[2] = buf[1];
                        break;
                    case 3:
                        buf2[3] = buf[0];
                        buf2[2] = buf[1];
                        buf2[1] = buf[2];
                        break;
                    case 4:
                        buf2[3] = buf[0];
                        buf2[2] = buf[1];
                        buf2[1] = buf[2];
                        buf2[0] = buf[3];
                        break;
                }
            }

            if (destOrder == ByteOrder.BigEndian)
            {
                ret = (uint)((buf2[0] << 24 | buf2[1] << 16 | buf2[2] << 8 | buf2[3]) & 0xFFFFFFFF);
            }
            else
            {
                ret = (uint)((buf2[3] << 24 | buf2[2] << 16 | buf2[1] << 8 | buf2[0]) & 0xFFFFFFFF);
            }

            return ret;
        }

        public static ushort String2Ushort(String str, ByteOrder sourceOrder, ByteOrder destOrder)
        {
            uint ret = String2Int(str,sourceOrder,destOrder);
            ushort rslt = 0;
            
            if (destOrder == ByteOrder.LittleEndian)
            {
                rslt = (ushort)((ret * 0xFFFF0000) >> 16);
            }
            else
            {
                rslt = (ushort)(ret & 0xFFFF);
            }

            return rslt;
        }

        public static string Int2String(uint value, ByteOrder sourceOrder, ByteOrder destOrder)
        {
            byte[] buf = new byte[4];

            if ( sourceOrder == ByteOrder.BigEndian && sourceOrder == destOrder)
            {
                buf[0] = (byte)((value & 0xFF000000) >> 24);
                buf[1] = (byte)((value & 0xFF0000) >> 16);
                buf[2] = (byte)((value & 0xFF00) >> 8);
                buf[3] = (byte)(value & 0xFF);
            }
            else
            {
                buf[3] = (byte)((value & 0xFF000000) >> 24);
                buf[2] = (byte)((value & 0xFF0000) >> 16);
                buf[1] = (byte)((value & 0xFF00) >> 8);
                buf[0] = (byte)(value & 0xFF);
            }

            return Encoding.GetEncoding("ISO-8859-1").GetString(buf);
        }
        
        public static string Ushort2String(ushort value, ByteOrder sourceOrder, ByteOrder destOrder)
        {
            string ret = Int2String((uint)value, sourceOrder,destOrder);
            string rslt = "";
            if (destOrder == ByteOrder.BigEndian)
            {
                rslt = ret.Substring(2);
            }
            else
            {
                rslt = ret.Substring(0, 2);
            }
            return rslt;
        }

        public static string BaseUid(string prefix)
        {
            
            if (prefix == "")
            {
                prefix = "999.1.";
            }
            //每个点的开头不能是零
            prefix += string.Format("{0:0000}{1:00}{2:00}.9{6:0000}", DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second,DateTime.Now.Millisecond, DateTime.Now.TimeOfDay.TotalMilliseconds);
            
            return prefix;
        }

        public static Bitmap TransformRGB(byte[] pixelData, int imgWidth, int imgHeight,ushort planar, ref int palette)
        {
            //string df = Helpers.ApplicationRoot() + "\\" + "ReciverRgb";
            //pixelData = File.ReadAllBytes(df);

            //File.WriteAllBytes(Helpers.ApplicationRoot() + "\\" + "ReciverRgb=" + pixelData.GetHashCode(), pixelData);
            
            Bitmap bmp = new Bitmap(imgWidth, imgHeight, PixelFormat.Format24bppRgb);

            BitmapData bmd = bmp.LockBits(new Rectangle(0, 0, imgWidth, imgHeight),
            System.Drawing.Imaging.ImageLockMode.ReadOnly, bmp.PixelFormat);

            int w, h;
            h = bmd.Height;
            w = bmd.Width;

            Hashtable colors = new Hashtable();

            unsafe
            {
                int pixelSize = 3;
                int i, j, j1, i1;
                byte r, g, b;
                int rgb;

                if (planar == 0)
                {
                    //按照RGB的顺序
                    for (i = 0; i < bmd.Height; ++i)
                    {
                        byte* row = (byte*)bmd.Scan0 + (i * bmd.Stride);
                        i1 = i * bmd.Width * pixelSize;
                        
                        for (j = 0; j < bmd.Width; ++j)
                        {
                            //在bmp中rgb的位置是倒置的
                            j1 = j * pixelSize;
                            row[j1 + 2] = r = pixelData[i1 + j1];                // Red
                            row[j1 + 1] = g = pixelData[i1 + j1 + 1];            // Green
                            row[j1] = b = pixelData[i1 + j1 + 2];                // Blue
                            rgb = r << 16 | g << 8 | b;
                            if ( !colors.ContainsKey(rgb) )
                            {
                                colors.Add(rgb,rgb);
                            }
                        }

                    }
                }
                else if (planar == 1)
                {
                    //按照RRRGGGBBB的顺序
                    //rowoffset = bmd.Height * bmd.Width;
                    int len = bmd.Width * bmd.Height;
                    int offset = 0;

                    for (i = 0; i < bmd.Height; ++i)
                    {
                        byte* row = (byte*)bmd.Scan0 + (i * bmd.Stride);
                        
                        i1 = i * bmd.Width * pixelSize;

                        for (j = 0; j < bmd.Width; ++j)
                        {
                            //在bmp中rgb的位置是倒置的
                            j1 = j * pixelSize;
                            offset = i * bmd.Width + j;
                            row[j1 + 2] = r = pixelData[offset];                // Red
                            row[j1 + 1] = g = pixelData[len + offset];          // Green
                            row[j1] = b = pixelData[2 * len + offset];          // Blue

                            rgb = r << 16 | g << 8 | b;
                            if (!colors.ContainsKey(rgb))
                            {
                                colors.Add(rgb,rgb);
                            }
                        }
                    }
                }
            }

            IntPtr ptr = bmd.Scan0;

            bmp.UnlockBits(bmd);
            //bmp.Save(Helpers.ApplicationRoot() + "\\bmpRGB-"+ bmp.GetHashCode() +".bmp", ImageFormat.Bmp);

            palette = colors.Count;
            
            colors.Clear();

            return bmp;
        }
        public static Bitmap TransformRGBTest(string df, int imgWidth, int imgHeight, ushort planar)
        {
            //string df = Helpers.ApplicationRoot() + "\\" + "ReciverRgb";
            byte[] pixelData = File.ReadAllBytes(df);
            int colors = 0;
            return Helpers.TransformRGB(pixelData,imgWidth,imgHeight,planar,ref colors);
        }

        public static Bitmap CreateImage8(byte[] pixelData, int winLevel, int winWidth, int imgWidth, int imgHeight)
        {

            int winMin, winMax;

            winMin = (int)(winWidth / 2) - winLevel;
            winMax = winWidth + winMin;

            if (winMin >= winMax) winMin = winMax - 1;
            if (winMax <= winMin) winMax = winMin + 1;


            byte[] lut = Helpers.ComputeLookUpTable8(winMin,winMax);

            Bitmap bmp = new Bitmap(imgWidth, imgHeight, PixelFormat.Format24bppRgb);

            BitmapData bmd = bmp.LockBits(new Rectangle(0, 0, imgWidth, imgHeight),
            System.Drawing.Imaging.ImageLockMode.ReadOnly, bmp.PixelFormat);

            unsafe
            {
                int pixelSize = 3;
                int i, j, j1, i1,pix;
                byte b;
                
                for (i = 0; i < bmd.Height; ++i)
                {
                    byte* row = (byte*)bmd.Scan0 + (i * bmd.Stride);
                    i1 = i * bmd.Width;

                    for (j = 0; j < bmd.Width; ++j)
                    {

                        pix = pixelData[i * bmd.Width + j];
                        //b = pix;
                        b = lut[pix];
                        //b = (byte)(pix * factor);
                        //b = (byte)((pix - winMin) * factor);
                        j1 = j * pixelSize;
                        row[j1] = b;            // Red
                        row[j1 + 1] = b;        // Green
                        row[j1 + 2] = b;        // Blue
                    }
                }
            }

            IntPtr ptr = bmd.Scan0;
            bmp.UnlockBits(bmd);

            return bmp;
        }
        public static Bitmap CreateImage8Test(string file, int winLevel, int winWidth, int imgWidth, int imgHeight)
        {
            int winMin, winMax;

            winMin = (int)(winWidth / 2) - winLevel;
            winMax = winWidth + winMin;

            if (winMin >= winMax) winMin = winMax - 1;
            if (winMax <= winMin) winMax = winMin + 1;

            //string df = Helpers.ApplicationRoot() + "\\" + "Recive8";
            byte[] pixelData = File.ReadAllBytes(file);

            //File.WriteAllBytes(Helpers.ApplicationRoot() + "\\" + "Recive8=" + pixelData.GetHashCode(),pixelData);

            return Helpers.CreateImage8( pixelData,winLevel,winWidth,imgWidth,imgHeight);
        }

        public static Bitmap CreateImage16(byte[] pixelData, int winLevel, int winWidth, int imgWidth, int imgHeight)
        {
            int winMin, winMax;

            winMin = (int)(winWidth / 2) - winLevel;
            winMax = winWidth + winMin;

            if (winMin >= winMax) winMin = winMax - 1;
            if (winMax <= winMin) winMax = winMin + 1;


            int numPixels = imgWidth * imgHeight;

            byte[] lut = Helpers.ComputeLookUpTable16(winMin, winMax);

            int i,i1;

            Bitmap bmp = new Bitmap(imgWidth, imgHeight, PixelFormat.Format24bppRgb);

            BitmapData bmd = bmp.LockBits(new Rectangle(0, 0, imgWidth, imgHeight),
            System.Drawing.Imaging.ImageLockMode.ReadOnly, bmp.PixelFormat);

            unsafe
            {
                int pixelSize = 3;
                int j, j1,pix;
                byte b;

                for (i = 0; i < bmd.Height; ++i)
                {
                    byte* row = (byte*)bmd.Scan0 + (i * bmd.Stride);
                    i1 = i * bmd.Width;

                    for (j = 0; j < bmd.Width; ++j)
                    {
                        pix = pixelData[2 * i * bmd.Width + 2 * j + 1] << 8;
                        pix |= pixelData[2 * i * bmd.Width + 2 * j];

                        //b = pix;
                        b = lut[pix];
                        //b = (byte)(pix * factor);
                        //b = (byte)((pix - winMin) * factor);

                        j1 = j * pixelSize;
                        row[j1] = b;            // Red
                        row[j1 + 1] = b;        // Green
                        row[j1 + 2] = b;        // Blue
                    }
                }
            }

            IntPtr ptr = bmd.Scan0;
            bmp.UnlockBits(bmd);

            return bmp; 
        }
        public static Bitmap CreateImage16Test(string file, int winLevel, int winWidth, int imgWidth, int imgHeight)
        {
            int winMin, winMax;

            winMin = (int)(winWidth / 2) - winLevel;
            winMax = winWidth + winMin;

            if (winMin >= winMax) winMin = winMax - 1;
            if (winMax <= winMin) winMax = winMin + 1;


            //string df = Helpers.ApplicationRoot() + "\\" + "Recive16";
            byte[] pixelData = File.ReadAllBytes(file);

            return Helpers.CreateImage16(pixelData,winLevel,winWidth, imgWidth, imgHeight);
        }

        public static Bitmap CreateImage12(byte[] pixelData, int winLevel, int winWidth, int imgWidth, int imgHeight)
        {
            int winMin, winMax;

            winMin = (int)(winWidth / 2) - winLevel;
            winMax = winWidth + winMin;

            if (winMin >= winMax) winMin = winMax - 1;
            if (winMax <= winMin) winMax = winMin + 1;


            int numPixels = imgWidth * imgHeight;

            byte[] lut = Helpers.ComputeLookUpTable12(winMin, winMax);

            int i, i1;

            Bitmap bmp = new Bitmap(imgWidth, imgHeight, PixelFormat.Format24bppRgb);

            BitmapData bmd = bmp.LockBits(new Rectangle(0, 0, imgWidth, imgHeight),
            System.Drawing.Imaging.ImageLockMode.ReadOnly, bmp.PixelFormat);

            unsafe
            {
                int pixelSize = 3;
                int j, j1, pix;
                byte b;

                for (i = 0; i < bmd.Height; ++i)
                {
                    byte* row = (byte*)bmd.Scan0 + (i * bmd.Stride);
                    i1 = i * bmd.Width;

                    for (j = 0; j < bmd.Width; ++j)
                    {
                        pix = pixelData[2 * i * bmd.Width + 2 * j + 1] << 8;
                        pix |= pixelData[2 * i * bmd.Width + 2 * j];

                        //b = pix;
                        b = lut[pix];
                        //b = (byte)(pix * factor);
                        //b = (byte)((pix - winMin) * factor);

                        j1 = j * pixelSize;
                        row[j1] = b;            // Red
                        row[j1 + 1] = b;        // Green
                        row[j1 + 2] = b;        // Blue
                    }
                }
            }

            IntPtr ptr = bmd.Scan0;
            bmp.UnlockBits(bmd);

            return bmp;
        }
        public static Bitmap CreateImage12Test(string file, int winLevel, int winWidth, int imgWidth, int imgHeight)
        {
            int winMin, winMax;

            winMin = (int)(winWidth / 2) - winLevel;
            winMax = winWidth + winMin;

            if (winMin >= winMax) winMin = winMax - 1;
            if (winMax <= winMin) winMax = winMin + 1;


            //string df = Helpers.ApplicationRoot() + "\\" + "Recive16";
            byte[] pixelData = File.ReadAllBytes(file);

            return Helpers.CreateImage12(pixelData, winLevel, winWidth, imgWidth, imgHeight);
        }
        
        public static byte[] ComputeLookUpTable8(int winMin, int winMax)
        {
            byte[] lut8 = new byte[256];

            winMax = 256;

            int range = winMax - winMin;
            
            if (range < 1) range = 1;
            double factor = 255.0 / range;

            for (int i = 0; i < 256; ++i)
            {
                if (i <= winMin)
                    lut8[i] = 0;
                else if (i >= winMax)
                    lut8[i] = 255;
                else
                {
                    lut8[i] = (byte)((i - winMin) * factor);
                }
            }

            return lut8;
        }
        public static byte[] ComputeLookUpTable16(int winMin, int winMax)
        {
            byte[] lut16 = new byte[65536];
            winMax = 65536;
            int range = winMax - winMin;
            if (range < 1) range = 1;
            double factor = 255.0 / range;
            int i;

            for (i = 0; i < 65536; ++i)
            {
                if (i <= winMin)
                    lut16[i] = 0;
                else if (i >= winMax)
                    lut16[i] = 255;
                else
                {
                    lut16[i] = (byte)((i - winMin) * factor);
                }
            }
            return lut16;
        }
        public static byte[] ComputeLookUpTable12(int winMin, int winMax)
        {
            byte[] lut12 = new byte[2<<11];
            winMax = 2 << 11;
            int range = winMax - winMin;
            if (range < 1) range = 1;
            double factor = 255.0 / range;
            int i;

            for (i = 0; i < winMax; ++i)
            {
                if (i <= winMin)
                    lut12[i] = 0;
                else if (i >= winMax)
                    lut12[i] = 255;
                else
                {
                    lut12[i] = (byte)((i - winMin) * factor);
                }
            }
            return lut12;
        }

    }
}
