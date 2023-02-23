using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Wpf.Ui.Controls;

namespace EZMediafireDownloader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : UiWindow
    {
        private WebClient client = new WebClient();
        private static Stopwatch sw = new Stopwatch();

        private string lastPath = string.Empty;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void UiWindow_Loaded(object sender, RoutedEventArgs e)
        {
            GetSettings();

            //Load icon
            ImageSource imageSource = Imaging.CreateBitmapSourceFromHIcon(
                                      Properties.Resources.icon.Handle,
                                      Int32Rect.Empty,
                                      BitmapSizeOptions.FromEmptyOptions());
            TitleBar.Icon = imageSource;
        }

        private void GetSettings()
        {
            Savepath_CheckBox.IsChecked = Properties.Settings.Default.SaveLastSavePath;
            CloseDone_CheckBox.IsChecked = Properties.Settings.Default.CloseOnFinish;
        }

        private void CloseDone_CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (CloseDone_CheckBox.IsChecked == true)
            {
                Properties.Settings.Default.CloseOnFinish = true;
            }
            else
            {
                Properties.Settings.Default.CloseOnFinish = false;
            }

            Properties.Settings.Default.Save();
        }

        private void Savepath_CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (Savepath_CheckBox.IsChecked == true)
            {
                Properties.Settings.Default.SaveLastSavePath = true;
            }
            else
            {
                Properties.Settings.Default.SaveLastSavePath = false;
            }

            Properties.Settings.Default.Save();
        }

        private void FastDownload_Button_Click(object sender, RoutedEventArgs e)
        {
            StartDownload(true);
        }

        private void Download_Button_Click(object sender, RoutedEventArgs e)
        {
            StartDownload();
        }

        private string SelectDest(string fileName = "REPLACE_THE_NAME_AND_ENDING")
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.FileName = fileName;
            if (saveFileDialog.ShowDialog() == true)
            {
                return saveFileDialog.FileName;
            }

            return string.Empty;
        }

        private void ChangeDownloadInfoVisibility(bool hidden)
        {
            if (!hidden)
            {
                DownloadInfo_TextBlock.Visibility = Visibility.Visible;
                DownloadInfo_TextBlock.Text = "Please wait...";
                Download_ProgressBar.Visibility = Visibility.Visible;
                return;
            }
            DownloadInfo_TextBlock.Visibility = Visibility.Collapsed;
            Download_ProgressBar.Visibility = Visibility.Collapsed;

            DownloadInfo_TextBlock.Text = string.Empty;
            Download_ProgressBar.Value = 0;
        }

        private string GetDownloadFolderPath()
        {
            return Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders", "{374DE290-123F-4565-9164-39C4925E467B}", String.Empty).ToString();
        }

        private void StartDownload(bool quick = false)
        {
            string fileName = string.Empty;

            try
            {
                fileName = Mediafire.MediafireDownloader.GetMediafireDDL(MediafireLink_TextBox.Text).Split('/').Last();
            }
            catch (Exception)
            {
                if (string.IsNullOrWhiteSpace(MediafireLink_TextBox.Text))
                {
                    System.Windows.MessageBox.Show("Link field is empty, enter a valid Link!", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                System.Windows.MessageBox.Show("The entered Mediafire link is not working...", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            client.DownloadProgressChanged += Client_DownloadProgressChanged;
            client.DownloadFileCompleted += Client_DownloadFileCompleted;

            ChangeDownloadInfoVisibility(false);

            string dest;

            if (!quick)
            {
                if (Properties.Settings.Default.SaveLastSavePath && Properties.Settings.Default.LastSavePath != string.Empty)
                {
                    dest = Properties.Settings.Default.LastSavePath + "\\" + fileName;
                }
                else
                {
                    dest = SelectDest(fileName);
                }

                if (dest != string.Empty)
                {
                    Mediafire.MediafireDownloader.DownloadMediafireFileAsync(MediafireLink_TextBox.Text, dest, client);
                    sw.Start();
                }
            }
            else
            {
                dest = GetDownloadFolderPath() + "\\" + fileName;
                Mediafire.MediafireDownloader.DownloadMediafireFileAsync(MediafireLink_TextBox.Text, dest, client);
                sw.Start();
            }

            lastPath = dest.Replace("\\" + fileName, string.Empty);
        }

        private void Client_DownloadFileCompleted(object? sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            sw.Stop();

            ChangeDownloadInfoVisibility(true);

            if (Properties.Settings.Default.SaveLastSavePath)
            {
                Properties.Settings.Default.LastSavePath = string.Empty;
                Properties.Settings.Default.LastSavePath = lastPath;
                Properties.Settings.Default.Save();
            }
            else
            {
                Properties.Settings.Default.LastSavePath = string.Empty;
            }

            if (Properties.Settings.Default.CloseOnFinish)
            {
                Close();
            }
        }

        private void Client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            double bytesIn = double.Parse(e.BytesReceived.ToString());
            double totalBytes = double.Parse(e.TotalBytesToReceive.ToString());

            // Calculate progress values
            double fileSize = totalBytes / 1024.0 / 1024.0;
            double downloadSpeed = e.BytesReceived / 1024.0 / 1024.0 / sw.Elapsed.TotalSeconds;

            //Calculate ETA
            double remainingBytes = totalBytes - bytesIn;
            double remainingTime = remainingBytes / (downloadSpeed * 1024 * 1024);
            string remainingTimeString = TimeSpan.FromSeconds(remainingTime).ToString(@"hh\:mm\:ss");

            string progressBarText = string.Format("{0} MB/s | {1}: {2} MB | ETA: {3}",
                downloadSpeed.ToString("0.00"),
                "Downlaod Info",
                GetFileSizeWithoutComma(fileSize),
                remainingTimeString);

            Download_ProgressBar.Value = e.ProgressPercentage;
            DownloadInfo_TextBlock.Text = progressBarText;
        }

        private static string GetFileSizeWithoutComma(double totalBytes)
        {
            if (totalBytes.ToString().Contains(","))
            {
                return totalBytes.ToString().Split(',')[0];
            }

            return totalBytes.ToString().Split('.')[0];
        }
    }
}