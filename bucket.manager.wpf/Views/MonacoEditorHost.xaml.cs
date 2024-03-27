using System.ComponentModel;
using System.Windows;
using bucket.manager.wpf.Utils;
using Microsoft.Web.WebView2.Core;

namespace bucket.manager.wpf.Views
{
    /// <summary>
    /// Interaction logic for MonacoEditorHost.xaml
    /// </summary>
    public partial class MonacoEditorHost : Window
    {

        private MonacoEditorInterop _monacoEditorInterop;
        private MainWindow _mainWindow;
        public bool CancelSave = false;
        public bool IsAppExiting = false;
        public bool CloseFromInterop = false;
        
        public MonacoEditorHost(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            _monacoEditorInterop = new MonacoEditorInterop(this, _mainWindow);
            InitializeComponent();
            Icon = ImageFromBytes.GetBitmapImage(StringResources.logo);
            //MonacoView.EnsureCoreWebView2Async();
        }
        
        private void MonacoView_OnCoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            MonacoView.CoreWebView2.SetVirtualHostNameToFolderMapping("monacohost", "", CoreWebView2HostResourceAccessKind.Allow);
            MonacoView.CoreWebView2.AddHostObjectToScript("bucketManager", _monacoEditorInterop);
            MonacoView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
            MonacoView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            MonacoView.CoreWebView2.Settings.AreDevToolsEnabled = false;

        }
        private void MonacoEditorHost_OnClosing(object? sender, CancelEventArgs e)
        {
            if (!_mainWindow.EditorShown)
            {
                e.Cancel = false;
                return;
            }
            if (IsAppExiting && !CloseFromInterop)
            {
                MonacoView.CoreWebView2.ExecuteScriptAsync(
                    "window.chrome.webview.hostObjects.sync.bucketManager.Close(editor.getValue());");
                e.Cancel = true;
                CloseFromInterop = false;
                CancelSave = false;
            }
            else
            {
                Hide();
                e.Cancel = !IsAppExiting;
                IsAppExiting = false;
                CancelSave = false;
            }

            if (!e.Cancel)
            {
                try
                {
                    _mainWindow.Close();
                }
                catch{}
            }
        }
    }
}
