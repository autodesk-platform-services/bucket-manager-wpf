using System;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace bucket.manager.wpf.Views
{
    [ClassInterface(ClassInterfaceType.AutoDual)]
    [ComVisible(true)]
    public class MonacoEditorInterop
    {
        private MonacoEditorHost _editorHost;
        private MainWindow _mainWindow;
        public MonacoEditorInterop(MonacoEditorHost editorWindow, MainWindow mainWindow)
        {
            _editorHost = editorWindow;
            _mainWindow = mainWindow;
        }

        public void SaveFile(string javaScript)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Javascript|*.js|Text|*.txt"
            };
            if (dialog.ShowDialog() == true)
            {
                if (string.IsNullOrEmpty(dialog.FileName))
                    return;
                using var file = File.CreateText(dialog.FileName);
                file.Write(javaScript);
            }
        }

        public string? LoadFile()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Javascript|*.js|Text|*.txt"
            };
            if (dialog.ShowDialog() == true)
            {
                using var file = File.OpenText(dialog.FileName);
                return file.ReadToEnd();
            }
            return null;
        }

        public void RunJavascript(string js)
        {
            _mainWindow.ExecuteJavascript(js);
        }

        public bool SaveBeforeClose(string code)
        {
            _editorHost.CancelSave = false;
            //_editorHost.CloseFromInterop = false;
            var result = MessageBox.Show(StringResources.msgSaveBeforeExit, StringResources.msgSaveCaption,
                MessageBoxButton.YesNoCancel);
            switch (result)
            {
                case MessageBoxResult.Yes:
                    var dialog = new SaveFileDialog
                    {
                        Filter = "Javascript|*.js|Text|*.txt"
                    };
                    if (dialog.ShowDialog() == true)
                    {
                        using var file = File.CreateText(dialog.FileName);
                        file.Write(code);
                        
                    }
                    else
                    {
                        _editorHost.CancelSave = true;
                        return false;
                    }
                    break;
                case MessageBoxResult.No:
                    break;
                case MessageBoxResult.Cancel:
                    _editorHost.CancelSave = true;
                    return false;
                    
            }
            _mainWindow.SetEditorHostNull();
            return true;
        }

        public void Close(string code)
        {
            if (_editorHost.IsAppExiting)
            {
                SaveBeforeClose(code);
                if (_editorHost.CancelSave){
                    _editorHost.CancelSave = false;
                    _editorHost.IsAppExiting = false;
                    _editorHost.Hide();
                }
                else
                {
                    _editorHost.CloseFromInterop = true;
                    _editorHost.Close();
                }
            }
            _editorHost.Hide();
            
        }

    }
}
