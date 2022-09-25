using Microsoft.Win32;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Lab1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int MaxAudioFileBytes = 44100 * 60 * 10; // 10 minutes of 44100 bitrate

        public MainWindow()
        {
            InitializeComponent();

            loadedFileLabel.Content = "";
        }

        private void btnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog()
            {
                Filter = "WAV|*.wav",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;
                string fileName = System.IO.Path.GetFileName(filePath);

                loadedFileLabel.Content = fileName;

                using WaveFileReader reader = new WaveFileReader(filePath);
                if (reader.Length > MaxAudioFileBytes)
                {
                    // TODO: error handling
                    return;
                }

                byte[] bytes = new byte[reader.Length];
                reader.Read(bytes, 0, (int)reader.Length);

                int byteDepth = reader.WaveFormat.BitsPerSample / 8;
                int numberOfSamples = (int)reader.Length / byteDepth;

                double[] values = GetAudioValuesFromBytes(bytes, 0, (int)reader.Length, byteDepth);

                audioPlot.Plot.AddSignal(values);
                audioPlot.Plot.AxisZoom(5);
                audioPlot.Refresh();
            }
                
        }

        private double[] GetAudioValuesFromBytes(byte[] bytes, int offset, int length, int byteDepth)
        {
            if (bytes.Length < offset + length) 
                throw new Exception("Attempting to read more bytes than the array contains.");
            if (bytes.Length % byteDepth != 0)
                throw new Exception("Byte length must be divisible by byte depth.");
            if (byteDepth > 4)
                throw new Exception("Unsupported byte depth.");

            double[] values = new double[bytes.Length / byteDepth];
            for (int i = offset; i < length - offset; i += byteDepth)
            {
                byte[] sample = new byte[4];
                // WAV file bytes are kept in big-endian order
                // (least significant byte comes first)
                for (int j = 0; j < byteDepth; ++j)
                {
                    sample[4 - (j + 1)] = bytes[i + j];
                }

                int value = BitConverter.ToInt32(sample);
                values[i / byteDepth] = value;
            }

            return values;
        }
    }
}
