﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Glav.PayMeBack.Web.Data;
using Glav.PayMeBack.Web.Domain;

namespace PayMeBackWeb.UnitTests.Controllers.ApiControllerTests
{
	public class MockRepository : IRepository
	{
		public User GetUser(string emailAddress)
		{
			if (emailAddress == "exists@domain.com")
			{
				return new User() { EmailAddress = emailAddress, FirstNames = "Exists", Surname = "Domain", Id = Guid.NewGuid()};
			}

			return null;
		}


		public User GetUser(Guid userId)
		{
			return null;
		}


		public Guid AddUser(User user, string password)
		{
			return Guid.NewGuid();
		}

		public string GetUserPassword(string emailAddress)
		{
			return "password";
		}
	}
}
