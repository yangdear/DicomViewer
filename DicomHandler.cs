using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Collections;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Drawing.Imaging;

namespace DicomViewer
{
    class DicomHandler
    {
        string fileName = "";
        Dictionary<string, string> tags = new Dictionary<string, string>();//dicom文件中的标签
        BinaryReader dicomFile;//dicom文件流

        //文件元信息
        public Bitmap gdiImg;//转换后的gdi图像
        UInt32 fileHeadLen;//文件头长度
        long fileHeadOffset;//文件数据开始位置
        UInt32 pixDatalen;//像素数据长度
        long pixDataOffset = 0;//像素数据开始位置
        bool isLitteEndian = true;//是否小字节序（小端在前 、大端在前）
        bool isExplicitVR = true;//有无VR

        //像素信息
        int colors;//颜色数 RGB为3 黑白为1
        public int windowWith = 0, windowCenter = 0 / 2;//在未设定时 窗宽窗位为0
        int rows, cols;
        public void readAndShow(TextBox textBox1)
        {
            if (fileName == string.Empty)
                return;
            dicomFile = new BinaryReader(File.OpenRead(fileName));

            //跳过128字节导言部分
            dicomFile.BaseStream.Seek(128, SeekOrigin.Begin);

            if (new string(dicomFile.ReadChars(4)) != "DICM")
            {
                MessageBox.Show("没有dicom标识头，文件格式错误");
                return;
            }

            textBox1.Clear();
            tagRead();

            IDictionaryEnumerator enor = tags.GetEnumerator();
            while (enor.MoveNext())
            {
                if (enor.Key.ToString().Length > 9)
                {
                    textBox1.Text += enor.Key.ToString() + "\r\n";
                    textBox1.Text += enor.Value.ToString().Replace('\0', ' ');
                }
                else
                    textBox1.Text += enor.Key.ToString() + enor.Value.ToString().Replace('\0', ' ') + "\r\n";
            }
            dicomFile.Close();
        }
        public  DicomHandler(string _filename)
        {
            fileName = _filename;
        }

        public void saveAs(string filename)
        {
            switch (filename.Substring(filename.LastIndexOf('.')))
            {
                case ".jpg":
                    gdiImg.Save(filename, System.Drawing.Imaging.ImageFormat.Jpeg);
                    break;
                case ".bmp":
                    gdiImg.Save(filename, System.Drawing.Imaging.ImageFormat.Bmp);
                    break;
                case ".png":
                    gdiImg.Save(filename, System.Drawing.Imaging.ImageFormat.Png);
                    break;
                default:
                    break;
            }
        }

