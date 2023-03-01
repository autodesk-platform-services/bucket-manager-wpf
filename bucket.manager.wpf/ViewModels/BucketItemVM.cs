using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace bucket.manager.wpf.ViewModels
{
    internal class BucketItemVM : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _key = string.Empty;
        private bool _isExpanded = false;
        private readonly BucketVM _parent ;

        public BucketItemVM(BucketVM parent)
        {
            _parent = parent;
        }
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetField(ref _isExpanded, value);
        }
        public string Name
        {
            get => _name;
            set => SetField(ref _name, value);
        }

        public string Key
        {
            get => _key;
            set => SetField(ref _key, value);
        }

        public void Remove()
        {
            _parent.Items.Remove(this);
        }

        public string ParentKey => _parent.Key;


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
