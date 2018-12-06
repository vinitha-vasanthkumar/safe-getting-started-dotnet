using Rg.Plugins.Popup.Extensions;
using SafeTodoExample.Helpers;
using SafeTodoExample.Model;
using SafeTodoExample.ViewModel;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace SafeTodoExample.View
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class AddItem
    {
        private readonly TodoItem _item;
        private readonly bool _edit;
        private AddItemViewModel _viewModel;

        public AddItem()
        {
            InitializeComponent();
        }

        public AddItem(TodoItem item)
        {
            InitializeComponent();
            _edit = true;
            _item = item;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            if (_viewModel == null)
            {
                if (_edit)
                {
                    _viewModel = new AddItemViewModel(_item, _edit);
                }
                else
                {
                    _viewModel = new AddItemViewModel();
                }
            }

            BindingContext = _viewModel;
            MessageCenterSubscribe();
        }

        public void MessageCenterUnsubscribe()
        {
            MessagingCenter.Unsubscribe<AddItemViewModel>(this, MessengerConstants.HidePopUp);
        }

        public void MessageCenterSubscribe()
        {
            MessagingCenter.Subscribe<AddItemViewModel>(
               this,
               MessengerConstants.HidePopUp,
               async sender =>
               {
                   await Navigation.PopPopupAsync();
               });
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            MessageCenterUnsubscribe();
        }
    }
}
