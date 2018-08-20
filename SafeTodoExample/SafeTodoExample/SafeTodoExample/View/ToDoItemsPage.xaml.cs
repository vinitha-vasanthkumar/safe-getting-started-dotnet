using SafeTodoExample.Helpers;
using SafeTodoExample.ViewModel;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace SafeTodoExample.View
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class ToDoItemsPage : ContentPage
    {
        private TodoItemsPageViewModel viewModel;
        public ToDoItemsPage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (viewModel == null)
            {
                viewModel = new TodoItemsPageViewModel();
            }

            BindingContext = viewModel;
            MessageCenterSubscribe();

            TodoItemListView.ItemTapped += TodoItemListView_ItemTapped;
        }

        private void TodoItemListView_ItemTapped(object sender, ItemTappedEventArgs e)
        {
            var listview = (ListView)sender;
            listview.SelectedItem = null;
        }

        public void MessageCenterUnsubscribe()
        {
            MessagingCenter.Unsubscribe<AddItemViewModel>(this, MessengerConstants.RefreshItemList);
            MessagingCenter.Unsubscribe<TodoItemsPageViewModel>(this, MessengerConstants.NavigateToAuthPage);
        }

        public void MessageCenterSubscribe()
        {
            MessagingCenter.Subscribe<AddItemViewModel>(
               this,
               MessengerConstants.RefreshItemList,
               sender =>
               {
                   Device.BeginInvokeOnMainThread(() =>
                   {
                       if (viewModel.RefereshItemCommand.CanExecute(null))
                       {
                           viewModel.RefereshItemCommand.Execute(null);
                       }
                   });
               });

            MessagingCenter.Subscribe<TodoItemsPageViewModel>(
               this,
               MessengerConstants.NavigateToAuthPage,
               sender =>
               {
                   Application.Current.MainPage = new MainPage();
               });
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            MessageCenterUnsubscribe();
            TodoItemListView.ItemTapped -= TodoItemListView_ItemTapped;
        }
    }
}