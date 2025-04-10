﻿namespace RT_Validate_Booking
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Net.Http;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using System.Xml;

	using Library.SharedTestCases;
	using Library.Tests.TestCases;

	using QAPortalAPI.Models.ReportingModels;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Common;
	using Skyline.DataMiner.Library.Reservation;
	using Skyline.DataMiner.Library.Resource;
	using Skyline.DataMiner.Library.Solutions.SRM;
	using Skyline.DataMiner.Library.Solutions.SRM.Model;
	using Skyline.DataMiner.Library.Solutions.SRM.Model.ReservationAction;
	using Skyline.DataMiner.Net;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Jobs;
	using Skyline.DataMiner.Net.LogHelpers;
	using Skyline.DataMiner.Net.ManagerStore;
	using Skyline.DataMiner.Net.Messages;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.Net.ResourceManager.Objects;
	using Skyline.DataMiner.Net.Sections;

	public class ValidateBooking : ITestCase
	{
		private const string InputGroup = "Input Group";
		private const string OutputGroup = "Output Group";
		private const string InputName = "Input Name";
		private const string OutputName = "Output Name";
		private readonly AcknowledgmentParameters _parameters;

		public ValidateBooking(AcknowledgmentParameters parameters)
		{
			_parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));

			Name = $"Validate Booking: {parameters.JobName} ({parameters.Source} -> {parameters.Destination})";
		}

		public string Name { get; set; }

		public TestCaseReport TestCaseReport { get; private set; }

		public PerformanceTestCaseReport PerformanceTestCaseReport { get; private set; }

		public void Execute(IEngine engine)
		{
			try
			{
				bool isSuccess = IsValidBooking(engine);

				if (isSuccess)
				{
					TestCaseReport = TestCaseReport.GetSuccessTestCase(Name);
				}
				else
				{
					TestCaseReport = TestCaseReport.GetFailTestCase(Name, "The work Order was not created correctly");
				}
			}
			catch (Exception ex)
			{
				TestCaseReport = TestCaseReport.GetFailTestCase(Name, $"Exception occurred: {ex.Message}");
			}
		}

		private static string GetBookingName(string circuitId, string workOrder, string jobName)
		{
			string bookingname;

			if (String.IsNullOrEmpty(jobName))
			{
				bookingname = $"{workOrder}-{circuitId}";
			}
			else
			{
				bookingname = $"{workOrder}-{circuitId}-{jobName}";
			}

			char[] invalidChars = Path.GetInvalidFileNameChars();
			bookingname = String.Join(string.Empty, bookingname.Select(c => invalidChars.Contains(c) ? ' ' : c));
			return bookingname;
		}

		private static ReservationInstance[] GetCurrentReservation(string circuitId, string workOrder, string jobName)
		{
			var bookingName = GetBookingName(circuitId, workOrder, jobName);
			var filterName = ReservationInstanceExposers.Name.Equal(bookingName);
			var reservation = SrmManagers.ResourceManager.GetReservationInstances(filterName);

			return reservation;
		}

		private bool IsValidBooking(IEngine engine)
		{
			var reservation = GetCurrentReservation(_parameters.ChainId, _parameters.WorkOrder, _parameters.JobName);
			if (reservation == null)
			{
				return false;
			}

			var currentReservation = reservation[0];

			var inputname = currentReservation.GetPropertyByName(InputName);
			var outputname = currentReservation.GetPropertyByName(OutputName);
			var inputGroup = currentReservation.GetPropertyByName(InputGroup);
			var outputgroup = currentReservation.GetPropertyByName(OutputGroup);

			if (!Convert.ToString(inputname).Equals(_parameters.Source) && !Convert.ToString(outputname).Equals(_parameters.Destination))
			{
				engine.Log($"prop dont match 1");
				return false;
			}

			if (!Convert.ToString(inputGroup).Equals(_parameters.SourceGroup) && !Convert.ToString(outputgroup).Equals(_parameters.DestinationGroup))
			{
				engine.Log($"prop dont match 2");
				return false;
			}

			return true;
		}
	}
}