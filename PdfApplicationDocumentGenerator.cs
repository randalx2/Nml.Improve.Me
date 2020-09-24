using System;
using System.Linq;
using Nml.Improve.Me.Dependencies;

namespace Nml.Improve.Me
{
	public class PdfApplicationDocumentGenerator : IApplicationDocumentGenerator
	{
		//All of these should be ideally private as they are attributes not properties
		//Should follow a uniform naming convention
		private readonly IDataContext _dataContext;
		private readonly IPathProvider _templatePathProvider;
        private readonly IViewGenerator _viewGenerator;
		private readonly IConfiguration _configuration;
		private readonly ILogger<PdfApplicationDocumentGenerator> _logger;
		private readonly IPdfGenerator _pdfGenerator;

		public PdfApplicationDocumentGenerator(
			IDataContext dataContext,
			IPathProvider templatePathProvider,
			IViewGenerator viewGenerator,
			IConfiguration configuration,
			IPdfGenerator pdfGenerator,
			ILogger<PdfApplicationDocumentGenerator> logger)
		{
			//Exception handling in a constructor ==> exceptions thrown in a constructor is generally not good
			//Rather be handled by separate helper initialization methods

            //Initialize the attributes
			//Check for initializing with null value parameters
			_dataContext = dataContext ?? throw new ArgumentNullException(nameof(dataContext));
			_templatePathProvider = templatePathProvider ?? throw new ArgumentNullException(nameof(templatePathProvider));
			_viewGenerator = viewGenerator;
			_configuration = configuration;
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
			_pdfGenerator = pdfGenerator;
		}
		
		//Implementing the interface method
		//Add exception handling
		public byte[] Generate(Guid applicationId, string baseUri)
		{
			Application application = _dataContext.Applications.Single(app => app.Id == applicationId);

			if (application != null)
			{
				//Can use remove method here to remove the / character.
				//The substring method here may not be the correct method to use
				if (baseUri.EndsWith("/"))
					baseUri = baseUri.Substring(baseUri.Length - 1);

				var view = "";

				if (application.State == ApplicationState.Pending)
				{
					var path = _templatePathProvider.Get("PendingApplication");

					var vm = new PendingApplicationViewModel
					{
						ReferenceNumber = application.ReferenceNumber,
						State = application.State.ToDescription(),
						FullName = application.Person.FirstName + " " + application.Person.Surname,
						AppliedOn = application.Date,
						SupportEmail = _configuration.SupportEmail,
						Signature = _configuration.Signature
					};

					view = _viewGenerator.GenerateFromPath($"{baseUri}{path}", vm);
				}
				else if (application.State == ApplicationState.Activated)
				{
					var path = _templatePathProvider.Get("ActivatedApplication");

					var vm = new ActivatedApplicationViewModel
					{
						ReferenceNumber = application.ReferenceNumber,
						State = application.State.ToDescription(),
						FullName = $"{application.Person.FirstName} {application.Person.Surname}", //Check on this convention for string formatting
						LegalEntity = application.IsLegalEntity ? application.LegalEntity : null,

						//LINQ
						PortfolioFunds = application.Products.SelectMany(p => p.Funds),

						//LINQ Query of a collection
						PortfolioTotalAmount = application.Products.SelectMany(p => p.Funds)
														.Select(f => (f.Amount - f.Fees) * _configuration.TaxRate)
														.Sum(),
						AppliedOn = application.Date,
						SupportEmail = _configuration.SupportEmail,
						Signature = _configuration.Signature
					};

					view = _viewGenerator.GenerateFromPath(baseUri + path, vm);
				}
				else if (application.State == ApplicationState.InReview)
				{
					var templatePath = _templatePathProvider.Get("InReviewApplication");

					//Check on implementation of this new switch statement syntax in C# 8.0
					var inReviewMessage = "Your application has been placed in review" +
										application.CurrentReview.Reason switch
										{
											{ } reason when reason.Contains("address") =>
												" pending outstanding address verification for FICA purposes.",
											{ } reason when reason.Contains("bank") =>
												" pending outstanding bank account verification.",
											_ =>
												" because of suspicious account behaviour. Please contact support ASAP."
					
										};

					//Requires some cleanup
                    var inReviewApplicationViewModel = new InReviewApplicationViewModel
                    {
                        ReferenceNumber = application.ReferenceNumber,
                        State = application.State.ToDescription(),
                        FullName = $"{application.Person.FirstName} {application.Person.Surname}",
                        LegalEntity = application.IsLegalEntity ? application.LegalEntity : null,
                        PortfolioFunds = application.Products.SelectMany(p => p.Funds),
                        PortfolioTotalAmount = application.Products.SelectMany(p => p.Funds)
                            .Select(f => (f.Amount - f.Fees) * _configuration.TaxRate)
                            .Sum(),
                        InReviewMessage = inReviewMessage,
                        InReviewInformation = application.CurrentReview,
                        AppliedOn = application.Date,
                        SupportEmail = _configuration.SupportEmail,
                        Signature = _configuration.Signature
                    };

                    view = _viewGenerator.GenerateFromPath($"{baseUri}{templatePath}", inReviewApplicationViewModel);
				}
				else
				{
					_logger.LogWarning(
						$"The application is in state '{application.State}' and no valid document can be generated for it.");
					return null;
				}

				var pdfOptions = new PdfOptions
				{
					PageNumbers = PageNumbers.Numeric,
					HeaderOptions = new HeaderOptions
					{
						HeaderRepeat = HeaderRepeat.FirstPageOnly,
						HeaderHtml = PdfConstants.Header
					}
				};
				var pdf = _pdfGenerator.GenerateFromHtml(view, pdfOptions);
				return pdf.ToBytes();
			}
			else
			{
				
				_logger.LogWarning(
					$"No application found for id '{applicationId}'");
				return null;
			}
		}
	}
}
