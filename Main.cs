using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Collections;
using System.IO;
using System.Threading;

namespace DicomViewer
{
    public partial class Main : Form
    {
        bool showTag =true;//tag/ 影像切换
        bool zoomYes = true;//原图/适合窗口
        DicomHandler handler;
        public Main()
        {
            InitializeComponent();
            UIswitch();
        }

        
        void UIswitch()
        {
            if (handler != null)
            {
                windowWidthTxt.Text = handler.windowWith.ToString();
                windowCenterTxt.Text = handler.windowCenter.ToString();
            }
            if (showTag)
            {
                textBox1.Visible = true;
                pictureBox1.Visible = false;
                toolStripButton2.Text = "[切换到影像　]";
            }
            else
            {
                textBox1.Visible = false;
                pictureBox1.Visible = true;

                toolStripButton2.Text = "[切换到标签　]";
            }


            if (zoomYes)
            {
                pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
                toolStripButton3.Text = "[切换到原始尺寸　　]";
            }
            else
            {
                pictureBox1.SizeMode = PictureBoxSizeMode.CenterImage;
                toolStripButton3.Text = "[切换到适合窗口尺寸]";
            }

            if (backgroundWorker1.IsBusy)
            {
                Bitmap loadImg = new Bitmap(200, 100);
                Graphics g = Graphics.FromImage(loadImg);
                g.DrawString("载入中，请稍后...", new Font(new FontFamily("Arial"), 15.0f), Brushes.Black, new PointF(0, 0));
                pictureBox1.Image = loadImg;
                pictureBox1.SizeMode = PictureBoxSizeMode.CenterImage;
            }
            else
            {
                if (handler!=null&& handler.gdiImg!=null)
                    pictureBox1.Image = handler.gdiImg;

                if (zoomYes)
                {
                    pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
                    toolStripButton3.Text = "[切换到原始尺寸　　]";
                }
                else
                {
                    pictureBox1.SizeMode = PictureBoxSizeMode.CenterImage;
                    toolStripButton3.Text = "[切换到适合窗口尺寸]";
                }
            }
        }

        

        private void toolStripLabel1_Click(object sender, EventArgs e)
        {
            if (backgroundWorker1.IsBusy)
            {
                MessageBox.Show("影像载入中，请稍后...");
                return;
            }


            if (openFileDialog1.ShowDialog() != DialogResult.OK)
                return;

            string fileName = openFileDialog1.FileName;
            
            handler = new DicomHandler(fileName);

            handler.readAndShow(textBox1);

            this.Text = "DicomViewer-" + openFileDialog1.FileName;

            backgroundWorker1.RunWorkerAsync();

            UIswitch();

            //if (handler.getImg())
            //    pictureBox1.Image = handler.gdiImg;
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            if (backgroundWorker1.IsBusy)
            {
                MessageBox.Show("影像载入中，请稍后...");
                return;
            }

            if (saveFileDialog1.ShowDialog() != DialogResult.OK || handler == null || handler.gdiImg == null)
                return;

            handler.saveAs(saveFileDialog1.FileName);
        }

        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            showTag = !showTag;
            UIswitch();
        }

        private void toolStripButton3_Click(object sender, EventArgs e)
        {
            zoomYes = !zoomYes;
            UIswitch();
        }

        private void toolStripButton4_Click(object sender, EventArgs e)
        {
            if (backgroundWorker1.IsBusy||handler==null)
            {
                MessageBox.Show("影像载入中，请稍后...");
                return;
            }

            int.TryParse(windowWidthTxt.Text, out handler.windowWith);
            int.TryParse(windowCenterTxt.Text, out handler.windowCenter);

            backgroundWorker1.RunWorkerAsync();

            UIswitch();
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
           
                if (handler.getImg())
                    pictureBox1.Image = handler.gdiImg;
            
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            UIswitch();
        }

        
    }
}
