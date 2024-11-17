using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace RDR2_Image_Converter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Stream imageStream;
        public MainWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        ~MainWindow()
        {
            imageStream.Dispose();
        }

        private void btnStartConv_Click(object sender, RoutedEventArgs e)
        {
            string? srcDir = tboxSrcDir.Text;
            string? dstDir = tboxDstDir.Text;

            if (string.IsNullOrEmpty(srcDir) || string.IsNullOrEmpty(dstDir))
            {
                MessageBox.Show("Please select source and destination directories.");
                return;
            }

            if (!System.IO.Directory.Exists(srcDir))
            {
                MessageBox.Show("Source directory does not exist.");
                return;
            }

            if (!System.IO.Directory.Exists(dstDir))
            {
                MessageBox.Show("Destination directory does not exist.");
                return;
            }

            SaveSettings();

            var files = System.IO.Directory.GetFiles(srcDir, "PRDR*");
            var convertedImages = new List<string>();

            foreach (var file in files)
            {
                tboxConvLog.Text += $"Processing file: {file}\n";

                Bitmap? image = extractImageFromFile(file);
                if (image == null)
                {
                    tboxConvLog.Text += $"Failed to extract image from file: {file}\n";
                    continue;
                }

                string dstPath = System.IO.Path.Combine(dstDir, System.IO.Path.GetFileName(file) + ".jpg");
                try
                {
                    image.Save(dstPath, ImageFormat.Jpeg);
                    tboxConvLog.Text += $"Successfully saved image to: {dstPath}\n";
                    convertedImages.Add(dstPath);
                }
                catch (ExternalException ex)
                {
                    tboxConvLog.Text += $"Failed to save image: {ex.Message}\n";
                }
                finally
                {
                    image.Dispose();
                }
            }

            tboxConvLog.Text += "Conversion process completed.\n";

            if (convertedImages.Count > 0)
            {
                var previewWindow = new ImagePreviewWindow(convertedImages);
                previewWindow.Show();
            }
        }

        private void btnSelectSrcDir_Click(object sender, RoutedEventArgs e)
        {
            string? path = selectPath();
            if (!string.IsNullOrEmpty(path))
            {
                tboxSrcDir.Text = path;
            }
        }

        private void btnSelectDstDir_Click(object sender, RoutedEventArgs e)
        {
            string? path = selectPath();
            if (!string.IsNullOrEmpty(path))
            {
                tboxDstDir.Text = path;
            }
        }

        private void LoadSettings()
        {
            tboxSrcDir.Text = Properties.Settings.Default.SrcDir;
            tboxDstDir.Text = Properties.Settings.Default.DstDir;
        }

        private void SaveSettings()
        {
            Properties.Settings.Default.SrcDir = tboxSrcDir.Text;
            Properties.Settings.Default.DstDir = tboxDstDir.Text;
            Properties.Settings.Default.Save();
        }

        private Bitmap? extractImageFromFile(string path)
        {
            int findMarker(byte[] marker, byte[] fileBytes)
            {
                int markerIndex = -1;
                for (int i = 0; i < fileBytes.Length - 1; i++)
                {
                    if (fileBytes[i] == marker[0] && fileBytes[i + 1] == marker[1])
                    {
                        markerIndex = i;
                        break;
                    }
                }

                return markerIndex;
            }

            // JPEG SOI and EOI markers
            byte[] soi = new byte[] { 0xFF, 0xD8 };
            byte[] eoi = new byte[] { 0xFF, 0xD9 };

            // Read file
            byte[] bytes = System.IO.File.ReadAllBytes(path);

            // Find SOI and EOI marker
            int soiIndex = findMarker(soi, bytes);
            int eoiIndex = findMarker(eoi, bytes);
            if (soiIndex == -1 || eoiIndex == -1)
            {
                return null;
            }

            // Extract image
            byte[] image = new byte[eoiIndex - soiIndex + 2];
            System.Buffer.BlockCopy(bytes, soiIndex, image, 0, image.Length);

            // Convert to bitmap
            imageStream = new MemoryStream(image);
            return new Bitmap(imageStream);
        }

        private string? selectPath()
        {
            var dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                return dialog.FileName;
            }

            return string.Empty;
        }
    }
}