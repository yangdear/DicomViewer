using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;

namespace DicomViewer
{
    unsafe class Class1
    {
        public byte[] convertTo8Bit(byte* pData, long nNumPixels, bool bIsSigned, short nHighBit,
                                  float fRescaleSlope, float fRescaleIntercept,
                                  float fWindowCenter, float fWindowWidth)
        {
            //;byte [] pixData
            //pData = (char *)&pixData[0];
            //char* pNewData;//= 0;
            byte[] pNewData;
            long nCount;
            short* pp;
            //short[] pp;


            // 1. Clip the high bits.
            if (nHighBit < 15)
            {
                short nMask;
                short nSignBit;

                pp = (short*)pData;
                nCount = nNumPixels;

                if (bIsSigned == false) // Unsigned integer
                {
                    nMask = (short)(0xffff << (nHighBit + 1));

                    while (nCount-- > 0)
                        //pp[i++] =  ()(~nMask);
                        *(pp++) &= (short)~nMask;
                }
                else
                {
                    // 1's complement representation

                    nSignBit = (short)(1 << nHighBit);
                    nMask = (short)(0xffff << (nHighBit + 1));
                    while (nCount-- > 0)
                    {
                        if ((*pp & nSignBit) != 0)
                            *(pp++) |= nMask;
                        else
                            *(pp++) &= (short)~nMask;
                    }
                }
            }

            // 2. Rescale if needed (especially for CT)
            if ((fRescaleSlope != 1.0f) || (fRescaleIntercept != 0.0f))
            {
                float fValue;

                pp = (short*)pData;
                nCount = nNumPixels;

                while (nCount-- > 0)
                {
                    fValue = (*pp) * fRescaleSlope + fRescaleIntercept;
                    *pp++ = (short)fValue;
                }

            }

            // 3. Window-level or rescale to 8-bit
            if ((fWindowCenter != 0) || (fWindowWidth != 0))
            {
                float fSlope;
                float fShift;
                float fValue;
                pNewData = new byte[nNumPixels + 4];//实际字节数要多4个 ？ 不明白
                int i = 0;
                //pNewData = np;

                // Since we have window level info, we will only map what are
                // within the Window.

                fShift = fWindowCenter - fWindowWidth / 2.0f;
                fSlope = 255.0f / fWindowWidth;

                nCount = nNumPixels;
                pp = (short*)pData;

                while (nCount-- > 0)
                {
                    fValue = ((*pp++) - fShift) * fSlope;
                    if (fValue < 0)
                        fValue = 0;
                    else if (fValue > 255)
                        fValue = 255;

                    //(*np)++ = (char)fValue;
                    pNewData[i++] = (byte)fValue;
                }

            }
            else
            {
                // We will map the whole dynamic range.
                float fSlope;
                float fValue;
                int nMin, nMax;
                pNewData = new byte[nNumPixels + 4];
                int i = 0;
                //pNewData = np;
                // First compute the min and max.
                nCount = nNumPixels;
                pp = (short*)pData;
                nMin = nMax = *pp;
                while (nCount-- > 0)
                {
                    if (*pp < nMin)
                        nMin = *pp;

                    if (*pp > nMax)
                        nMax = *pp;

                    pp++;
                }

                // Calculate the scaling factor.
                if (nMax != nMin)
                    fSlope = 255.0f / (nMax - nMin);
                else
                    fSlope = 1.0f;

                nCount = nNumPixels;
                pp = (short*)pData;
                while (nCount-- > 0)
                {
                    fValue = ((*pp++) - nMin) * fSlope;
                    if (fValue < 0)
                        fValue = 0;
                    else if (fValue > 255)
                        fValue = 255;

                    //*(np++) = (char)fValue;
                    pNewData[i++] = (byte)fValue;
                }
            }

            return pNewData;//(char*)pNewData;
        }
     }
}
