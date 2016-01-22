﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Cirrious.MvvmCross.ViewModels;
using MoneyManager.Core.Helpers;
using MoneyManager.Core.ViewModels.CategoryList;
using MoneyManager.Foundation;
using MoneyManager.Foundation.Interfaces;
using MoneyManager.Foundation.Messages;
using MoneyManager.Foundation.Model;
using MoneyManager.Localization;
using MvvmCross.Plugins.Messenger;
using PropertyChanged;

namespace MoneyManager.Core.ViewModels
{
    [ImplementPropertyChanged]
    public class ModifyPaymentViewModel : BaseViewModel
    {
        private readonly IAccountRepository accountRepository;
        private readonly IDefaultManager defaultManager;
        private readonly IDialogService dialogService;
        private readonly IPaymentManager paymentManager;
        private readonly IPaymentRepository paymentRepository;

        //this token ensures that we will be notified when a message is sent.
        private readonly MvxSubscriptionToken token;

        public ModifyPaymentViewModel(IPaymentRepository paymentRepository,
            IAccountRepository accountRepository,
            IDialogService dialogService,
            IPaymentManager paymentManager,
            IDefaultManager defaultManager)
        {
            this.paymentRepository = paymentRepository;
            this.dialogService = dialogService;
            this.paymentManager = paymentManager;
            this.defaultManager = defaultManager;
            this.accountRepository = accountRepository;

            token = MessageHub.Subscribe<CategorySelectedMessage>(message => SelectedPayment.Category = message.SelectedCategory);
        }

        /// <summary>
        ///     Init the view. Is executed after the constructor call
        /// </summary>
        /// <param name="typeString">Type of the transaction.</param>
        /// <param name="isEdit">Weather the transaction is in edit mode or not.</param>
        public void Init(string typeString, bool isEdit = false)
        {
            IsEdit = isEdit;
            IsEndless = true;

            amount = 0;

            if (IsEdit)
            {
                PrepareEdit();
            } 
            else
            {
                PrepareDefault(typeString);
            }
        }

        private void PrepareEdit()
        {
            // Monkey patch for issues with binding to the account selection
            // TODO: fix this that the binding works without this.
            SelectedPayment.ChargedAccount =
                accountRepository.Data.FirstOrDefault(x => x.Id == SelectedPayment.ChargedAccountId);

            IsTransfer = SelectedPayment.IsTransfer;
            // set the private amount property. This will get properly formatted and then displayed.
            amount = SelectedPayment.Amount;
            Recurrence = SelectedPayment.IsRecurring
                ? SelectedPayment.RecurringPayment.Recurrence
                : 0;
            EndDate = SelectedPayment.IsRecurring
                ? SelectedPayment.RecurringPayment.EndDate
                : DateTime.Now;
            IsEndless = !SelectedPayment.IsRecurring || SelectedPayment.RecurringPayment.IsEndless;

        }

        private void PrepareDefault(string typeString)
        {
            var type = (PaymentType) Enum.Parse(typeof (PaymentType), typeString);

            SetDefaultTransaction(type);
            SelectedPayment.ChargedAccount = defaultManager.GetDefaultAccount();
            IsTransfer = type == PaymentType.Transfer;
            EndDate = DateTime.Now;
        }

        private void SetDefaultTransaction(PaymentType paymentType)
        {
            SelectedPayment = new Payment
            {
                Type = (int) paymentType,
                Date = DateTime.Now,
                // Assign empty category to reset the GUI
                Category = new Category()
            };
        }

        private async void Save()
        {
            if (SelectedPayment.ChargedAccount == null)
            {
                ShowAccountRequiredMessage();
                return;
            }

            if (SelectedPayment.IsRecurring && !IsEndless && EndDate.Date <= DateTime.Today)
            {
                ShowInvalidEndDateMessage();
                return;
            }

            // Make sure that the old amount is removed to not count the amount twice.
            RemoveOldAmount();
            SelectedPayment.Amount = amount;

            //Create a recurring transaction based on the financial transaction or update an existing
            await PrepareRecurringTransaction();

            // SaveItem or update the transaction and add the amount to the account
            paymentRepository.Save(SelectedPayment);
            accountRepository.AddTransactionAmount(SelectedPayment);

            Close(this);
        }

        private void RemoveOldAmount()
        {
            if (IsEdit)
            {
                accountRepository.RemoveTransactionAmount(SelectedPayment);
            }
        }

        private async Task PrepareRecurringTransaction()
        {
            if ((IsEdit && await paymentManager.CheckForRecurringPayment(SelectedPayment))
                || SelectedPayment.IsRecurring)
            {
                SelectedPayment.RecurringPayment = RecurringPaymentHelper.
                    GetRecurringFromPayment(SelectedPayment,
                        IsEndless,
                        Recurrence,
                        EndDate);
            }
        }