        //获取图像 在图像数据偏移量已经确定的情况下（附带简略调窗代码） 
        public bool getImg()
        {
            if (fileName == string.Empty)
                return false;

            int dataLen, validLen, hibit;//数据长度 有效位
            int imgNum;//帧数

            rows = int.Parse(tags["0028,0010"].Substring(5));
            cols = int.Parse(tags["0028,0011"].Substring(5));

            colors = int.Parse(tags["0028,0002"].Substring(5));
            dataLen = int.Parse(tags["0028,0100"].Substring(5));//bits allocated
            validLen = int.Parse(tags["0028,0101"].Substring(5));
            bool signed = int.Parse(tags["0028,0103"].Substring(5)) == 0 ? false : true;
            hibit = int.Parse(tags["0028,0102"].Substring(5));
            float rescaleSlop = 1, rescaleinter = 0;
            if (tags.ContainsKey("0028,1052") && tags.ContainsKey("0028,1053"))
            {
                rescaleSlop = float.Parse(tags["0028,1053"].Substring(5));
                rescaleinter = float.Parse(tags["0028,1052"].Substring(5));
            }
            //读取预设窗宽窗位
            //预设窗值读取代码......
            #region//读取预设窗宽窗位
            if (windowWith == 0 && windowCenter == 0)
            {
                Regex r = new Regex(@"([0-9]+)+");
                if (tags.ContainsKey("0028,1051"))
                {
                    Match m = r.Match(tags["0028,1051"].Substring(5));

                    if (m.Success)
                        windowWith = int.Parse(m.Value);
                    else
                        windowWith = 1 << validLen;
                }
                else
                {
                    windowWith = 1 << validLen;
                }

                if (tags.ContainsKey("0028,1050"))
                {
                    Match m = r.Match(tags["0028,1050"].Substring(5));
                    if (m.Success)
                        windowCenter = int.Parse(m.Value);//窗位
                    else
                        windowCenter = windowWith / 2;
                }
                else
                {
                    windowCenter = windowWith / 2;
                }
            }

            #endregion

            gdiImg = new Bitmap(cols, rows);

            BinaryReader dicomFile = new BinaryReader(File.OpenRead(fileName));

            dicomFile.BaseStream.Seek(pixDataOffset, SeekOrigin.Begin);

            long reads = 0;

            int max = 0, min = int.MaxValue;

            for (int i = 0; i < gdiImg.Height; i++)
            {
                for (int j = 0; j < gdiImg.Width; j++)
                {
                    if (reads >= pixDatalen)
                        break;
                    byte[] pixData;

                    pixData = dicomFile.ReadBytes(dataLen / 8 * colors);
                    reads += pixData.Length;

                    Color c = Color.Empty;
                    if (colors == 1)
                    {
                        int grayGDI;

                        double gray;
                        if (validLen <= 8)
                            gray = (double)pixData[0];
                        else
                        {
                            if (isLitteEndian == false)
                                Array.Reverse(pixData, 0, 2);

                            if (signed == false)
                                gray = BitConverter.ToUInt16(pixData, 0);
                            else
                                gray = BitConverter.ToInt16(pixData, 0);

                            if ((rescaleSlop != 1.0f) || (rescaleinter != 0.0f))
                            {
                                float fValue = (float)gray * rescaleSlop + rescaleinter;
                                gray = (short)fValue;
                            }

                            if (gray > max)
                                max = (int)gray;
                            if (gray < min)
                                min = (int)gray;
                        }
                        
                        //调窗代码，就这么几句而已 
                        //1先确定窗口范围 2映射到8位灰度

                        int grayStart = (windowCenter - windowWith / 2);
                        int grayEnd = (windowCenter + windowWith / 2);

                        if (gray < grayStart)
                            grayGDI = 0;
                        else if (gray > grayEnd)
                            grayGDI = 255;
                        else
                        {
                            grayGDI = (int)((gray - grayStart) * 255 / windowWith);
                        }

                        if (grayGDI > 255)
                            grayGDI = 255;
                        else if (grayGDI < 0)
                            grayGDI = 0;
                        c = Color.FromArgb(grayGDI, grayGDI, grayGDI);
                    }
                    else if (colors == 3)
                    {
                        c = Color.FromArgb(pixData[0], pixData[1], pixData[2]);
                    }
                    gdiImg.SetPixel(j, i, c);
                }
            }
            //MessageBox.Show(string.Format("max{0} min{1}", max, min));
            dicomFile.Close();
            return true;
        }

