﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Web.Http;
using Glav.PayMeBack.Web.Domain.Services;
using Glav.PayMeBack.Web.Models;

namespace Glav.PayMeBack.Web.Controllers.Api
{
    public class SignInController : ApiController
    {
    	private ISignInService _signInService;

    	public SignInController(ISignInService signInService)
    	{
    		_signInService = signInService;
    	}
		public SignInResponse PostSignInDetails(string email, string password)
		{
			var response = new SignInResponse();
			var user = _signInService.SignIn(email, password);
			if (user != null)
			{
				response.IsSuccessful = true;
				response.UserId = user.Id;
			}
			return response;
		}
    }
}
