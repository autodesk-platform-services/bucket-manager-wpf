using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Autodesk.Forge;
using Autodesk.Forge.Model;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Autodesk.Forge.Client;
using bucket.manager.wpf.ViewModels;
using Microsoft.Win32;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
using RestSharp;
using bucket.manager.wpf.Utils;
using bucket.manager.wpf.Views;
using Microsoft.Web.WebView2.Core.Raw;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;

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
        public MainWindow()
        {

            InitializeComponent();
            _editorHost = new MonacoEditorHost(this);
           
            Icon = ImageFromBytes.GetBitmapImage(StringResources.logo);
            DataContext = _context;
            ForgeBucketsTree.ItemsSource = _buckets;
            _context.StatusBarText = StringResources.statusReady;

            ForgeClientId.Text = Environment.GetEnvironmentVariable("FORGE_CLIENT_ID");
            ForgeClientSecret.Text = Environment.GetEnvironmentVariable("FORGE_CLIENT_SECRET");

        }

        private void CleanUp()
        {
            _context.UIEnabled = true;
            _context.IsProgressBarIndetermined = false;
            _context.ProgressBarPercentage = 0;
            if (!_context.StatusBarText.Contains(StringResources.errSuffix))
                _context.StatusBarText = StringResources.statusReady;
        }

        private async Task ForgeApiCaller(Func<Task> function)
        {
            try
            {
                _context.UIEnabled = false;
                _context.IsProgressBarIndetermined = true;
                await function();
            }
            catch (ApiException ee)
            {
                MessageBox.Show(ee.ErrorContent, StringResources.errorCaption + ee.ErrorCode);
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
            await ForgeApiCaller(async () =>
            {
                _uiWait = true;
                var id = ForgeClientId.Text;
                var secret = ForgeClientSecret.Text;

                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(secret))
                {
                    _context.StatusBarText = StringResources.errMissingCredentials;
                    return;
                }

                var oAuth = new TwoLeggedApi();
                _context.StatusBarText = StringResources.statusAuthenticating;
                Bearer token = (await oAuth.AuthenticateAsync(
                        id,
                        secret,
                        oAuthConstants.CLIENT_CREDENTIALS,
                        new Scope[] { Scope.BucketRead, Scope.BucketCreate, Scope.DataRead, Scope.DataWrite }))
                    .ToObject<Bearer>();



                _context.AccessToken = token.AccessToken;
                if (token.ExpiresIn != null) _expiresAt = DateTime.Now.AddSeconds(token.ExpiresIn.Value);

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

            var objects = new ObjectsApi
            {
                Configuration =
                {
                    AccessToken = _context.AccessToken
                }
            };

            var derivative = new DerivativesApi
            {
                Configuration =
                {
                    AccessToken = _context.AccessToken
                }
            };

            _context.StatusBarText = StringResources.statusRefreshingBucketItems + nodeBucket.Key;
            // show objects on the given TreeNode
            BucketObjects objectsList = (await objects.GetObjectsAsync(nodeBucket.Key)).ToObject<BucketObjects>();
            foreach (var objInfo in objectsList.Items)
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
            await ForgeApiCaller(async () =>
            {

                _buckets.Clear();
                var bucketApi = new BucketsApi
                {
                    Configuration =
                    {
                        AccessToken = _context.AccessToken
                    }
                };

                // Control GetBucket pagination
                string? lastBucket = null;
                Buckets buckets;
                do
                {
                    _context.StatusBarText = StringResources.statusRefreshingBuckets;
                    buckets = (await bucketApi.GetBucketsAsync(Region.Text, 100, lastBucket)).ToObject<Buckets>();
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

            await ForgeApiCaller(async () =>
            {
                _context.IsProgressBarIndetermined = false;

                if (ForgeBucketsTree.SelectedItem is null or not BucketVM)
                {
                    MessageBox.Show(StringResources.errorUploadMessage, StringResources.errorUploadCaption);
                    return;
                }


                var bucket = ForgeBucketsTree.SelectedItem as BucketVM;
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

                var objects = new ObjectsApi
                {
                    Configuration =
                    {
                                    AccessToken = _context.AccessToken
                    }
                };

                // get file size
                var fileSize = (new FileInfo(filePath)).Length;

                // show progress bar for upload
                _context.StatusBarText = StringResources.statusPreparingUpload;
                _context.IsProgressBarIndetermined = true;

                // decide if upload direct or resumable (by chunks)
                if (fileSize > UploadChunkSize) // upload in chunks
                {
                    var numberOfChunks = (long)Math.Round((double)(fileSize / UploadChunkSize)) + 1;

                    _context.ProgressBarMaximum = (int)numberOfChunks;

                    var start = 0L;
                    var chunkSize = (numberOfChunks > 1 ? UploadChunkSize : fileSize);
                    var end = chunkSize;
                    var sessionId = Guid.NewGuid().ToString();

                    // upload one chunk at a time
                    using (var reader = new BinaryReader(new FileStream(filePath, FileMode.Open)))
                    {
                        for (var chunkIndex = 0; chunkIndex < numberOfChunks; chunkIndex++)
                        {
                            var range = $"bytes {start}-{end}/{fileSize}";

                            var numberOfBytes = chunkSize + 1;
                            var fileBytes = new byte[numberOfBytes];
                            var memoryStream = new MemoryStream(fileBytes);
                            reader.BaseStream.Seek(start, SeekOrigin.Begin);
                            var count = reader.Read(fileBytes, 0, (int)numberOfBytes);
                            memoryStream.Write(fileBytes, 0, (int)numberOfBytes);
                            memoryStream.Position = 0;

                            await objects.UploadChunkAsync(bucketKey, objectKey,
                                (int)numberOfBytes, range, sessionId, memoryStream);

                            start = end + 1;
                            chunkSize = ((start + chunkSize > fileSize) ? fileSize - start - 1 : chunkSize);
                            end = start + chunkSize;
                            _context.IsProgressBarIndetermined = false;
                            _context.StatusBarText =
                                $"{(chunkIndex * chunkSize) / 1024.0 / 1024.0:F2}" + StringResources.statusMbsUploaded;
                            _context.ProgressBarPercentage = chunkIndex;
                        }
                    }

                    _context.ProgressBarPercentage = _context.ProgressBarMaximum;
                }
                else // upload in a single call
                {
                    using var streamReader = new StreamReader(filePath);
                    await objects.UploadObjectAsync(bucketKey,
                        objectKey, (int)streamReader.BaseStream.Length, streamReader.BaseStream,
                        "application/octet-stream");
                }

                await UpdateBucketObjects(bucket, true);
            });
        }

        private async void DeleteObjectButton_Click(object sender, RoutedEventArgs e)
        {
            await ForgeApiCaller(async () =>
            {
                var selectedItem = ForgeBucketsTree.SelectedItem;
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
                var objects = new ObjectsApi
                {
                    Configuration =
                    {
                        AccessToken = _context.AccessToken
                    }
                };

                _context.StatusBarText = StringResources.statusDelete + bucketItem.Name;
                await objects.DeleteObjectAsync(bucketItem.ParentKey, bucketItem.Name);
                bucketItem.Remove();
            });
        }

        private async void IsTranslationReady(object sender, EventArgs e)
        {
            var derivative = new DerivativesApi
            {
                Configuration =
                {
                    AccessToken = _context.AccessToken
                }
            };

            // get the translation manifest
            dynamic manifest = await derivative.GetManifestAsync(_translationTimer.BucketItem?.Key);
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
            await ForgeApiCaller(async () =>
            {
                var item = sender as MenuItem;
                JobPayloadItem.TypeEnum workType;
                Enum.TryParse(item?.Name, out workType);
                var selectedItem = ForgeBucketsTree.SelectedItem;
                if (selectedItem is null or not BucketItemVM)
                {
                    MessageBox.Show(StringResources.errorObjectSelectionMessage, StringResources.errorObjectSelectionCaption);
                    return;
                }

                var bucketItem = selectedItem as BucketItemVM;
                _context.StatusBarText = string.Format("{0}, {1}", bucketItem.Name,
                    StringResources.statusTranslating);
                var urn = bucketItem?.Key;
                JobPayloadItem payloadItem = null;
                if (workType is JobPayloadItem.TypeEnum.Svf or JobPayloadItem.TypeEnum.Svf2)
                {
                    payloadItem = new JobPayloadItem(
                        workType,
                        new List<JobPayloadItem.ViewsEnum>()
                        {
                            JobPayloadItem.ViewsEnum._2d,
                            JobPayloadItem.ViewsEnum._3d
                        });
                }
                else
                {
                    payloadItem = new JobPayloadItem(workType);
                }

                var outputs = new List<JobPayloadItem>() { payloadItem };

                var derivative = new DerivativesApi
                {
                    Configuration =
                    {
                        AccessToken = _context.AccessToken
                    }
                };
                var job = new JobPayload(new JobPayloadInput(urn), new JobPayloadOutput(outputs));

                _context.ProgressBarMaximum = 100;
                _context.ProgressBarPercentage = 0;
                _context.IsProgressBarIndetermined = false;
                await derivative.TranslateAsync(job, true);
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
            await ForgeApiCaller(async () =>
            {
                if (ForgeBucketsTree.SelectedItem is null or not BucketItemVM)
                {
                    MessageBox.Show(StringResources.errorObjectSelectionMessage,
                        StringResources.errorObjectSelectionCaption);
                    return;
                }

                var bucketItem = ForgeBucketsTree.SelectedItem as BucketItemVM;
                var urn = bucketItem?.Key;
                var folderPicker = new FolderPicker();

                if (folderPicker.ShowDialog() != true) return;
                var folderPath = folderPicker.ResultPath;
                folderPath = Path.Combine(folderPath, bucketItem.Name + ".md");
                if (Directory.Exists(folderPath)) Directory.Delete(folderPath, true);
                Directory.CreateDirectory(folderPath);


                _context.StatusBarText = StringResources.statusDownloading;

                // get the list of resources to download
                var resourcesToDownload = await ForgeUtils.Derivatives.ExtractSVFAsync(urn, _context.AccessToken);

                // update the UI
                _context.ProgressBarMaximum = resourcesToDownload.Count;
                _context.ProgressBarPercentage = 0;
                _context.IsProgressBarIndetermined = false;
                var client = new RestClient("https://developer.api.autodesk.com/");
                foreach (ForgeUtils.Derivatives.Resource resource in resourcesToDownload)
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
            await ForgeApiCaller(async () =>
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
                    var buckets = new BucketsApi
                    {
                        Configuration =
                        {
                            AccessToken = _context.AccessToken
                        }
                    };
                    _uiWait = true;
                    var bucketKey = createDialog.AddGuid.IsChecked == true ? $"{createDialog.BucketName.Text}.{Guid.NewGuid()}" : createDialog.BucketName.Text;
                    var bucketPayload = new PostBucketsPayload(bucketKey.ToLower(), null, PostBucketsPayload.PolicyKeyEnum.Transient);
                    await buckets.CreateBucketAsync(bucketPayload, Region.Text);
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

        private void ForgeBucketsTree_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not TextBlock)
                return;
            if (ForgeBucketsTree.SelectedItem is BucketItemVM bucketItem)
            {
                WebView.CoreWebView2.Navigate($"https://bucketmanager/HTML/Viewer.html?URN={bucketItem.Key}&Token={_context.AccessToken}");
            }
        }
    }
}