        //获取图像 在图像数据偏移量已经确定的情况下（读取默认窗值 并调用调窗函数 convertTo8）
        public bool getImg2()
        {
            if (fileName == string.Empty)
                return false;

            int dataLen, validLen, hibit;//数据长度 有效位
            int imgNum;//帧数

            rows = int.Parse(tags["0028,0010"].Substring(5));
            cols = int.Parse(tags["0028,0011"].Substring(5));

            colors = int.Parse(tags["0028,0002"].Substring(5));
            dataLen = int.Parse(tags["0028,0100"].Substring(5));//bits allocated
            validLen = int.Parse(tags["0028,0101"].Substring(5));
            bool signed = int.Parse(tags["0028,0103"].Substring(5)) == 0 ? false : true;
            hibit = int.Parse(tags["0028,0102"].Substring(5));
            float rescaleSlop = 1, rescaleinter = 0;
            if (tags.ContainsKey("0028,1052") && tags.ContainsKey("0028,1053"))
            {
                rescaleSlop = float.Parse(tags["0028,1053"].Substring(5));
                rescaleinter = float.Parse(tags["0028,1052"].Substring(5));
            }

            #region//读取预设窗宽窗位
            if (windowWith == 0 && windowCenter == 0)
            {
                Regex r = new Regex(@"([0-9]+)+");
                if (tags.ContainsKey("0028,1051"))
                {
                    Match m = r.Match(tags["0028,1051"].Substring(5));

                    if (m.Success)
                        windowWith = int.Parse(m.Value);
                    else
                        windowWith = 1 << validLen;
                }
                else
                {
                    windowWith = 1 << validLen;
                }

                if (tags.ContainsKey("0028,1050"))
                {
                    Match m = r.Match(tags["0028,1050"].Substring(5));
                    if (m.Success)
                        windowCenter = int.Parse(m.Value);//窗位
                    else
                        windowCenter = windowWith / 2;
                }
                else
                {
                    windowCenter = windowWith / 2;
                }
            }

            #endregion

            BinaryReader dicomFile = new BinaryReader(File.OpenRead(fileName));
            dicomFile.BaseStream.Seek(pixDataOffset, SeekOrigin.Begin);

            try
            {
                /*另外一种坑爹调用方式
                byte[] pixdata = dicomFile.ReadBytes((int)pixDatalen);
                Class1 c1 = new Class1();
                unsafe
                {
                    fixed (byte* pdata = &(pixdata[0]))
                    {
                        byte[] datas = c1.convertTo8Bit(pdata, rows * cols, signed, (short)hibit, rescaleSlop, rescaleinter, windowCenter, windowWith);
                        int indx = 0;
                        gdiImg = new Bitmap(cols, rows);
                        for (int i = 0; i < rows; i++)
                        {
                            for (int j = 0; j < cols; j++)
                            {
                                if (datas[indx] > 255)
                                    datas[indx] = 255;
                                if (datas[indx] < 0)
                                    datas[indx] = 0;
                                Color c = Color.FromArgb(datas[indx], datas[indx], datas[indx]);
                                gdiImg.SetPixel(j, i, c);
                                indx++;
                            }
                        }
                    }
                }
                */
                gdiImg = convertTo8(dicomFile, colors, isLitteEndian, signed, (short)hibit, dataLen, rescaleSlop, rescaleinter,
                                                windowCenter, windowWith, cols, rows);
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format("影像数据读取时发生错误，可能是因为压缩格式造成的：{0}", ex.Message));
                gdiImg = new Bitmap(cols, rows);
            }


            dicomFile.Close();
            return true;
        }