        private void OpenSelectCategoryList()
        {
            ShowViewModel<SelectCategoryListViewModel>();
        }

        private async void Delete()
        {
            if (await dialogService.ShowConfirmMessage(Strings.DeleteTitle, Strings.DeleteTransactionConfirmationMessage))
            {
                paymentRepository.Delete(paymentRepository.Selected);
                accountRepository.RemoveTransactionAmount(SelectedPayment);
                Close(this);
            }
        }

        private async void ShowAccountRequiredMessage()
        {
            await dialogService.ShowMessage(Strings.MandatoryFieldEmptyTitle,
                Strings.AccountRequiredMessage);
        }

        private async void ShowInvalidEndDateMessage()
        {
            await dialogService.ShowMessage(Strings.InvalidEnddateTitle,
                Strings.InvalidEnddateMessage);
        }


        private void ResetSelection()
        {
            SelectedPayment.Category = null;
        }


        private void Cancel()
        {
            Close(this);
        }

        #region Commands

        /// <summary>
        ///     Saves the transaction or updates the existing depending on the IsEdit Flag.
        /// </summary>
        public IMvxCommand SaveCommand => new MvxCommand(Save);

        /// <summary>
        ///     Opens to the SelectCategoryView
        /// </summary>
        public IMvxCommand GoToSelectCategorydialogCommand => new MvxCommand(OpenSelectCategoryList);

        /// <summary>
        ///     Delets the transaction or updates the existing depending on the IsEdit Flag.
        /// </summary>
        public IMvxCommand DeleteCommand => new MvxCommand(Delete);

        /// <summary>
        ///     Cancels the operations.
        /// </summary>
        public IMvxCommand CancelCommand => new MvxCommand(Cancel);

        /// <summary>
        ///     Resets the category of the currently selected transaction
        /// </summary>
        public IMvxCommand ResetCategoryCommand => new MvxCommand(ResetSelection);

        #endregion

        #region Properties

        /// <summary>
        ///     Indicates if the view is in Edit mode.
        /// </summary>
        public bool IsEdit { get; private set; }

        /// <summary>
        ///     Indicates if the transaction is a transfer.
        /// </summary>
        public bool IsTransfer { get; private set; }

        /// <summary>
        ///     Indicates if the reminder is endless
        /// </summary>
        public bool IsEndless { get; set; }

        /// <summary>
        ///     The Enddate for recurring transaction
        /// </summary>
        public DateTime EndDate { get; set; }

        /// <summary>
        ///     The selected recurrence
        /// </summary>
        public int Recurrence { get; set; }

        // This has to be static in order to keep the value even if you leave the page to select a category.
        private double amount;

        /// <summary>
        ///     Property to format amount string to double with the proper culture.
        ///     This is used to prevent issues when converting the amount string to double
        ///     without the correct culture.
        /// </summary>
        public string AmountString
        {
            get { return Utilities.FormatLargeNumbers(amount); }
            set
            {
                double convertedValue;
                if (double.TryParse(value, out convertedValue))
                {
                    amount = convertedValue;
                }
            }
        }

        /// <summary>
        ///     List with the different recurrence types.
        /// </summary>
        public List<string> RecurrenceList => new List<string>
        {
            Strings.DailyLabel,
            Strings.DailyWithoutWeekendLabel,
            Strings.WeeklyLabel,
            Strings.BiweeklyLabel,
            Strings.MonthlyLabel,
            Strings.YearlyLabel
        };

        /// <summary>
        ///     The selected transaction
        /// </summary>
        public Payment SelectedPayment
        {
            get { return paymentRepository.Selected; }
            set { paymentRepository.Selected = value; }
        }

        /// <summary>
        ///     Gives access to all accounts
        /// </summary>
        public ObservableCollection<Account> AllAccounts => accountRepository.Data;

        /// <summary>
        ///     Returns the Title for the page
        /// </summary>
        public string Title => PaymentTypeHelper.GetViewTitleForType(SelectedPayment.Type, IsEdit);

        /// <summary>
        ///     Returns the Header for the account field
        /// </summary>
        public string AccountHeader
            => SelectedPayment?.Type == (int) PaymentType.Income
                ? Strings.TargetAccountLabel
                : Strings.ChargedAccountLabel;

        /// <summary>
        ///     The transaction date
        /// </summary>
        public DateTime Date
        {
            get
            {
                if (!IsEdit && SelectedPayment.Date == DateTime.MinValue)
                {
                    SelectedPayment.Date = DateTime.Now;
                }
                return SelectedPayment.Date;
            }
            set { SelectedPayment.Date = value; }
        }

        #endregion
    }
}