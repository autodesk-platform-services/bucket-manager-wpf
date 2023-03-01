using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace bucket.manager.wpf.ViewModels
{
    internal class MainWindowDataContext : INotifyPropertyChanged
    {
        private string _accessToken = string.Empty;
        private int _progressBarPercentage = 0;
        private string _statusBarText = string.Empty;
        
        // Locking all buttons is not the best solution, we can do better in real production world 
        private bool _uiEnabled = true;

        
        private bool _isProgressBarIndetermined = false;
        private int _progressBarMaximum = 100;
        private bool _isAuthenticating = false;
        private bool _isRefreshingBucket = false;
        private bool _isRefreshingBucketItem = false;
        private bool _isTranslating = false;

        

        public string StatusBarText { get => _statusBarText; set => SetField(ref _statusBarText, value); }
        public int ProgressBarPercentage { get => _progressBarPercentage; set => SetField(ref _progressBarPercentage, value); }
        public int ProgressBarMaximum { get => _progressBarMaximum; set => SetField(ref _progressBarMaximum, value); }
        public string AccessToken { get => _accessToken; set => SetField(ref _accessToken, value); }

        public bool UIEnabled { get => _uiEnabled; set => SetField(ref _uiEnabled, value); }

        public bool IsProgressBarIndetermined { get => _isProgressBarIndetermined; set => SetField(ref _isProgressBarIndetermined, value); }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