        //成熟的调窗函数 参见class1
        public unsafe Bitmap convertTo8(BinaryReader streamdata, int colors, bool littleEdition, bool signed, short nHighBit,
               int dataLen, float rescaleSlope, float rescaleIntercept, float windowCenter, float windowWidth, int width, int height)
        {
            Bitmap bmp = new Bitmap(width, height);
            Graphics gg = Graphics.FromImage(bmp);
            gg.Clear(Color.Green);
            BitmapData bmpDatas = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
                System.Drawing.Imaging.ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            long numPixels = width * height;

            if (colors == 3)//color Img
            {
                byte* p = (byte*)bmpDatas.Scan0;
                int indx = 0;
                for (int i = 0; i < bmp.Height; i++)
                {
                    for (int j = 0; j < bmp.Width; j++)
                    {
                        p[indx + 2] = streamdata.ReadByte();
                        p[indx + 1] = streamdata.ReadByte();
                        p[indx] = streamdata.ReadByte();
                        indx += 3;
                    }
                }
            }
            else if (colors == 1)//grayscale Img
            {
                byte* p = (byte*)bmpDatas.Scan0;
                int nMin = ~(0xffff << (nHighBit + 1)), nMax = 0;
                int indx = 0;//byteData index

                for (int n = 0; n < numPixels; n++)//pixNum index
                {
                    short nMask; nMask = (short)(0xffff << (nHighBit + 1));
                    short nSignBit;

                    byte[] pixData = null;
                    short pixValue = 0;

                    pixData = streamdata.ReadBytes(dataLen / 8 * colors);
                    if (nHighBit <= 15 && nHighBit > 7)
                    {
                        if (littleEdition == false)
                            Array.Reverse(pixData, 0, 2);

                        // 1. Clip the high bits.
                        if (signed == false)// Unsigned integer
                        {
                            pixValue = (short)((~nMask) & (BitConverter.ToInt16(pixData, 0)));
                        }
                        else
                        {
                            nSignBit = (short)(1 << nHighBit);
                            if (((BitConverter.ToInt16(pixData, 0)) & nSignBit) != 0)
                                pixValue = (short)(BitConverter.ToInt16(pixData, 0) | nMask);
                            else
                                pixValue = (short)((~nMask) & (BitConverter.ToInt16(pixData, 0)));
                        }
                    }
                    else if (nHighBit <= 7)
                    {
                        if (signed == false)// Unsigned integer
                        {
                            nMask = (short)(0xffff << (nHighBit + 1));
                            pixValue = (short)((~nMask) & (pixData[0]));
                        }
                        else
                        {
                            nMask = (short)(0xffff << (nHighBit + 1));
                            nSignBit = (short)(1 << nHighBit);
                            if (((pixData[0]) & nSignBit) != 0)
                                pixValue = (short)((short)pixData[0] | nMask);
                            else
                                pixValue = (short)((~nMask) & (pixData[0]));
                        }

                    }

                    // 2. Rescale if needed (especially for CT)
                    if ((rescaleSlope != 1.0f) || (rescaleIntercept != 0.0f))
                    {
                        float fValue = pixValue * rescaleSlope + rescaleIntercept;
                        pixValue = (short)fValue;
                    }

                    // 3. Window-level or rescale to 8-bit
                    if ((windowCenter != 0) || (windowWidth != 0))
                    {
                        float fSlope;
                        float fShift;
                        float fValue;

                        fShift = windowCenter - windowWidth / 2.0f;
                        fSlope = 255.0f / windowWidth;

                        fValue = ((pixValue) - fShift) * fSlope;
                        if (fValue < 0)
                            fValue = 0;
                        else if (fValue > 255)
                            fValue = 255;


                        p[indx++] = (byte)fValue;
                        p[indx++] = (byte)fValue;
                        p[indx++] = (byte)fValue;
                    }
                    else
                    {
                        // We will map the whole dynamic range.
                        float fSlope;
                        float fValue;


                        int i = 0;
                        // First compute the min and max.
                        if (n == 0)
                            nMin = nMax = pixValue;
                        else
                        {
                            if (pixValue < nMin)
                                nMin = pixValue;

                            if (pixValue > nMask)
                                nMask = pixValue;
                        }

                        // Calculate the scaling factor.
                        if (nMax != nMin)
                            fSlope = 255.0f / (nMax - nMin);
                        else
                            fSlope = 1.0f;

                        fValue = ((pixValue) - nMin) * fSlope;
                        if (fValue < 0)
                            fValue = 0;
                        else if (fValue > 255)
                            fValue = 255;

                        p[indx++] = (byte)fValue;
                    }
                }
            }

            bmp.UnlockBits(bmpDatas);
            //bmp.Dispose();
            return bmp;
        }

