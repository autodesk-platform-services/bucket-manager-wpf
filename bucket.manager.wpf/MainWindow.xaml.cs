using Microsoft.Win32;

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Net.Http;
using Microsoft.Web.WebView2.Core;

using Autodesk.Authentication.Model;
using Autodesk.Oss.Model;

using bucket.manager.wpf.ViewModels;
using bucket.manager.wpf.Utils;
using bucket.manager.wpf.Views;
using bucket.manager.wpf.APSUtils;


namespace bucket.manager.wpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private DateTime _expiresAt;
        private HighPrecisionTimer? _tokenTimer;
        private HighPrecisionTimer? _translationTimer;
        private readonly MainWindowDataContext _context = new();
        private readonly ObservableCollection<BucketVM> _buckets = new();
        private bool _uiWait;
        
        private const int TimeTickInterval = 500;
        private const int TranslateTickInterval = 5000;
        private const int TranslateTimeLimit = 5000 * 12 * 60; // Sixty minutes
        private MonacoEditorHost? _editorHost;

        public bool EditorShown = false;
        public MainWindow()
        {

            InitializeComponent();
            _editorHost = new MonacoEditorHost(this);
           
            Icon = ImageFromBytes.GetBitmapImage(StringResources.logo);
            DataContext = _context;
            APSBucketsTree.ItemsSource = _buckets;
            _context.StatusBarText = StringResources.statusReady;

            APSClientId.Text = Environment.GetEnvironmentVariable("APS_CLIENT_ID") ?? string.Empty;
            APSClientSecret.Text = Environment.GetEnvironmentVariable("APS_CLIENT_SECRET") ?? string.Empty;

        }

        private void CleanUp()
        {
            _context.UIEnabled = true;
            _context.IsProgressBarIndetermined = false;
            _context.ProgressBarPercentage = 0;
            if (!_context.StatusBarText.Contains(StringResources.errSuffix))
                _context.StatusBarText = StringResources.statusReady;
        }

        private async Task APSAPICaller(Func<Task> function)
        {
            try
            {
                _context.UIEnabled = false;
                _context.IsProgressBarIndetermined = true;
                await function();
            }
            catch (HttpRequestException ee)
            {
                MessageBox.Show(ee.Message, StringResources.errorCaption);
                _context.StatusBarText = StringResources.errGeneral;
                _uiWait = false;
                CleanUp();
            }
            catch (Exception ee)
            {
                MessageBox.Show(ee.ToString(), StringResources.errorCaption);
                _context.StatusBarText = StringResources.errGeneral;
                _uiWait = false;
                CleanUp();
            }
            finally
            {
                if (!_uiWait)
                {
                    CleanUp();
                }

            }
        }
        private async void AuthenticateButton_Click(object sender, RoutedEventArgs e)
        {
            await APSAPICaller(async () =>
            {
                _uiWait = true;
                var id = APSClientId.Text;
                var secret = APSClientSecret.Text;

                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(secret))
                {
                    _context.StatusBarText = StringResources.errMissingCredentials;
                    return;
                }

                var token = await Authentication.GetToken(id, secret,
                    [Scopes.BucketRead, Scopes.BucketCreate, Scopes.DataRead, Scopes.DataWrite, Scopes.ViewablesRead]);
                       
                
                _context.AccessToken = token.AccessToken;
                if (token.ExpiresIn != null)
                {
                    _expiresAt = DateTime.Now.AddSeconds(token.ExpiresIn.Value);
                }

                // keep track on time
                _tokenTimer = new HighPrecisionTimer()
                {
                    Interval = TimeTickInterval
                };
                _tokenTimer.Elapsed += TokenTimer_Elapsed;
                _tokenTimer.Start();
                RefreshBucketButton_Click(null, null);

            });
        }

        // Update timeout countdown
        private void TokenTimer_Elapsed(object? sender, EventArgs e)
        {
            var secondsLeft = (_expiresAt - DateTime.Now).TotalSeconds;

            if (secondsLeft < 0)
                secondsLeft = 0;
            // We need update the timer asap, use invoke instead of data binding.
            try
            {
                Dispatcher.Invoke(() =>
                {
                    TimeOut.Text = secondsLeft.ToString("0");
                    TimeOut.Background = secondsLeft < 60
                        ? System.Windows.Media.Brushes.Red
                        : System.Windows.Media.Brushes.Transparent;
                });
            }
            catch (Exception)
            {
                //Igonre Cancellation exception
            }

            if (secondsLeft == 0)
            {
                _context.AccessToken = string.Empty;
                _tokenTimer.Stop();
            }
        }
        private async Task UpdateBucketObjects(BucketVM nodeBucket, bool wantExpand = false)
        {
            nodeBucket.Items.Clear();

           _context.StatusBarText = StringResources.statusRefreshingBucketItems + nodeBucket.Key;
            // show objects on the given TreeNode
            var objectsList = await OSS.GetBucketObjectsAsync(nodeBucket.Key, _context.AccessToken);

            foreach (var objInfo in objectsList.Items)
            {
                var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(objInfo.ObjectId);

                var item = new BucketItemVM(nodeBucket)
                {
                    Name = objInfo.ObjectKey,
                    Key = Convert.ToBase64String(plainTextBytes)
                };
                nodeBucket.Items.Add(item);
            }

            nodeBucket.IsExpanded = wantExpand;
        }
        private async void RefreshBucketButton_Click(object sender, RoutedEventArgs e)
        {
            await APSAPICaller(async () =>
            {

                _buckets.Clear();
               
                // Control GetBucket pagination
                string? lastBucket = null;
                Buckets buckets;
                do
                {
                    _context.StatusBarText = StringResources.statusRefreshingBuckets;
                    buckets = (await OSS.GetBucketsAsync(_context.Region, _context.AccessToken, 100, lastBucket));

                    foreach (var bucketsItem in buckets.Items)
                    {
                        var nodeBucket = new BucketVM
                        {
                            Key = bucketsItem.BucketKey
                        };
                        _buckets.Add(nodeBucket);
                        lastBucket = bucketsItem.BucketKey;
                    }

                } while (buckets.Items.Count > 0);

                // for each bucket, show the objects
                foreach (var bucket in _buckets)
                {
                    await UpdateBucketObjects(bucket);
                }
                if (_uiWait)
                {
                    _uiWait = false;
                    CleanUp();
                }
            });
        }

        private async void UploadFileButton_Click(object sender, RoutedEventArgs e)
        {

            await APSAPICaller(async () =>
            {
                _context.IsProgressBarIndetermined = false;

                if (APSBucketsTree.SelectedItem is null or not BucketVM)
                {
                    MessageBox.Show(StringResources.errorUploadMessage, StringResources.errorUploadCaption);
                    return;
                }


                var bucket = APSBucketsTree.SelectedItem as BucketVM;
                var bucketKey = bucket?.Key;

                // ask user to select file
                var formSelectFile = new OpenFileDialog
                {
                    Multiselect = false
                };
                if (formSelectFile.ShowDialog() is null or false)
                {
                    return;
                }

                var filePath = formSelectFile.FileName;
                var objectKey = Path.GetFileName(filePath);

                // get file size
                var fileSize = (new FileInfo(filePath)).Length;

                // show progress bar for upload
                _context.StatusBarText = StringResources.statusPreparingUpload;
                var result = await OSS.UploadFileWithProgress(bucketKey!, objectKey, filePath, _context.AccessToken,
                    new ProgressUpdater(_context));
                if (result.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    await UpdateBucketObjects(bucket!, true);
                }
                else
                {
                    _context.StatusBarText = $"{StringResources.errorCaption} : {result.Content.ToString() ?? string.Empty}";
                }
                
                
            });
        }

        private async void DeleteObjectButton_Click(object sender, RoutedEventArgs e)
        {
            await APSAPICaller(async () =>
            {
                var selectedItem = APSBucketsTree.SelectedItem;
                if (selectedItem is null or not BucketItemVM)
                {
                    MessageBox.Show(StringResources.errorObjectSelectionMessage, StringResources.errorObjectSelectionCaption);
                    return;
                }
                var bucketItem = selectedItem as BucketItemVM;
                var dlgResult = MessageBox.Show(bucketItem.Name + StringResources.msgDeleteConfirm,
                    StringResources.msgDeleteConfirmCaption, MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (dlgResult != MessageBoxResult.Yes)
                    return;
                

                _context.StatusBarText = StringResources.statusDelete + bucketItem.Name;
                await OSS.DeleteObjectAsync(bucketItem.ParentKey, bucketItem.Name, _context.AccessToken);
                bucketItem.Remove();
            });
        }

        private async void IsTranslationReady(object sender, EventArgs e)
        {
          
            // get the translation manifest
            var manifest = await ModelDerivatives.GetManifestAsync( _translationTimer.BucketItem?.Key, _context.AccessToken, _context.Region);
            if (manifest is null)
            {
                return;
            }
            var progress = (string.IsNullOrWhiteSpace(Regex.Match(manifest.Progress, @"\d+").Value)
                ? 100
                : int.Parse(Regex.Match(manifest.Progress, @"\d+").Value));

            // for better UX, show a small number of progress (instead zero)
            _context.ProgressBarPercentage = (progress == 0 ? 1 : progress);
            var eventArg = e as HighPrecisionTimer.TickEvent;
            // if ready, reset percentage to 0
            if (progress >= 100)
            {
                _context.ProgressBarPercentage = 0;
                _translationTimer.Stop();
                _uiWait = false;
                CleanUp();
            }

            if (eventArg is { Total: > TranslateTimeLimit })
            {
                _context.ProgressBarPercentage = 0;
                _translationTimer.Stop();
                _uiWait = false;
                _context.StatusBarText = StringResources.errorTranslateTimeout;
                CleanUp();
            }
        }
        private async void TranslateMenuItemClick(object sender, RoutedEventArgs e)
        {
            await APSAPICaller(async () =>
            {
                var item = sender as MenuItem;
       
                var selectedItem = APSBucketsTree.SelectedItem;
                if (selectedItem is null or not BucketItemVM)
                {
                    MessageBox.Show(StringResources.errorObjectSelectionMessage, StringResources.errorObjectSelectionCaption);
                    return;
                }
               
                var bucketItem = selectedItem as BucketItemVM;
                _context.StatusBarText = string.Format("{0}, {1}", bucketItem.Name,
                    StringResources.statusTranslating);
                var urn = bucketItem?.Key;


                _context.ProgressBarMaximum = 100;
                _context.ProgressBarPercentage = 0;
                _context.IsProgressBarIndetermined = false;
                await ModelDerivatives.TranslateAsync(urn, _context.AccessToken, _context.Region,
                    item?.Name ?? "svf2");
                _uiWait = true;

                // start a monitor job to follow the translation
                _translationTimer = new HighPrecisionTimer();
                _translationTimer.Elapsed += IsTranslationReady;
                _translationTimer.BucketItem = bucketItem;
                _translationTimer.Interval = TranslateTickInterval;
                _translationTimer.Start();

            });

        }

        // Disable context menu when right clicking
        private void TranslateFileButton_OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private void DevToolsButton_Click(object sender, RoutedEventArgs e)
        {
            WebView.CoreWebView2.OpenDevToolsWindow();
        }

        private async void DownloadSVFButton_Click(object sender, RoutedEventArgs e)
        {
            await APSAPICaller(async () =>
            {
                if (APSBucketsTree.SelectedItem is null or not BucketItemVM)
                {
                    MessageBox.Show(StringResources.errorObjectSelectionMessage,
                        StringResources.errorObjectSelectionCaption);
                    return;
                }

                var bucketItem = APSBucketsTree.SelectedItem as BucketItemVM;
                var urn = bucketItem?.Key;
                var folderPicker = new FolderPicker();

                if (folderPicker.ShowDialog() != true)
                {
                    return;
                }

                var folderPath = folderPicker.ResultPath;
                folderPath = Path.Combine(folderPath, bucketItem?.Name + ".md");
                if (Directory.Exists(folderPath))
                {
                    Directory.Delete(folderPath, true);
                }
                Directory.CreateDirectory(folderPath);


                _context.StatusBarText = StringResources.statusDownloading;
                // get the list of resources to download
                var resourcesToDownload = await ModelDerivatives.PrepareUrlForDownload(urn, _context.AccessToken, _context.Region);

                // update the UI
                _context.ProgressBarPercentage = 0;
                _context.ProgressBarMaximum = resourcesToDownload.Count;
                _context.IsProgressBarIndetermined = false;
                var client = new HttpClient();
                foreach (ModelDerivatives.Resource resource in resourcesToDownload)
                {

                    _context.StatusBarText = StringResources.statusDownloadingFile + resource.FileName;

                    // prepare the GET to download the file
                    var request = new HttpRequestMessage(HttpMethod.Get, resource.RemotePath);
                    request.Headers.TryAddWithoutValidation("Content-Type", resource.ContentType);
                    request.Headers.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate");
                    request.Headers.TryAddWithoutValidation("Cookie", resource.CookieList);
                    var response = await client.SendAsync(request);

                    if (response.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        // something went wrong with this file...
                        MessageBox.Show(StringResources.errorDownloading + resource.FileName, response.StatusCode.ToString());

                        // any other action?
                    }
                    else
                    {
                        // combine with selected local path
                        var pathToSave = Path.Combine(folderPath, resource.LocalPath);
                        if (!string.IsNullOrWhiteSpace(pathToSave))
                        {
                            // ensure local dir exists
                            Directory.CreateDirectory(Path.GetDirectoryName(pathToSave)!);
                            await using var s = await response.Content.ReadAsStreamAsync();
                            await using var fs = new FileStream(pathToSave, FileMode.Create);
                            await s.CopyToAsync(fs);
                            // save file
                            
                        }
                    }
                    
                    ++_context.ProgressBarPercentage;

                }

            });
        }

        private async void CreateBucketButton_Click(object sender, RoutedEventArgs e)
        {
            await APSAPICaller(async () =>
            {

                if (string.IsNullOrEmpty(_context.AccessToken))
                {
                    MessageBox.Show(StringResources.errorNotAuthenticate, StringResources.errorCaption);
                    return;
                }

                var createDialog = new CreateBucketPrompt();
                var result = createDialog.ShowDialog();
                if (result == true)
                {
  
                    _uiWait = true;
                    var bucketKey = createDialog.AddGuid.IsChecked == true ? $"{createDialog.BucketName.Text}.{Guid.NewGuid()}" : createDialog.BucketName.Text;
                    await OSS.CreateBucketAsync(_context.Region, bucketKey, "transient", _context.AccessToken);
                    RefreshBucketButton_Click(null!, null!);
                }
            });
        }

        private void JSButton_Click(object sender, RoutedEventArgs e)
        {
            if (_editorHost is null)
            {
                _editorHost = new MonacoEditorHost(this);
                _editorHost.Show();
            }
            else
            {
                _editorHost.Show();
            }
            _editorHost.Focus();
            EditorShown = true;
        }

        public async void ExecuteJavascript(string javascript)
        {
            await WebView.CoreWebView2.ExecuteScriptAsync(javascript);
        }

        public void SetEditorHostNull()
        {
            _editorHost = null;
        }

        private void MainWindow_OnClosing(object? sender, CancelEventArgs e)
        {
            if (_editorHost != null)
            {
                _editorHost.IsAppExiting = true;
                if(EditorShown)
                    _editorHost.Show();
                _editorHost.Close();
                e.Cancel = EditorShown;
            }
            else
            {
                e.Cancel = false;
            }
        }

        private void WebView_OnCoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            WebView.CoreWebView2.SetVirtualHostNameToFolderMapping("bucketmanager", "", CoreWebView2HostResourceAccessKind.Allow);
            WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            WebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
        }

        private void BucketsTree_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not TextBlock)
                return;
            if (APSBucketsTree.SelectedItem is BucketItemVM bucketItem)
            {
                WebView.CoreWebView2.Navigate($"https://bucketmanager/HTML/Viewer.html?URN={bucketItem.Key}&Token={_context.AccessToken}");
            }
        }

        internal class ProgressUpdater : IProgress<int>
        {
            private readonly MainWindowDataContext _context;

            public ProgressUpdater(MainWindowDataContext context)
            {
                _context = context;
                _context.IsProgressBarIndetermined = true;
                _context.ProgressBarPercentage = 0;
                _context.ProgressBarMaximum = 100;
            }

            public void Report(int value)
            {
                _context.IsProgressBarIndetermined = false;
                _context.ProgressBarPercentage = value;
            }
        }

        private void Region_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var data = e.AddedItems[0] as ComboBoxItem;
            _context.Region = data.Content.ToString();
        }
    }
}
