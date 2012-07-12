﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using Glav.PayMeBack.Core.Domain;
using Data = Glav.PayMeBack.Web.Data;
using Glav.PayMeBack.Web.Domain.Engines;
using Glav.CacheAdapter.Core;
using System.Transactions;
using Glav.PayMeBack.Web.Helpers;
using Glav.PayMeBack.Web.Data;

namespace Glav.PayMeBack.Web.Domain.Services
{
	public class PaymentPlanService : IPaymentPlanService
	{
		private IUserEngine _userEngine;
		private Data.ICrudRepository _crudRepository;
		private Data.IDebtRepository _debtRepository;
		private ICacheProvider _cacheProvider;
		private const string UserPaymentPlanCacheKey = "UserPaymentPlan_{0}";

		public PaymentPlanService(IUserEngine userEngine, Data.ICrudRepository crudRepository, Data.IDebtRepository debtRepository, ICacheProvider cacheProvider)
		{
			_userEngine = userEngine;
			_crudRepository = crudRepository;
			_debtRepository = debtRepository;
			_cacheProvider = cacheProvider;
		}

		public UserPaymentPlan GetPaymentPlan(Guid userId)
		{
			var paymentPlanDetail = _cacheProvider.Get<UserPaymentPlanDetail>(GetCacheKeyForUserPaymentPlan(userId), DateTime.Now.AddHours(1), () =>
				{
					return _debtRepository.GetUserPaymentPlan(userId);
				});

			var paymentPlan = new UserPaymentPlan();
			if (paymentPlanDetail == null || paymentPlanDetail.Id == Guid.Empty)
			{
				paymentPlan.User = _crudRepository.GetSingle<Data.UserDetail>(u => u.Id == userId).ToModel();
				paymentPlan.DebtsOwedToMe = new List<Debt>();
				paymentPlan.DebtsOwedToOthers = new List<Debt>();
				paymentPlan.Id = Guid.Empty;
				paymentPlan.DateCreated = DateTime.UtcNow;
				return paymentPlan;
			}

			paymentPlan.Id = paymentPlanDetail.Id;
			paymentPlan.User = paymentPlanDetail.UserDetail.ToModel();
			paymentPlan.DateCreated = paymentPlanDetail.DateCreated;

			paymentPlan.DebtsOwedToMe = GetDebtsRelatedToUser(userId, paymentPlanDetail, true);
			paymentPlan.DebtsOwedToOthers = GetDebtsRelatedToUser(userId, paymentPlanDetail, false);
			return paymentPlan;
		}

		private string GetCacheKeyForUserPaymentPlan(Guid userId)
		{
			return string.Format(UserPaymentPlanCacheKey, userId.ToString());
		}

		private List<Debt> GetDebtsRelatedToUser(Guid userId, UserPaymentPlanDetail paymentPlanDetail, bool debtsOwedToUser)
		{
			var debts = new List<Debt>();
			if (paymentPlanDetail.DebtDetails != null && paymentPlanDetail.DebtDetails.Count > 0)
			{
				paymentPlanDetail.DebtDetails.ToList().ForEach(p =>
				{
					if ((p.UserIdWhoOwesDebt != userId && debtsOwedToUser) || (p.UserIdWhoOwesDebt == userId && !debtsOwedToUser))
					{
						debts.Add(p.ToModel());
					}
				});
			}

			return debts;
		}

		public DataAccessResult AddDebtOwed(Guid userId, Debt debt)
		{
			var userPaymentPlan = GetPaymentPlan(userId);
			userPaymentPlan.DebtsOwedToMe.Add(new Debt()
				{
					UserWhoOwesDebt = userPaymentPlan.User,
				});
			return UpdatePaymentPlan(userPaymentPlan);
		}

		public DataAccessResult AddDebtOwing(Guid userId, Debt debt)
		{
			var userPaymentPlan = GetPaymentPlan(userId);
			userPaymentPlan.DebtsOwedToOthers.Add(new Debt()
			{
				UserWhoOwesDebt = userPaymentPlan.User,
			});
			return UpdatePaymentPlan(userPaymentPlan);
		}

		public DataAccessResult UpdatePaymentPlan(UserPaymentPlan usersPaymentPlan)
		{
			var result = new DataAccessResult();
			_cacheProvider.InvalidateCacheItem(GetCacheKeyForUserPaymentPlan(usersPaymentPlan.User.Id));

			using (var scope = new TransactionScope(TransactionScopeOption.Required, new TransactionOptions() { IsolationLevel = IsolationLevel.ReadCommitted }))
			{
				try
				{
					ValidatePlan(usersPaymentPlan);
					AdjustDebtAggregateValues(usersPaymentPlan);
					_debtRepository.UpdateUserPaymentPlan(usersPaymentPlan.ToDataRecord());
					result.WasSuccessfull = true;
					scope.Complete();

				}
				catch (System.Data.Entity.Validation.DbEntityValidationException valEx)
				{
					// log it - return it
					return valEx.ToDataResult();
				}
				catch (Exception ex)
				{
					// log it - return it
					return ex.ToDataResult();
				}
			}
			return result;
		}

		private void AdjustDebtAggregateValues(UserPaymentPlan usersPaymentPlan)
		{
			if (usersPaymentPlan.DebtsOwedToMe != null)
			{
				usersPaymentPlan
					.DebtsOwedToMe
					.ForEach(d =>
						{
							if (d.PaymentInstallments != null)
							{
								var totalPayedOff = d.InitialPayment;
								d.PaymentInstallments.ForEach(p =>
								{
									totalPayedOff += p.AmountPaid;
								});
								if (totalPayedOff == d.TotalAmountOwed)
								{
									d.IsOutstanding = false;
								}
							}
						});
			}
		}

		private void ValidatePlan(UserPaymentPlan usersPaymentPlan)
		{
			if (usersPaymentPlan == null)
			{
				throw new ArgumentException("Payment plan is empty");
			}

			// Ensure the amount of payments does not exceed the total debt.
			// If the debt has been paid in full, then ensure it is no longer
			// marked as outstanding
			if (usersPaymentPlan.DebtsOwedToMe != null)
			{
				usersPaymentPlan
					.DebtsOwedToMe
					.ForEach(d =>
							{
								if (d.PaymentInstallments != null)
								{
									var totalPayedOff = d.InitialPayment;
									d.PaymentInstallments.ForEach(p =>
												{
													totalPayedOff += p.AmountPaid;
												});
									if (totalPayedOff > d.TotalAmountOwed)
									{
										throw new ValidationException("Amount paid off exceeds total debt.");
									}
								}
							});
			}
		}

		public void RemoveDebt(Guid userId, Debt debt)
		{
			throw new NotImplementedException();
		}
	}
}