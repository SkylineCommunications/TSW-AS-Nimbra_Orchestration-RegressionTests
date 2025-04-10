﻿namespace RT_Validate_Acknowledgment
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Net.Http;
	using System.Text;
	using System.Threading.Tasks;
	using System.Xml;

	using Library.SharedTestCases;
	using Library.Tests.TestCases;

	using QAPortalAPI.Models.ReportingModels;

	using Skyline.DataMiner.Automation;

	public class ValidateAcknowledgment : ITestCase
	{
		private readonly string _xmlRequest;
		private readonly string _endpoint;

		public ValidateAcknowledgment(AcknowledgmentParameters parameters)
		{
			_endpoint = parameters.Endpoint;

			Name = $"Validate Acknowledgment: {parameters.JobName} ({parameters.Source} -> {parameters.Destination})";

			XmlDocument doc = new XmlDocument();

			// Add XML declaration
			XmlDeclaration xmlDeclaration = doc.CreateXmlDeclaration("1.0", "UTF-8", null);
			doc.AppendChild(xmlDeclaration);

			XmlElement root = doc.CreateElement("InteropSetup");
			doc.AppendChild(root);

			AddElement(doc, root, "MessageType", "New");
			AddElement(doc, root, "CircuitID", parameters.ChainId);
			AddElement(doc, root, "SharedID", "18922861");
			AddElement(doc, root, "WorkOrder", parameters.WorkOrder);
			AddElement(doc, root, "Client", "CL_ID 10000003");
			AddElement(doc, root, "JobName", parameters.JobName);
			AddElement(doc, root, "Start", parameters.Start.ToString("yyyy/MM/dd HH:mm:ss"));
			AddElement(doc, root, "End", parameters.End.ToString("yyyy/MM/dd HH:mm:ss"));
			AddElement(doc, root, "ServiceDescription", "0 Edge Switch");
			AddElement(doc, root, "ServiceID", "Edge");
			AddElement(doc, root, "Platform", parameters.Platform);
			AddElement(doc, root, "Source", parameters.Source);
			AddElement(doc, root, "SourceGroup", parameters.SourceGroup);
			AddElement(doc, root, "Destination", parameters.Destination);
			AddElement(doc, root, "DestinationGroup", parameters.DestinationGroup);
			AddElement(doc, root, "TimeStamp", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));

			// Create the XML string in the exact format needed without XML declaration
			StringBuilder sb = new StringBuilder();
			using (XmlWriter writer = XmlWriter.Create(sb, new XmlWriterSettings
			{
				Indent = true,
				OmitXmlDeclaration = true,
				Encoding = Encoding.UTF8,
			}))
			{
				doc.WriteTo(writer);
			}

			_xmlRequest = sb.ToString();
		}

		public string Name { get; set; }

		public TestCaseReport TestCaseReport { get; private set; }

		public PerformanceTestCaseReport PerformanceTestCaseReport { get; private set; }

		public void Execute(IEngine engine)
		{
			try
			{
				// Log the XML being sent for debugging purposes
				engine.GenerateInformation($"Sending XML to {_endpoint}:");
				engine.GenerateInformation(_xmlRequest);

				var (isSuccess, responseBody, errorMessage) = SendXmlRequestAsync(_xmlRequest, _endpoint).GetAwaiter().GetResult();

				if (isSuccess)
				{
					engine.GenerateInformation("Received response:");
					engine.GenerateInformation(responseBody);

					bool isValidResponse = ValidateResponseXml(responseBody);

					if (isValidResponse)
					{
						TestCaseReport = TestCaseReport.GetSuccessTestCase(Name);
					}
					else
					{
						TestCaseReport = TestCaseReport.GetFailTestCase(Name, "Response XML format is invalid");
					}
				}
				else
				{
					TestCaseReport = TestCaseReport.GetFailTestCase(Name, $"HTTP Request failed: {errorMessage}");
				}
			}
			catch (Exception ex)
			{
				TestCaseReport = TestCaseReport.GetFailTestCase(Name, $"Exception occurred: {ex.Message}");
			}
		}

		private static void AddElement(XmlDocument doc, XmlElement parent, string name, string value)
		{
			XmlElement element = doc.CreateElement(name);
			element.InnerText = value;
			parent.AppendChild(element);
		}

		private static async Task<(bool isSuccess, string responseBody, string errorMessage)> SendXmlRequestAsync(string xml, string endpoint)
		{
			using (HttpClient client = new HttpClient())
			{
				try
				{
					// Create content with "xmlCmd=" prefix followed by XML
					string content = "xmlCmd=" + xml;
					var stringContent = new StringContent(content, Encoding.UTF8, "text/plain");

					client.DefaultRequestHeaders.Clear();

					// Set additional headers to match Postman
					client.DefaultRequestHeaders.Add("User-Agent", "PostmanRuntime/7.43.0");
					client.DefaultRequestHeaders.Add("Accept", "*/*");

					// Set request timeout if needed
					client.Timeout = TimeSpan.FromSeconds(30);

					// Send the request
					HttpResponseMessage response = await client.PostAsync(endpoint, stringContent).ConfigureAwait(false);

					// Read the response
					string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
					int statusCode = (int)response.StatusCode;

					// Check if successful (200 OK)
					bool isSuccess = response.IsSuccessStatusCode && statusCode == 200;

					return (isSuccess, responseBody, isSuccess ? null : $"Response status code: {statusCode}");
				}
				catch (Exception ex)
				{
					return (false, null, ex.Message);
				}
			}
		}

		private static bool ValidateResponseXml(string xml)
		{
			try
			{
				// Parse the XML
				XmlDocument doc = new XmlDocument();
				doc.LoadXml(xml);

				// Validate structure - check if it matches expected response format
				XmlNode interopSetup = doc.SelectSingleNode("/InteropSetup");
				if (interopSetup == null)
				{
					return false;
				}

				XmlNode response = interopSetup.SelectSingleNode("Response");
				if (response == null)
				{
					return false;
				}

				XmlNode circuitId = response.SelectSingleNode("CircuitID");
				if (circuitId == null)
				{
					return false;
				}

				XmlNode messageType = response.SelectSingleNode("MessageType");
				if (messageType == null || messageType.InnerText != "New")
				{
					return false;
				}

				XmlNode statusCode = response.SelectSingleNode("StatusCode");
				if (statusCode == null || statusCode.InnerText != "200")
				{
					return false;
				}

				// All validations passed
				return true;
			}
			catch (Exception)
			{
				// XML parsing failed
				return false;
			}
		}
	}
}