        void tagRead()//不断读取所有tag 及其值 直到碰到图像数据 (7fe0 0010 )
        {
            bool enDir = false;
            int leve = 0;
            StringBuilder folderData = new StringBuilder();//该死的文件夹标签
            string folderTag = "";
            while (dicomFile.BaseStream.Position + 6 < dicomFile.BaseStream.Length)
            {
                //读取tag
                string tag = dicomFile.ReadUInt16().ToString("x4") + "," +
                dicomFile.ReadUInt16().ToString("x4");

                string VR = string.Empty;
                UInt32 Len = 0;
                //读取VR跟Len
                //对OB OW SQ 要做特殊处理 先置两个字节0 然后4字节值长度
                //------------------------------------------------------这些都是在读取VR一步被阻断的情况
                if (tag.Substring(0, 4) == "0002")//文件头 特殊情况
                {
                    VR = new string(dicomFile.ReadChars(2));

                    if (VR == "OB" || VR == "OW" || VR == "SQ" || VR == "OF" || VR == "UT" || VR == "UN")
                    {
                        dicomFile.BaseStream.Seek(2, SeekOrigin.Current);
                        Len = dicomFile.ReadUInt32();
                    }
                    else
                        Len = dicomFile.ReadUInt16();
                }
                else if (tag == "fffe,e000" || tag == "fffe,e00d" || tag == "fffe,e0dd")//文件夹标签
                {
                    VR = "**";
                    Len = dicomFile.ReadUInt32();
                }
                else if (isExplicitVR == true)//有无VR的情况
                {
                    VR = new string(dicomFile.ReadChars(2));

                    if (VR == "OB" || VR == "OW" || VR == "SQ" || VR == "OF" || VR == "UT" || VR == "UN")
                    {
                        dicomFile.BaseStream.Seek(2, SeekOrigin.Current);
                        Len = dicomFile.ReadUInt32();
                    }
                    else
                        Len = dicomFile.ReadUInt16();
                }
                else if (isExplicitVR == false)
                {
                    VR = getVR(tag);//无显示VR时根据tag一个一个去找 真tm烦啊。
                    Len = dicomFile.ReadUInt32();
                }
                //判断是否应该读取VF 以何种方式读取VF
                //-------------------------------------------------------这些都是在读取VF一步被阻断的情况
                byte[] VF = { 0x00 };

                if (tag == "7fe0,0010")//图像数据开始了
                {
                    pixDatalen = Len;
                    pixDataOffset = dicomFile.BaseStream.Position;
                    dicomFile.BaseStream.Seek(Len, SeekOrigin.Current);
                    VR = "UL";
                    VF = BitConverter.GetBytes(Len);
                }
                else if ((VR == "SQ" && Len == UInt32.MaxValue) || (tag == "fffe,e000" && Len == UInt32.MaxValue))//靠 遇到文件夹开始标签了
                {
                    if (enDir == false)
                    {
                        enDir = true;
                        folderData.Remove(0, folderData.Length);
                        folderTag = tag;
                    }
                    else
                    {
                        leve++;//VF不赋值
                    }
                }
                else if ((tag == "fffe,e00d" && Len == UInt32.MinValue) || (tag == "fffe,e0dd" && Len == UInt32.MinValue))//文件夹结束标签
                {
                    if (enDir == true)
                    {
                        enDir = false;
                    }
                    else
                    {
                        leve--;
                    }
                }
                else
                    VF = dicomFile.ReadBytes((int)Len);

                string VFStr;

                VFStr = getVF(VR, VF);

                //----------------------------------------------------------------针对特殊的tag的值的处理
                //特别针对文件头信息处理
                if (tag == "0002,0000")
                {
                    fileHeadLen = Len;
                    fileHeadOffset = dicomFile.BaseStream.Position;
                }
                else if (tag == "0002,0010")//传输语法 关系到后面的数据读取
                {
                    switch (VFStr)
                    {
                        case "1.2.840.10008.1.2.1\0"://显示little
                            isLitteEndian = true;
                            isExplicitVR = true;
                            break;
                        case "1.2.840.10008.1.2.2\0"://显示big
                            isLitteEndian = false;
                            isExplicitVR = true;
                            break;
                        case "1.2.840.10008.1.2\0"://隐式little
                            isLitteEndian = true;
                            isExplicitVR = false;
                            break;
                        default:
                            break;
                    }
                }
                for (int i = 1; i <= leve; i++)
                    tag = "--" + tag;
                //------------------------------------数据搜集代码
                if ((VR == "SQ" && Len == UInt32.MaxValue) || (tag == "fffe,e000" && Len == UInt32.MaxValue) || leve > 0)//文件夹标签代码
                {
                    folderData.AppendLine(tag + "(" + VR + ")：" + VFStr);
                }
                else if (((tag == "fffe,e00d" && Len == UInt32.MinValue) || (tag == "fffe,e0dd" && Len == UInt32.MinValue)) && leve == 0)//文件夹结束标签
                {
                    folderData.AppendLine(tag + "(" + VR + ")：" + VFStr);
                    tags.Add(folderTag + "SQ", folderData.ToString());
                }
                else
                    tags.Add(tag, "(" + VR + "):" + VFStr);
            }
        }

