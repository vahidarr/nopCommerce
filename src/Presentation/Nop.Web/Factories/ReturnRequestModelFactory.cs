﻿using System;
using System.Collections.Generic;
using System.Linq;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Tax;
using Nop.Services.Catalog;
using Nop.Services.Directory;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Seo;
using Nop.Web.Infrastructure.Cache;
using Nop.Web.Models.Order;

namespace Nop.Web.Factories
{
    public partial class ReturnRequestModelFactory : IReturnRequestModelFactory
    {
		#region Fields

        private readonly IReturnRequestService _returnRequestService;
        private readonly IOrderService _orderService;
        private readonly IWorkContext _workContext;
        private readonly IStoreContext _storeContext;
        private readonly ICurrencyService _currencyService;
        private readonly IPriceFormatter _priceFormatter;
        private readonly ILocalizationService _localizationService;
        private readonly IDateTimeHelper _dateTimeHelper;
        private readonly ICacheManager _cacheManager;

        #endregion

        #region Constructors

        public ReturnRequestModelFactory(IReturnRequestService returnRequestService,
            IOrderService orderService, 
            IWorkContext workContext, 
            IStoreContext storeContext,
            ICurrencyService currencyService, 
            IPriceFormatter priceFormatter,
            ILocalizationService localizationService,
            IDateTimeHelper dateTimeHelper,
            ICacheManager cacheManager)
        {
            this._returnRequestService = returnRequestService;
            this._orderService = orderService;
            this._workContext = workContext;
            this._storeContext = storeContext;
            this._currencyService = currencyService;
            this._priceFormatter = priceFormatter;
            this._localizationService = localizationService;
            this._dateTimeHelper = dateTimeHelper;
            this._cacheManager = cacheManager;
        }

        #endregion

        #region Methods

        public virtual SubmitReturnRequestModel.OrderItemModel PrepareSubmitReturnRequestOrderItemModel(OrderItem orderItem)
        {
            if (orderItem == null)
                throw new ArgumentNullException("orderItem");

            var order = orderItem.Order;

            var model = new SubmitReturnRequestModel.OrderItemModel
            {
                Id = orderItem.Id,
                ProductId = orderItem.Product.Id,
                ProductName = orderItem.Product.GetLocalized(x => x.Name),
                ProductSeName = orderItem.Product.GetSeName(),
                AttributeInfo = orderItem.AttributeDescription,
                Quantity = orderItem.Quantity
            };

            //unit price
            if (order.CustomerTaxDisplayType == TaxDisplayType.IncludingTax)
            {
                //including tax
                var unitPriceInclTaxInCustomerCurrency = _currencyService.ConvertCurrency(orderItem.UnitPriceInclTax, order.CurrencyRate);
                model.UnitPrice = _priceFormatter.FormatPrice(unitPriceInclTaxInCustomerCurrency, true, order.CustomerCurrencyCode, _workContext.WorkingLanguage, true);
            }
            else
            {
                //excluding tax
                var unitPriceExclTaxInCustomerCurrency = _currencyService.ConvertCurrency(orderItem.UnitPriceExclTax, order.CurrencyRate);
                model.UnitPrice = _priceFormatter.FormatPrice(unitPriceExclTaxInCustomerCurrency, true, order.CustomerCurrencyCode, _workContext.WorkingLanguage, false);
            }

            return model;
        }

        public virtual SubmitReturnRequestModel PrepareSubmitReturnRequestModel(SubmitReturnRequestModel model, Order order)
        {
            if (order == null)
                throw new ArgumentNullException("order");

            if (model == null)
                throw new ArgumentNullException("model");

            model.OrderId = order.Id;

            //return reasons
            model.AvailableReturnReasons = _cacheManager.Get(string.Format(ModelCacheEventConsumer.RETURNREQUESTREASONS_MODEL_KEY, _workContext.WorkingLanguage.Id),
                () =>
                {
                    var reasons = new List<SubmitReturnRequestModel.ReturnRequestReasonModel>();
                    foreach (var rrr in _returnRequestService.GetAllReturnRequestReasons())
                        reasons.Add(new SubmitReturnRequestModel.ReturnRequestReasonModel
                        {
                            Id = rrr.Id,
                            Name = rrr.GetLocalized(x => x.Name)
                        });
                    return reasons;
                });

            //return actions
            model.AvailableReturnActions = _cacheManager.Get(string.Format(ModelCacheEventConsumer.RETURNREQUESTACTIONS_MODEL_KEY, _workContext.WorkingLanguage.Id),
                () =>
                {
                    var actions = new List<SubmitReturnRequestModel.ReturnRequestActionModel>();
                    foreach (var rra in _returnRequestService.GetAllReturnRequestActions())
                        actions.Add(new SubmitReturnRequestModel.ReturnRequestActionModel
                        {
                            Id = rra.Id,
                            Name = rra.GetLocalized(x => x.Name)
                        });
                    return actions;
                });

            //returnable products
            var orderItems = order.OrderItems.Where(oi => !oi.Product.NotReturnable);
            foreach (var orderItem in orderItems)
            {
                var orderItemModel = PrepareSubmitReturnRequestOrderItemModel(orderItem);
                model.Items.Add(orderItemModel);
            }

            return model;
        }

        public virtual CustomerReturnRequestsModel PrepareCustomerReturnRequestsModel()
        {
            var model = new CustomerReturnRequestsModel();

            var returnRequests = _returnRequestService.SearchReturnRequests(_storeContext.CurrentStore.Id, 
                _workContext.CurrentCustomer.Id);
            foreach (var returnRequest in returnRequests)
            {
                var orderItem = _orderService.GetOrderItemById(returnRequest.OrderItemId);
                if (orderItem != null)
                {
                    var product = orderItem.Product;

                    var itemModel = new CustomerReturnRequestsModel.ReturnRequestModel
                    {
                        Id = returnRequest.Id,
                        CustomNumber = returnRequest.CustomNumber,
                        ReturnRequestStatus = returnRequest.ReturnRequestStatus.GetLocalizedEnum(_localizationService, _workContext),
                        ProductId = product.Id,
                        ProductName = product.GetLocalized(x => x.Name),
                        ProductSeName = product.GetSeName(),
                        Quantity = returnRequest.Quantity,
                        ReturnAction = returnRequest.RequestedAction,
                        ReturnReason = returnRequest.ReasonForReturn,
                        Comments = returnRequest.CustomerComments,
                        CreatedOn = _dateTimeHelper.ConvertToUserTime(returnRequest.CreatedOnUtc, DateTimeKind.Utc),
                    };
                    model.Items.Add(itemModel);
                }
            }

            return model;
        }
        
        #endregion
    }
}
