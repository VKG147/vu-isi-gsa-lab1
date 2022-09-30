using Microsoft.Win32;
using NAudio.Wave;
using ScottPlot.Plottable;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace Lab1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        enum TimeUnit
        {
            Milliseconds = 0,
            Seconds = 1,
            Minutes = 2
        }

        private const int MaxAudioFileBytes = 44100 * 60 * 60; // 1 hour of 44100 bitrate

        private TimeUnit _currentTimeUnit = TimeUnit.Minutes;
        private VLine? _timeMarkerLine = null;
        private SignalPlotXY? _channel1Plot = null;
        private SignalPlotXY? _channel2Plot = null;

        private readonly System.Drawing.Color TimerMarkerLineColor = System.Drawing.Color.DarkRed;
        private readonly System.Drawing.Color Channe1PlotColor = System.Drawing.Color.Blue;
        private readonly System.Drawing.Color Channe2PlotColor = System.Drawing.Color.Red;

        public MainWindow()
        {
            InitializeComponent();

            audioPlot.Plot.XLabel("X (minutes)");
            audioPlot.Plot.YLabel("Y");

            loadedFileLabel.Content = "";
        }

        private void btnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog()
            {
                Filter = "WAV|*.wav",
                Multiselect = false,
                InitialDirectory = Environment.CurrentDirectory
            };

            if (openFileDialog.ShowDialog() == true)
            {
                audioPlot.Reset();
                loadTimeLabel.Content = "";
                channel1Checkbox.IsEnabled = false;
                channel2Checkbox.IsEnabled = false;
                channel1Checkbox.IsChecked = false;
                channel2Checkbox.IsChecked = false;

                string filePath = openFileDialog.FileName;
                string fileName = System.IO.Path.GetFileName(filePath);

                loadedFileLabel.Content = fileName;

                using WaveFileReader reader = new WaveFileReader(filePath);
                if (reader.Length > MaxAudioFileBytes)
                {
                    // TODO: error handling
                    return;
                }
                if (reader.WaveFormat.Channels > 2)
                {
                    // TODO: error handling
                    return;
                }

                List<double> channel1Ys = new();
                List<double> channel2Ys = new();
                int maxAudioPlotYValue = (int)Math.Round(Math.Pow(2.0, reader.WaveFormat.BitsPerSample - 1));

                /* LOAD BEGIN */
                Stopwatch loadStopwatch = new Stopwatch();
                loadStopwatch.Start();

                while (reader.Position < reader.Length)
                {
                    float[] sampleFrame = reader.ReadNextSampleFrame();
                    channel1Ys.Add(sampleFrame[0] * maxAudioPlotYValue);

                    if (sampleFrame.Length > 1)
                    {
                        channel2Ys.Add(sampleFrame[1] * maxAudioPlotYValue);
                    }
                }

                double totalTimeMultiplier = 0;
                if (reader.TotalTime.TotalSeconds < 1)
                {
                    totalTimeMultiplier = reader.TotalTime.TotalMilliseconds;
                    _currentTimeUnit = TimeUnit.Milliseconds;
                    audioPlot.Plot.XLabel("X (milliseconds)");
                    audioPlot.Plot.YLabel("Y");
                } 
                else if (reader.TotalTime.TotalSeconds < 60)
                {
                    totalTimeMultiplier = reader.TotalTime.TotalSeconds;
                    _currentTimeUnit = TimeUnit.Seconds;
                    audioPlot.Plot.XLabel("X (seconds)");
                    audioPlot.Plot.YLabel("Y");
                }
                else
                {
                    totalTimeMultiplier = reader.TotalTime.TotalMinutes;
                    _currentTimeUnit = TimeUnit.Minutes;
                    audioPlot.Plot.XLabel("X (minutes)");
                    audioPlot.Plot.YLabel("Y");
                }

                double[] channel1Xs = new double[channel1Ys.Count];
                for (int i = 0; i < channel1Xs.Length; i++)
                {
                    channel1Xs[i] = (double)i / channel1Xs.Length * totalTimeMultiplier;
                }
                _channel1Plot = audioPlot.Plot.AddSignalXY(channel1Xs, channel1Ys.ToArray(), color: Channe1PlotColor, label: "channel1");
                channel1Checkbox.IsEnabled = true;
                channel1Checkbox.IsChecked = true;

                if (channel2Ys.Count > 0)
                {
                    double[] channel2Xs = new double[channel2Ys.Count];
                    for (int i = 0; i < channel2Xs.Length; i++)
                    {
                        channel2Xs[i] = (double)i / channel2Xs.Length * totalTimeMultiplier;
                    }
                    _channel2Plot = audioPlot.Plot.AddSignalXY(channel2Xs, channel2Ys.ToArray(), color: Channe2PlotColor, label: "channel2");
                    channel2Checkbox.IsEnabled = true;
                    channel2Checkbox.IsChecked = true;
                }

                audioPlot.Refresh();

                /* LOAD END*/
                loadStopwatch.Stop();
                loadTimeLabel.Content = $"Loaded in {loadStopwatch.Elapsed.TotalSeconds} seconds";
            }
        }

        private void txtboxMarkerTime_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtboxMarkerTime.Text))
            {
                RemoveTimeMarkerLine();
                return;
            }

            string markerTimeText = txtboxMarkerTime.Text.Replace('.', ',');
            if (double.TryParse(markerTimeText, out double markerTimeValue))
            {
                UpdateTimeMarkerLine(markerTimeValue);
            }
        }

        private void comboTimeUnit_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtboxMarkerTime.Text))
                return;

            string markerTimeText = txtboxMarkerTime.Text.Replace('.', ',');
            if (double.TryParse(markerTimeText, out double markerTimeValue))
            {
                string previousUnit = (string)(e.RemovedItems[0] as ComboBoxItem ?? throw new Exception()).Content;
                string currentUnit = (string)(e.AddedItems[0] as ComboBoxItem ?? throw new Exception()).Content;

                if (previousUnit != currentUnit)
                {
                    TimeUnit unitFrom = Enum.Parse<TimeUnit>(previousUnit, true);
                    TimeUnit unitTo = Enum.Parse<TimeUnit>(currentUnit, true);
                    markerTimeValue = ConvertTimeUnit(markerTimeValue, unitFrom, unitTo);

                    txtboxMarkerTime.Text = markerTimeValue.ToString();
                }

                UpdateTimeMarkerLine(markerTimeValue);
            }
        }

        private void UpdateTimeMarkerLine(double markedTimeValue)
        {
            RemoveTimeMarkerLine();

            string userSelectedTimeUnit = (string)((ComboBoxItem)comboTimeUnit.SelectedValue).Content;

            TimeUnit selectedUnit = Enum.Parse<TimeUnit>(userSelectedTimeUnit, true);
            double adjustedTime = ConvertTimeUnit(markedTimeValue, selectedUnit, _currentTimeUnit);

            _timeMarkerLine = audioPlot.Plot.AddVerticalLine(adjustedTime, color: TimerMarkerLineColor);

            audioPlot.Refresh();
        }

        private double ConvertTimeUnit(double time, TimeUnit from, TimeUnit to)
        {
            if (from == TimeUnit.Milliseconds && to == TimeUnit.Seconds)
                return TimeSpan.FromMilliseconds(time).TotalSeconds;
            else if (from == TimeUnit.Milliseconds && to == TimeUnit.Minutes)
                return TimeSpan.FromMilliseconds(time).TotalMinutes;
            else if (from == TimeUnit.Seconds && to == TimeUnit.Milliseconds)
                return TimeSpan.FromSeconds(time).TotalMilliseconds;
            else if (from == TimeUnit.Seconds && to == TimeUnit.Minutes)
                return TimeSpan.FromSeconds(time).TotalMinutes;
            else if (from == TimeUnit.Minutes && to == TimeUnit.Milliseconds)
                return TimeSpan.FromMinutes(time).TotalMilliseconds;
            else if (from == TimeUnit.Minutes && to == TimeUnit.Seconds)
                return TimeSpan.FromMinutes(time).TotalSeconds;

            return time;
        }

        private void RemoveTimeMarkerLine()
        {
            if (_timeMarkerLine is not null)
                audioPlot.Plot.Remove(_timeMarkerLine);

            audioPlot.Refresh();
        }

        private void channel1Checkbox_Checked(object sender, RoutedEventArgs e)
        {
            if (_channel1Plot is null) throw new Exception($"Unexpected {nameof(_channel1Plot)} value.");
            _channel1Plot.IsVisible = true;
            audioPlot.Refresh();
        }

        private void channel2Checkbox_Checked(object sender, RoutedEventArgs e)
        {
            if (_channel2Plot is null) throw new Exception($"Unexpected {nameof(_channel2Plot)} value.");
            _channel2Plot.IsVisible = true;
            audioPlot.Refresh();
        }

        private void channel1Checkbox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_channel1Plot is null) throw new Exception($"Unexpected {nameof(_channel1Plot)} value.");
            _channel1Plot.IsVisible = false;
            audioPlot.Refresh();
        }

        private void channel2Checkbox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_channel2Plot is null) throw new Exception($"Unexpected {nameof(_channel2Plot)} value.");
            _channel2Plot.IsVisible = false;
            audioPlot.Refresh();
        }
    }
}
