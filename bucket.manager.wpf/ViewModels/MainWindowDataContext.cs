using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace bucket.manager.wpf.ViewModels
{
    // View model of the main window.
    internal class MainWindowDataContext : INotifyPropertyChanged
    {
        private string _accessToken = string.Empty;
        private int _progressBarPercentage = 0;
        private string _statusBarText = string.Empty;
        
        // Locking all buttons is not the best solution, we can do better in real production world 
        private bool _uiEnabled = true;
        public string Region = "US";
        
        private bool _isProgressBarIndetermined;
        private int _progressBarMaximum = 100;
        

        public string StatusBarText { get => _statusBarText; set => SetField(ref _statusBarText, value); }
        public int ProgressBarPercentage { get => _progressBarPercentage; set => SetField(ref _progressBarPercentage, value); }
        public int ProgressBarMaximum { get => _progressBarMaximum; set => SetField(ref _progressBarMaximum, value); }
        public string AccessToken { get => _accessToken; set => SetField(ref _accessToken, value); }

        public bool UIEnabled { get => _uiEnabled; set => SetField(ref _uiEnabled, value); }

        public bool IsProgressBarIndetermined { get => _isProgressBarIndetermined; set => SetField(ref _isProgressBarIndetermined, value); }

        public event PropertyChangedEventHandler? PropertyChanged;
        /// <summary>
        /// On a property change, invoke the event.
        /// </summary>
        /// <param name="propertyName"></param>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// When a field is set, check if the value is different and invoke the event.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="field"></param>
        /// <param name="value"></param>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