        string getVR(string tag)
        {
            switch (tag)
            {
                case "0002,0000"://文件元信息长度
                    return "UL";
                    break;
                case "0002,0010"://传输语法
                    return "UI";
                    break;
                case "0002,0013"://文件生成程序的标题
                    return "SH";
                    break;
                case "0008,0005"://文本编码
                    return "CS";
                    break;
                case "0008,0008":
                    return "CS";
                    break;
                case "0008,1032"://成像时间
                    return "SQ";
                    break;
                case "0008,1111":
                    return "SQ";
                    break;
                case "0008,0020"://检查日期
                    return "DA";
                    break;
                case "0008,0060"://成像仪器
                    return "CS";
                    break;
                case "0008,0070"://成像仪厂商
                    return "LO";
                    break;
                case "0008,0080":
                    return "LO";
                    break;
                case "0010,0010"://病人姓名
                    return "PN";
                    break;
                case "0010,0020"://病人id
                    return "LO";
                    break;
                case "0010,0030"://病人生日
                    return "DA";
                    break;
                case "0018,0060"://电压
                    return "DS";
                    break;
                case "0018,1030"://协议名
                    return "LO";
                    break;
                case "0018,1151":
                    return "IS";
                    break;
                case "0020,0010"://检查ID
                    return "SH";
                    break;
                case "0020,0011"://序列
                    return "IS";
                    break;
                case "0020,0012"://成像编号
                    return "IS";
                    break;
                case "0020,0013"://影像编号
                    return "IS";
                    break;
                case "0028,0002"://像素采样1为灰度3为彩色
                    return "US";
                    break;
                case "0028,0004"://图像模式MONOCHROME2为灰度
                    return "CS";
                    break;
                case "0028,0006"://颜色值排列顺序 可能骨头重建那个的显示就是这个问题
                    return "US";
                    break;
                case "0028,0008"://图像的帧数
                    return "IS";
                    break;

                case "0028,0010"://row高
                    return "US";
                    break;
                case "0028,0011"://col宽
                    return "US";
                    break;
                case "0028,0100"://单个采样数据长度
                    return "US";
                    break;
                case "0028,0101"://实际长度
                    return "US";
                    break;
                case "0028,0102"://采样最大值
                    return "US";
                    break;
                case "0028,0103"://像素数据类型
                    return "US";
                    break;

                case "0028,1050"://窗位
                    return "DS";
                    break;
                case "0028,1051"://窗宽
                    return "DS";
                    break;
                case "0028,1052":
                    return "DS";
                    break;
                case "0028,1053":
                    return "DS";
                    break;
                case "0040,0008"://文件夹标签
                    return "SQ";
                    break;
                case "0040,0260"://文件夹标签
                    return "SQ";
                    break;
                case "0040,0275"://文件夹标签
                    return "SQ";
                    break;
                case "7fe0,0010"://像素数据开始处
                    return "OW";
                    break;
                default:
                    return "UN";
                    break;
            }
        }

        string getVF(string VR, byte[] VF)
        {
            if (VF.Length == 0)
                return "";
            string VFStr = string.Empty;
            if (isLitteEndian == false)//如果是big字节序 先把数据翻转一下
                Array.Reverse(VF);
            switch (VR)
            {
                case "SS":
                    VFStr = BitConverter.ToInt16(VF, 0).ToString();
                    break;
                case "US":
                    VFStr = BitConverter.ToUInt16(VF, 0).ToString();

                    break;
                case "SL":
                    VFStr = BitConverter.ToInt32(VF, 0).ToString();

                    break;
                case "UL":
                    VFStr = BitConverter.ToUInt32(VF, 0).ToString();

                    break;
                case "AT":
                    VFStr = BitConverter.ToUInt16(VF, 0).ToString();

                    break;
                case "FL":
                    VFStr = BitConverter.ToSingle(VF, 0).ToString();

                    break;
                case "FD":
                    VFStr = BitConverter.ToDouble(VF, 0).ToString();

                    break;
                case "OB":
                    VFStr = BitConverter.ToString(VF, 0);
                    break;
                case "OW":
                    VFStr = BitConverter.ToString(VF, 0);
                    break;
                case "SQ":
                    VFStr = BitConverter.ToString(VF, 0);
                    break;
                case "OF":
                    VFStr = BitConverter.ToString(VF, 0);
                    break;
                case "UT":
                    VFStr = BitConverter.ToString(VF, 0);
                    break;
                case "UN":
                    VFStr = Encoding.Default.GetString(VF);
                    break;
                default:
                    VFStr = Encoding.Default.GetString(VF);
                    break;
            }
            return VFStr;
        }

    }
}
