﻿namespace RT_Validate_WorkOrder
{
	using System;
	using System.Collections.Generic;
	using System.IO;
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

	public class ValidateWorkOrder : ITestCase
	{
		private const int ChainPid = 1003;
		private const int WorkOrderPid = 1002;
		private const int IndexSource = 6;
		private const int IndexDestination = 7;
		private const int IndexJobName = 8;
		private const int IndexSourceGroup = 12;
		private const int IndexDestinationGroup = 13;
		private const int IndexStatus = 18;
		private readonly AcknowledgmentParameters _parameters;

		public ValidateWorkOrder(AcknowledgmentParameters parameters)
		{
			_parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));

			Name = $"Validate Work Order: {parameters.JobName} ({parameters.Source} -> {parameters.Destination})";
		}

		public string Name { get; set; }

		public TestCaseReport TestCaseReport { get; private set; }

		public PerformanceTestCaseReport PerformanceTestCaseReport { get; private set; }

		public void Execute(IEngine engine)
		{
			try
			{
				// wait the buffer time for the row to be added
				Thread.Sleep(11000);
				bool isSuccess = IsValidWorkOrder(engine);

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

		private static IDmsElement GetScheduAllElement(IDms dms)
		{
			ICollection<IDmsElement> elements = dms.GetElements();

			foreach (IDmsElement element in elements)
			{
				if (element.Protocol.Name.Equals("ScheduAll Generic Interop Manager") && element.Protocol.Version.Equals("Production", StringComparison.OrdinalIgnoreCase))
				{
					return element;
				}
			}

			return null;
		}

		private bool IsValidWorkOrder(IEngine engine)
		{
			IDms dms = engine.GetDms();
			IDmsElement scheduAll = GetScheduAllElement(dms);

			if (scheduAll == null)
			{
				return false;
			}

			var table = scheduAll.GetTable(1000);
			string[] keys = table.GetPrimaryKeys();

			if (keys.Length == 0)
			{
				return false;
			}

			string key = GetRow(table, keys, engine);

			if (String.IsNullOrEmpty(key))
			{
				return false;
			}

			object[] row = table.GetRow(key);

			if (!Convert.ToString(row[IndexSource]).Equals(_parameters.Source) && !Convert.ToString(row[IndexDestination]).Equals(_parameters.Destination))
			{
				return false;
			}

			if (!Convert.ToString(row[IndexJobName]).Equals(_parameters.JobName))
			{
				return false;
			}

			if (!Convert.ToString(row[IndexSourceGroup]).Equals(_parameters.SourceGroup) && !Convert.ToString(row[IndexDestinationGroup]).Equals(_parameters.DestinationGroup))
			{
				return false;
			}

			if (Convert.ToInt32(row[IndexStatus]) == 7)
			{
				return true;
			}

			var waitTimeParam = scheduAll.GetStandaloneParameter<int?>(106);
			int waitTime = waitTimeParam.GetValue() ?? -1;

			if (waitTime == -1)
			{
				return false;
			}

			waitTime = (waitTime + 5) * 1000; // Buffer time + 5 seconds in ms

			Thread.Sleep(waitTime);
			object[] rowUpdate = table.GetRow(key);

			if (Convert.ToInt32(rowUpdate[IndexStatus]) == 7 || Convert.ToInt32(rowUpdate[IndexStatus]) == 1)
			{
				return true;
			}

			return false;
		}

		private string GetRow(IDmsTable table, string[] keys, IEngine engine)
		{
			var chainIdColumn = table.GetColumn<string>(ChainPid);
			var workOrderIdColumn = table.GetColumn<string>(WorkOrderPid);

			for (int i = 0; i < keys.Length; i++)
			{
				string chainId = chainIdColumn.GetValue(keys[i], KeyType.PrimaryKey);
				string workOrderId = workOrderIdColumn.GetValue(keys[i], KeyType.PrimaryKey);
				if (chainId.Equals(_parameters.ChainId) && workOrderId.Equals(_parameters.WorkOrder))
				{
					return keys[i];
				}
			}

			return String.Empty;
		}
	}
}