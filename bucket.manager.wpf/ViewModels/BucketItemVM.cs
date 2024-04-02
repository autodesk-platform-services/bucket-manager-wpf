using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace bucket.manager.wpf.ViewModels
{
    // View Model of a bucket in the bucket tree.
    internal class BucketItemVM : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _key = string.Empty;
        private readonly BucketVM _parent ;
        private bool _isExpanded = false;

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetField(ref _isExpanded, value);
        }
        public BucketItemVM(BucketVM parent)
        {
            _parent = parent;
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
