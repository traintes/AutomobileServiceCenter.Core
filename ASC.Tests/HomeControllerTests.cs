using ASC.Tests.TestUtilities;
using ASC.Utilities;
using ASC.Web.Configuration;
using ASC.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace ASC.Tests
{
    public class HomeControllerTests
    {
        private readonly Mock<IOptions<ApplicationSettings>> optionsMock;
        private readonly Mock<HttpContext> mockHttpContext;

        public HomeControllerTests()
        {
            // Create an instance of Mock IOptions
            this.optionsMock = new Mock<IOptions<ApplicationSettings>>();
            this.mockHttpContext = new Mock<HttpContext>();

            // Set FakeSession to HttpContext session
            this.mockHttpContext.Setup(p => p.Session).Returns(new FakeSession());

            // Set IOptions<> values property to return ApplicationSettings object
            this.optionsMock.Setup(ap => ap.Value).Returns(new ApplicationSettings
            {
                ApplicationTitle = "ASC",
            });
        }

        [Fact]
        public void HomeController_Index_View_Test()
        {
            // Home controller instantiated with Mock IOptions<> object
            HomeController controller = new HomeController(optionsMock.Object);
            controller.ControllerContext.HttpContext = this.mockHttpContext.Object;

            Assert.IsType(typeof(ViewResult), controller.Index());
        }

        [Fact]
        public void HomeController_Index_NoModel_Test()
        {
            HomeController controller = new HomeController(optionsMock.Object);
            controller.ControllerContext.HttpContext = this.mockHttpContext.Object;

            // Assert Model for Null
            Assert.Null((controller.Index() as ViewResult).ViewData.Model);
        }

        [Fact]
        public void HomeController_Index_Validation_Test()
        {
            HomeController controller = new HomeController(optionsMock.Object);
            controller.ControllerContext.HttpContext = this.mockHttpContext.Object;

            // Assert ModelState Error Count to 0
            Assert.Equal(0, (controller.Index() as ViewResult).ViewData.ModelState.ErrorCount);
        }

        [Fact]
        public void HomeController_Index_Session_Test()
        {
            HomeController controller = new HomeController(optionsMock.Object);
            controller.ControllerContext.HttpContext = this.mockHttpContext.Object;

            controller.Index();

            // Session value with key "Test" should not be null.
            Assert.NotNull(controller.HttpContext.Session.GetSession<ApplicationSettings>("Test"));
        }
    }
}
