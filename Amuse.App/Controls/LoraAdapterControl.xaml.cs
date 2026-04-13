using Amuse.App.Common;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using TensorStack.Common;
using TensorStack.WPF;
using TensorStack.WPF.Controls;

namespace Amuse.App.Controls
{
    /// <summary>
    /// Interaction logic for LoraAdapterControl.xaml
    /// </summary>
    public partial class LoraAdapterControl : BaseControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LoraAdapterControl"/> class.
        /// </summary>
        public LoraAdapterControl()
        {
            AddCommand = new AsyncRelayCommand(AddAsync, CanAdd);
            RemoveCommand = new AsyncRelayCommand<LoraAdapterModel>(RemoveAsync, CanRemove);
            InitializeComponent();
        }

        public static readonly DependencyProperty LoraCollectionViewProperty = DependencyProperty.Register(nameof(LoraCollectionView), typeof(ListCollectionView), typeof(LoraAdapterControl));
        public static readonly DependencyProperty LoraAdaptersProperty = DependencyProperty.Register(nameof(LoraAdapters), typeof(ObservableCollection<LoraAdapterModel>), typeof(LoraAdapterControl), new PropertyMetadata(new ObservableCollection<LoraAdapterModel>()));

        public ListCollectionView LoraCollectionView
        {
            get { return (ListCollectionView)GetValue(LoraCollectionViewProperty); }
            set { SetValue(LoraCollectionViewProperty, value); }
        }

        public ObservableCollection<LoraAdapterModel> LoraAdapters
        {
            get { return (ObservableCollection<LoraAdapterModel>)GetValue(LoraAdaptersProperty); }
            set { SetValue(LoraAdaptersProperty, value); }
        }

        public AsyncRelayCommand AddCommand { get; }
        public AsyncRelayCommand<LoraAdapterModel> RemoveCommand { get; }


        private Task AddAsync()
        {
            var nextIndex = LoraCollectionView.IndexOf(LoraAdapters.Last()) + 1;
            var nextLoraAdapter = nextIndex > 0 && nextIndex < LoraCollectionView.Count ? LoraCollectionView.GetItemAt(nextIndex) as LoraAdapterModel : new LoraAdapterModel();
            LoraAdapters.Add(nextLoraAdapter);
            return Task.CompletedTask;
        }


        private bool CanAdd()
        {
            if (LoraAdapters.IsNullOrEmpty())
                return false;

            return LoraAdapters.All(x => !string.IsNullOrEmpty(x.Name)) && LoraAdapters.Count <= 6;
        }


        private Task RemoveAsync(LoraAdapterModel loraAdapter)
        {
            LoraAdapters.Remove(loraAdapter);
            return Task.CompletedTask;
        }


        private bool CanRemove(LoraAdapterModel loraAdapter)
        {
            return !LoraAdapters.IsNullOrEmpty() && LoraAdapters.Count > 1;
        }


        private void ComboBoxLora_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && int.TryParse(comboBox.Tag.ToString(), out var index))
            {
                var lora = (e.AddedItems[0] as LoraAdapterModel);
                if (LoraAdapters.ElementAtOrDefault(index) != lora)
                    LoraAdapters[index] = lora;
            }
        }

    }
}
