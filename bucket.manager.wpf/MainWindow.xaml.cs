using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using bucket.manager.wpf.ViewModels;
using Microsoft.Win32;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.RegularExpressions;
using RestSharp;
using bucket.manager.wpf.Utils;
using bucket.manager.wpf.Views;
using Microsoft.Web.WebView2.Core;
using Autodesk.SDKManager;
using Autodesk.Authentication;
using Autodesk.Authentication.Model;
using Autodesk.Oss;
using Autodesk.ModelDerivative;
using Autodesk.Oss.Model;
using Autodesk.ModelDerivative.Model;

namespace bucket.manager.wpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private DateTime _expiresAt;
        //private Timer _tokenTimer;
        private HighPrecisionTimer? _tokenTimer;
        private HighPrecisionTimer? _translationTimer;
        private readonly MainWindowDataContext _context = new();
        private readonly ObservableCollection<BucketVM> _buckets = new();
        private bool _uiWait;
        
        private const int UploadChunkSize = 2 * 1024 * 1024;
        private const int TimeTickInterval = 500;
        private const int TranslateTickInterval = 5000;
        private const int TranslateTimeLimit = 5000 * 12 * 60; // Sixty minutes
        private MonacoEditorHost? _editorHost;

        public bool EditorShown = false;

        private SDKManager _sdkManager;

        public MainWindow()
        {

            InitializeComponent();
            _editorHost = new MonacoEditorHost(this);
           
            Icon = ImageFromBytes.GetBitmapImage(StringResources.logo);
            DataContext = _context;
            BucketsTree.ItemsSource = _buckets;
            _context.StatusBarText = StringResources.statusReady;

            ClientId.Text = Environment.GetEnvironmentVariable("APS_CLIENT_ID");
            ClientSecret.Text = Environment.GetEnvironmentVariable("APS_CLIENT_SECRET");

            _sdkManager = SdkManagerBuilder.Create().Build();
        }

        private void CleanUp()
        {
            _context.UIEnabled = true;
            _context.IsProgressBarIndetermined = false;
            _context.ProgressBarPercentage = 0;
            if (!_context.StatusBarText.Contains(StringResources.errSuffix))
                _context.StatusBarText = StringResources.statusReady;
        }

        private async Task APSApiCaller(Func<Task> function)
        {
            try
            {
                _context.UIEnabled = false;
                _context.IsProgressBarIndetermined = true;
                await function();
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
            await APSApiCaller(async () =>
            {
                _uiWait = true;
                var id = ClientId.Text;
                var secret = ClientSecret.Text;

                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(secret))
                {
                    _context.StatusBarText = StringResources.errMissingCredentials;
                    return;
                }

                _context.StatusBarText = StringResources.statusAuthenticating;
                var authenticationClient = new AuthenticationClient(_sdkManager);
                var auth = await authenticationClient.GetTwoLeggedTokenAsync(id, secret, new List<Scopes>() { Scopes.BucketRead, Scopes.BucketCreate, Scopes.DataRead, Scopes.DataWrite });
                _context.AccessToken = auth.AccessToken;
                if (auth.ExpiresIn != null) _expiresAt = DateTime.Now.AddSeconds(auth.ExpiresIn.Value);

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

            var ossClient = new OssClient(_sdkManager);

            _context.StatusBarText = StringResources.statusRefreshingBucketItems + nodeBucket.Key;
            // show objects on the given TreeNode
            var objects = await ossClient.GetObjectsAsync(nodeBucket.Key, accessToken: _context.AccessToken);
            foreach (var objInfo in objects.Items)
            {
                var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(objInfo.ObjectId);

                var item = new BucketItemVM(nodeBucket)
                {
                    Name = objInfo.ObjectKey,
                    Key = System.Convert.ToBase64String(plainTextBytes)
                };
                nodeBucket.Items.Add(item);
                /*
                // get the translation manifest
                try
                {
                    var manifest = (await derivative.GetManifestAsync(item.Key)).ToString();
                }
                catch (Exception)
                {
                    // Ignore, we want continue if there isn't a manifest available
                }
                */
            }

            nodeBucket.IsExpanded = wantExpand;
        }
        private async void RefreshBucketButton_Click(object sender, RoutedEventArgs e)
        {
            await APSApiCaller(async () =>
            {

                _buckets.Clear();
                var ossClient = new OssClient(_sdkManager);

                // Control GetBucket pagination
                string? lastBucket = null;
                Buckets buckets;
                do
                {
                    _context.StatusBarText = StringResources.statusRefreshingBuckets;
                    buckets = await ossClient.GetBucketsAsync(Region.Text, 100, lastBucket, accessToken: _context.AccessToken);
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

            await APSApiCaller(async () =>
            {
                _context.IsProgressBarIndetermined = false;

                if (BucketsTree.SelectedItem is null or not BucketVM)
                {
                    MessageBox.Show(StringResources.errorUploadMessage, StringResources.errorUploadCaption);
                    return;
                }


                var bucket = BucketsTree.SelectedItem as BucketVM;
                var bucketKey = bucket?.Key;

                // ask user to select file
                var formSelectFile = new OpenFileDialog
                {
                    Multiselect = false
                };
                if (formSelectFile.ShowDialog() is null or false)
                    return;
                var filePath = formSelectFile.FileName;
                var objectKey = Path.GetFileName(filePath);
                var ossClient = new OssClient(_sdkManager);

                // get file size
                var fileSize = (new FileInfo(filePath)).Length;

                // show progress bar for upload
                _context.StatusBarText = StringResources.statusPreparingUpload;
                _context.IsProgressBarIndetermined = true;

                await ossClient.Upload(bucketKey, objectKey, filePath, accessToken: _context.AccessToken, new System.Threading.CancellationToken());
                await UpdateBucketObjects(bucket, true);
            });
        }

        private async void DeleteObjectButton_Click(object sender, RoutedEventArgs e)
        {
            await APSApiCaller(async () =>
            {
                var selectedItem = BucketsTree.SelectedItem;
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
                var ossClient = new OssClient(_sdkManager);

                _context.StatusBarText = StringResources.statusDelete + bucketItem.Name;
                await ossClient.DeleteObjectAsync(bucketItem.ParentKey, bucketItem.Name, accessToken: _context.AccessToken);
                bucketItem.Remove();
            });
        }

        private async void IsTranslationReady(object sender, EventArgs e)
        {
            var modelDerivativeClient = new ModelDerivativeClient(_sdkManager);

            // get the translation manifest
            dynamic manifest = await modelDerivativeClient.GetManifestAsync(_translationTimer.BucketItem?.Key, accessToken: _context.AccessToken);
            var progress = (string.IsNullOrWhiteSpace(Regex.Match(manifest.progress, @"\d+").Value)
                ? 100
                : int.Parse(Regex.Match(manifest.progress, @"\d+").Value));

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
            await APSApiCaller(async () =>
            {
                var item = sender as MenuItem;
                var selectedItem = BucketsTree.SelectedItem;
                if (selectedItem is null or not BucketItemVM)
                {
                    MessageBox.Show(StringResources.errorObjectSelectionMessage, StringResources.errorObjectSelectionCaption);
                    return;
                }
                var bucketItem = selectedItem as BucketItemVM;
                _context.StatusBarText = string.Format("{0}, {1}", bucketItem.Name, StringResources.statusTranslating);
                var urn = bucketItem?.Key;
                var modelDerivativeClient = new ModelDerivativeClient(_sdkManager);
                var payload = new JobPayload
                {
                    Input = new JobPayloadInput
                    {
                        Urn = urn
                    },
                    Output = new JobPayloadOutput
                    {
                        Formats = new List<JobPayloadFormat>
                        {
                            new JobSvf2OutputFormat
                            {
                                Views = new List<View>
                                {
                                    View._2d,
                                    View._3d
                                }
                            }
                        },
                        Destination = new JobPayloadOutputDestination
                        {
                            Region = Autodesk.ModelDerivative.Model.Region.US
                        }
                    }
                };
                _context.ProgressBarMaximum = 100;
                _context.ProgressBarPercentage = 0;
                _context.IsProgressBarIndetermined = false;
                var job = await modelDerivativeClient.StartJobAsync(jobPayload: payload, accessToken: _context.AccessToken);
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
            await APSApiCaller(async () =>
            {
                if (BucketsTree.SelectedItem is null or not BucketItemVM)
                {
                    MessageBox.Show(StringResources.errorObjectSelectionMessage,
                        StringResources.errorObjectSelectionCaption);
                    return;
                }

                var bucketItem = BucketsTree.SelectedItem as BucketItemVM;
                var urn = bucketItem?.Key;
                var folderPicker = new FolderPicker();

                if (folderPicker.ShowDialog() != true) return;
                var folderPath = folderPicker.ResultPath;
                folderPath = Path.Combine(folderPath, bucketItem.Name + ".md");
                if (Directory.Exists(folderPath)) Directory.Delete(folderPath, true);
                Directory.CreateDirectory(folderPath);


                _context.StatusBarText = StringResources.statusDownloading;

                // get the list of resources to download
                var resourcesToDownload = await Utils.Derivatives.ExtractSVFAsync(urn, _context.AccessToken);

                // update the UI
                _context.ProgressBarMaximum = resourcesToDownload.Count;
                _context.ProgressBarPercentage = 0;
                _context.IsProgressBarIndetermined = false;
                var client = new RestClient("https://developer.api.autodesk.com/");
                foreach (Utils.Derivatives.Resource resource in resourcesToDownload)
                {

                    ++_context.ProgressBarPercentage;
                    _context.StatusBarText = StringResources.statusDownloadingFile + resource.FileName;

                    // prepare the GET to download the file
                    var request = new RestRequest(resource.RemotePath, Method.Get);
                    request.AddHeader("Authorization", "Bearer " + _context.AccessToken);
                    request.AddHeader("Accept-Encoding", "gzip, deflate");
                    var response = await client.ExecuteAsync(request);

                    if (response.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        // something went wrong with this file...
                        MessageBox.Show(StringResources.errorDownloading + resource.FileName, response.StatusCode.ToString());

                        // any other action?
                    }
                    else
                    {
                        // combine with selected local path
                        string pathToSave = Path.Combine(folderPath, resource.LocalPath);
                        // ensure local dir exists
                        Directory.CreateDirectory(Path.GetDirectoryName(pathToSave));
                        // save file
                        File.WriteAllBytes(pathToSave, response.RawBytes);
                    }
                }

            });
        }

        private async void CreateBucketButton_Click(object sender, RoutedEventArgs e)
        {
            await APSApiCaller(async () =>
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
                    var ossClient = new OssClient(_sdkManager);
                    _uiWait = true;
                    var bucketKey = createDialog.AddGuid.IsChecked == true ? $"{createDialog.BucketName.Text}.{Guid.NewGuid()}" : createDialog.BucketName.Text;
                    await ossClient.CreateBucketAsync(Region.Text, new CreateBucketsPayload { BucketKey = bucketKey.ToLower(), PolicyKey = "Transient" }, accessToken: _context.AccessToken);
                    RefreshBucketButton_Click(null, null);
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
            if (BucketsTree.SelectedItem is BucketItemVM bucketItem)
            {
                WebView.CoreWebView2.Navigate($"https://bucketmanager/HTML/Viewer.html?URN={bucketItem.Key}&Token={_context.AccessToken}");
            }
        }
    }
}
