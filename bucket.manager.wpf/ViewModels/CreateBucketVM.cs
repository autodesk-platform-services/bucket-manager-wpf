using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace bucket.manager.wpf.ViewModels
{
    internal class CreateBucketVM : INotifyPropertyChanged
    {
        private bool _addingGuid = true;
        private string _bucketKey = string.Empty;

        public string BucketKey
        {
            get => _bucketKey;
            set => SetField(ref _bucketKey, value);
        }

        public bool AddingGuid
        {
            get => _addingGuid;
            set => SetField(ref _addingGuid, value);
        }

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
