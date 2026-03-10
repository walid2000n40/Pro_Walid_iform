using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using ProWalid.Models;
using ProWalid.ViewModels;
using System;

namespace ProWalid.Views
{
    public sealed partial class AddTransactionPage : Page
    {
        public AddTransactionPageViewModel ViewModel { get; }

        public AddTransactionPage()
        {
            this.InitializeComponent();
            ViewModel = new AddTransactionPageViewModel();
            this.DataContext = ViewModel;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ViewModel.SetFrame(this.Frame);
            
            if (e.Parameter is Transaction transaction)
            {
                ViewModel.LoadTransaction(transaction);
            }
            else if (e.Parameter is Customer customer)
            {
                ViewModel.LoadCustomer(customer);
            }
        }

        private async void CompanyNameAutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
            {
                return;
            }

            await ViewModel.UpdateCompanySuggestionsAsync(sender.Text);
        }

        private void CompanyNameAutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (args.SelectedItem is SuggestionEntry selectedSuggestion)
            {
                ViewModel.CompanyName = selectedSuggestion.Value;
                sender.Text = selectedSuggestion.Value;
            }
        }

        private async void EmployeeNameAutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
            {
                return;
            }

            await ViewModel.UpdateEmployeeSuggestionsAsync(sender.Text);
        }

        private void EmployeeNameAutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (args.SelectedItem is SuggestionEntry selectedSuggestion)
            {
                ViewModel.EmployeeName = selectedSuggestion.Value;
                sender.Text = selectedSuggestion.Value;
            }
        }

        private async void ItemDescriptionAutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
            {
                return;
            }

            if (sender.DataContext is TransactionItemDetail item)
            {
                await ViewModel.UpdateItemSuggestionsAsync(item, sender.Text);
            }
        }

        private void ItemDescriptionAutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (sender.DataContext is TransactionItemDetail item && args.SelectedItem is SuggestionEntry selectedSuggestion)
            {
                item.ServiceName = selectedSuggestion.Value;
                sender.Text = selectedSuggestion.Value;
            }
        }

        private async void DeleteSuggestionButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not SuggestionEntry suggestion)
            {
                return;
            }

            await ViewModel.DeleteSuggestionAsync(suggestion);
        }
    }
}
