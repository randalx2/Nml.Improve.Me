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

        //NB This assumes that the parameters passed in are from derived classes which already implement their respective interfaces
        //Achieves abstraction and DI
		public PdfApplicationDocumentGenerator(
			IDataContext dataContext,
			IPathProvider templatePathProvider,
			IViewGenerator viewGenerator,
			IConfiguration configuration,
			IPdfGenerator pdfGenerator,
			ILogger<PdfApplicationDocumentGenerator> logger)
		{
            //Initialize the attributes
			//Check for initializing with null value parameters
			_dataContext = dataContext ?? throw new ArgumentNullException(nameof(dataContext));
			_templatePathProvider = templatePathProvider ?? throw new ArgumentNullException(nameof(templatePathProvider));
			_viewGenerator = viewGenerator ?? throw new ArgumentNullException(nameof(viewGenerator));
			_configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
			_pdfGenerator = pdfGenerator ?? throw new ArgumentNullException(nameof(pdfGenerator));
		}
		
		//Implementing the interface method
		//Add exception handling
		public byte[] Generate(Guid applicationId, string baseUri)
		{
			var application = _dataContext.Applications.Single(app => app.Id == applicationId);

			if (application != null)
			{
                //The substring method here may not be the correct method to use
                //Normally a good base Uri ends with a trailing slash
                //Currently all this would do is return the "/" character as a substring

                /*********************************************************************************
                if (baseUri.EndsWith("/"))
					baseUri = baseUri.Substring(baseUri.Length - 1);
                **********************************************************************************/

                //Check for a bad baseUri
                if (!baseUri.EndsWith("/"))
                {
                    //append the trailing slash character
                    baseUri += "/";

                    //NB That this would work assuming that _templatePathProvider.Get() would return a file string path 
                    //which DOES NOT start with a "/" character
                }

                //Initialize string variable
                var view = "";

				switch (application.State)
                {
                    case ApplicationState.Pending:
                    {
                        var path = _templatePathProvider.Get("PendingApplication");

                        //Due to the convention used above for the base Uri, we cannot allow any paths to start with a "/" character
                        //Ideally this check should be done in the class which implements IPathProvider's Get() method but is added here as an extra check.
                        if (path.StartsWith("/") && path.Length > 1)
                        {
                            path = path.Substring(1);
                        }

                        var vm = new PendingApplicationViewModel
                        {
                            ReferenceNumber = application.ReferenceNumber,
                            State = application.State.ToDescription(),
                            FullName = application.Person.FirstName + " " + application.Person.Surname,
                            AppliedOn = application.Date,
                            SupportEmail = _configuration.SupportEmail,
                            Signature = _configuration.Signature
                        };

                        //Add some exception handling before generating the view as an extra measure
                        try
                        {
                            view = _viewGenerator.GenerateFromPath($"{baseUri}{path}", vm);
                        }
                        catch (Exception e)
                        {
                            _logger.LogWarning("Error generating view: " + e.Message);
                            throw;
                        }

                        break;
                    }
                    case ApplicationState.Activated:
                    {
                        var path = _templatePathProvider.Get("ActivatedApplication");

                        //Due to the convention used above for the base Uri, we cannot allow any paths to start with a "/" character
                        //Ideally this check should be done in the class which implements IPathProvider's Get() method but is added here as an extra check.
                        if (path.StartsWith("/") && path.Length > 1)
                        {
                            path = path.Substring(1);
                        }

                        var vm = new ActivatedApplicationViewModel
                        {
                            ReferenceNumber = application.ReferenceNumber,
                            State = application.State.ToDescription(),
                            FullName = $"{application.Person.FirstName} {application.Person.Surname}", 
                            LegalEntity = application.IsLegalEntity ? application.LegalEntity : null,

                            PortfolioFunds = application.Products.SelectMany(p => p.Funds),

                            PortfolioTotalAmount = application.Products.SelectMany(p => p.Funds)
                                .Select(f => (f.Amount - f.Fees) * _configuration.TaxRate)
                                .Sum(),

                            AppliedOn = application.Date,
                            SupportEmail = _configuration.SupportEmail,
                            Signature = _configuration.Signature
                        };

                        //Add some exception handling before generating the view as an extra measure
                        try
                        {
                            view = _viewGenerator.GenerateFromPath(baseUri + path, vm);
                        }
                        catch (Exception e)
                        {
                            _logger.LogWarning("Error generating view: " + e.Message);
                            throw;
                        }

                        break;
                    }
                    case ApplicationState.InReview:
                    {
                        var templatePath = _templatePathProvider.Get("InReviewApplication");

                        //Due to the convention used above for the base Uri, we cannot allow any paths to start with a "/" character
                        //Ideally this check should be done in the class which implements IPathProvider's Get() method but is added here as an extra check.
                        if (templatePath.StartsWith("/") && templatePath.Length > 1)
                        {
                            templatePath = templatePath.Substring(1);
                        }

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

                        //Add some exception handling before generating the view as an extra measure
                        try
                        {
                            view = _viewGenerator.GenerateFromPath($"{baseUri}{templatePath}", inReviewApplicationViewModel);
                        }
                        catch (Exception e)
                        {
                            _logger.LogWarning("Error generating view: " + e.Message);
                            throw;
                        }

                        break;
                    }
                    default:
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

				//Add exception handling before generating the pdf
                try
                {
                    var pdf = _pdfGenerator.GenerateFromHtml(view, pdfOptions);
                    return pdf.ToBytes();
                }
                catch (Exception e)
                {
                    _logger.LogWarning("Error generating pdf: " + e.Message);
                    throw;
                }
				
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
