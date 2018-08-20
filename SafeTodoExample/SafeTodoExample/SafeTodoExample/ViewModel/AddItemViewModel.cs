using Rg.Plugins.Popup.Extensions;
using SafeTodoExample.Model;
using SafeTodoExample.ViewModel.Base;
using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Xamarin.Forms;

namespace SafeTodoExample.ViewModel
{
    public class AddItemViewModel : BaseViewModel
    {
        private string _title;
        private string _detail;
        public string Title { get => _title; set => SetProperty(ref _title, value); }
        public string Details { get => _detail; set => SetProperty(ref _detail, value); }
        public bool Edit { get; }

        public ICommand AddItemCommand { get; private set; }
        public ICommand CloseDialogCommand { get; private set; }

        public AddItemViewModel()
        {
            InitializeCommands();
        }

        public AddItemViewModel(TodoItem item, bool edit)
        {
            Title = item.Title ?? string.Empty;
            Details = item.Detail ?? string.Empty;
            Edit = edit;
            InitializeCommands();
        }

        private void InitializeCommands()
        {
            AddItemCommand = new Command(async () => await OnAddItemCommand());
            CloseDialogCommand = new Command(async () => await OnCloseDialogCommand());
        }

        private async Task OnCloseDialogCommand()
        {
            await Application.Current.MainPage.Navigation.PopPopupAsync(true);
        }

        private async Task OnAddItemCommand()
        {
            try
            {
                if (Title.Length > 150)
                {
                    throw new Exception("Max subject length is 150 characters.");
                }

                if (Details.Length > 150)
                {
                    throw new Exception("Max body length is 150 characters.");
                }
                
                if (Edit)
                {
                    await AppService.UpdateItemAsync(new TodoItem { Title = Title, Detail = Details });
                }
                else
                {
                    await AppService.AddItemAsync(new TodoItem { Title = Title, Detail = Details });
                }

                Helpers.DialogHelper.ShowToast("Adding/Updating entry...", Helpers.DialogType.Information);

                await Application.Current.MainPage.Navigation.PopPopupAsync(true);
                MessagingCenter.Send(this, Helpers.MessengerConstants.RefreshItemList);
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", $"Add Item Failed: {ex.Message}", "OK");
            }
        }
    }
}
